using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Infrastructure.Ai;

public sealed class GeminiApiClient(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    IMemoryCache cache)
    : IGeminiClient
{
    private const string SystemCacheKey = "gemini:system-cache";

    public async Task<GeminiResult> RefineAsync(GeminiGenerationRequest request, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(options.Value.RequestTimeoutSeconds, 5)));

        var cachedContentName = await ResolveCachedContentNameAsync(request, cts.Token);
        var body = BuildGenerateBody(request, cachedContentName);
        var url = $"models/{request.Model}:generateContent?key={options.Value.ApiKey}";

        var httpContent = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, httpContent, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        var root = JObject.Parse(json);

        var text = ExtractText(root);
        var usage = root["usageMetadata"] as JObject;
        var promptTokens = usage?.Value<int?>("promptTokenCount") ?? 0;
        var candidateTokens = usage?.Value<int?>("candidatesTokenCount") ?? 0;
        var cachedTokens = usage?.Value<int?>("cachedContentTokenCount") ?? 0;

        return new GeminiResult(text, promptTokens, candidateTokens, cachedTokens);
    }

    public async IAsyncEnumerable<GeminiStreamChunk> StreamAsync(
        GeminiGenerationRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(options.Value.StreamTimeoutSeconds, 10)));

        var cachedContentName = await ResolveCachedContentNameAsync(request, cts.Token);
        var body = BuildGenerateBody(request, cachedContentName);
        var url = $"models/{request.Model}:streamGenerateContent?alt=sse&key={options.Value.ApiKey}";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        int? finalCachedTokens = null;

        while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line is null)
                break;

            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]")
                break;

            JObject chunkRoot;
            try
            {
                chunkRoot = JObject.Parse(data);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                continue;
            }

            var usage = chunkRoot["usageMetadata"] as JObject;
            if (usage is not null)
                finalCachedTokens = usage.Value<int?>("cachedContentTokenCount");

            var candidates = chunkRoot["candidates"] as JArray;
            if (candidates is null || candidates.Count == 0)
                continue;

            var candidate = candidates[0] as JObject;
            var content = candidate?["content"] as JObject;
            var parts = content?["parts"] as JArray;

            if (parts is null)
                continue;

            foreach (var part in parts.OfType<JObject>())
            {
                var thought = part.Value<bool?>("thought") ?? false;
                var text = part.Value<string>("text") ?? string.Empty;
                yield return new GeminiStreamChunk(text, thought, false, null);
            }
        }

        yield return new GeminiStreamChunk(string.Empty, false, true, finalCachedTokens);
    }

    public async Task<GeminiCacheHandle?> EnsureSessionCacheAsync(
        string model,
        string systemInstruction,
        IReadOnlyList<GeminiTurn> history,
        CancellationToken ct)
    {
        if (history.Count == 0)
            return null;

        // Rough token estimate: skip if below threshold
        var estimatedTokens = history.Sum(t => t.Text.Length / 4) + systemInstruction.Length / 4;
        if (estimatedTokens < options.Value.SessionCacheMinTokens)
            return null;

        try
        {
            var ttl = Math.Max(options.Value.SessionCacheTtlSeconds, 60);
            var body = new JObject
            {
                ["model"] = $"models/{model}",
                ["ttl"] = $"{ttl}s",
                ["contents"] = BuildTurnsArray(history),
            };

            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                body["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray { new JObject { ["text"] = systemInstruction } }
                };
            }

            var url = $"cachedContents?key={options.Value.ApiKey}";
            var httpContent = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, httpContent, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var root = JObject.Parse(json);
            var name = root.Value<string>("name");
            var expireTimeRaw = root.Value<string>("expireTime");

            if (string.IsNullOrWhiteSpace(name))
                return null;

            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(ttl);
            if (!string.IsNullOrWhiteSpace(expireTimeRaw) &&
                DateTimeOffset.TryParse(expireTimeRaw, out var parsed))
            {
                expiresAt = parsed;
            }

            return new GeminiCacheHandle(name, expiresAt);
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteCacheAsync(string name, CancellationToken ct)
    {
        // Extract just the last segment if a full path is given
        var segment = name.Contains('/') ? name[(name.LastIndexOf('/') + 1)..] : name;
        var url = $"cachedContents/{segment}?key={options.Value.ApiKey}";

        try
        {
            var response = await httpClient.DeleteAsync(url, ct);
            // Ignore 404 — already expired
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            // best effort
        }
    }

    private async Task<string?> ResolveCachedContentNameAsync(GeminiGenerationRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.CachedContentName))
            return request.CachedContentName;

        if (!request.UseSystemCache)
            return null;

        var systemInstruction = options.Value.SystemInstruction;
        if (string.IsNullOrWhiteSpace(systemInstruction))
            return null;

        // Try memory cache first
        if (cache.TryGetValue(SystemCacheKey, out GeminiCacheHandle? handle) && handle is not null)
        {
            if (handle.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
                return handle.Name;
        }

        // Attempt to create / refresh system cache
        try
        {
            var ttl = Math.Max(options.Value.SystemCacheTtlSeconds, 60);
            var body = new JObject
            {
                ["model"] = $"models/{request.Model}",
                ["ttl"] = $"{ttl}s",
                ["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray { new JObject { ["text"] = systemInstruction } }
                },
                // Gemini requires at least one content turn in a cached content object
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray { new JObject { ["text"] = "." } }
                    }
                }
            };

            var url = $"cachedContents?key={options.Value.ApiKey}";
            var httpContent = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, httpContent, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var root = JObject.Parse(json);
            var name = root.Value<string>("name");
            var expireTimeRaw = root.Value<string>("expireTime");

            if (string.IsNullOrWhiteSpace(name))
                return null;

            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(ttl);
            if (!string.IsNullOrWhiteSpace(expireTimeRaw) &&
                DateTimeOffset.TryParse(expireTimeRaw, out var parsed))
            {
                expiresAt = parsed;
            }

            var newHandle = new GeminiCacheHandle(name, expiresAt);
            cache.Set(SystemCacheKey, newHandle, expiresAt.AddMinutes(-2));
            return newHandle.Name;
        }
        catch
        {
            return null;
        }
    }

    private JObject BuildGenerateBody(GeminiGenerationRequest request, string? cachedContentName)
    {
        var body = new JObject();

        if (!string.IsNullOrWhiteSpace(cachedContentName))
        {
            body["cachedContent"] = cachedContentName;
        }

        body["contents"] = BuildTurnsArray(request.Contents);

        if (!string.IsNullOrWhiteSpace(request.SystemInstruction) && string.IsNullOrWhiteSpace(cachedContentName))
        {
            body["systemInstruction"] = new JObject
            {
                ["parts"] = new JArray { new JObject { ["text"] = request.SystemInstruction } }
            };
        }

        var config = new JObject
        {
            ["temperature"] = request.Temperature,
        };

        // Thinking configuration
        switch (request.Thinking.Mode)
        {
            case "budget":
                var budget = request.Thinking.Budget ?? 0;
                config["thinkingConfig"] = new JObject
                {
                    ["thinkingBudget"] = budget,
                    ["includeThoughts"] = request.IncludeThoughts && budget > 0
                };
                break;

            case "level":
                config["thinkingConfig"] = new JObject
                {
                    ["thinkingLevel"] = (request.Thinking.Level ?? "high").ToLowerInvariant(),
                    ["includeThoughts"] = request.IncludeThoughts
                };
                break;

            default:
                // none — no thinkingConfig
                break;
        }

        body["generationConfig"] = config;

        return body;
    }

    private static JArray BuildTurnsArray(IReadOnlyList<GeminiTurn> turns)
    {
        var array = new JArray();
        foreach (var turn in turns)
        {
            array.Add(new JObject
            {
                ["role"] = turn.Role,
                ["parts"] = new JArray { new JObject { ["text"] = turn.Text } }
            });
        }

        return array;
    }

    private static string ExtractText(JObject root)
    {
        var sb = new StringBuilder();
        var candidates = root["candidates"] as JArray;
        if (candidates is null)
            return sb.ToString();

        foreach (var candidate in candidates.OfType<JObject>())
        {
            var content = candidate["content"] as JObject;
            var parts = content?["parts"] as JArray;
            if (parts is null)
                continue;

            foreach (var part in parts.OfType<JObject>())
            {
                var thought = part.Value<bool?>("thought") ?? false;
                if (!thought)
                    sb.Append(part.Value<string>("text") ?? string.Empty);
            }
        }

        return sb.ToString();
    }
}
