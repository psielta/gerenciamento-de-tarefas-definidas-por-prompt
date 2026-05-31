using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Infrastructure.AgentUsage;

namespace PromptTasks.Infrastructure.UnitTests;

public sealed class AgentUsageReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"prompttasks-agent-usage-{Guid.NewGuid():N}");
    private readonly FakeDateTimeProvider _clock = new(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));

    public AgentUsageReaderTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Claude_reader_maps_success_response_and_sends_required_headers()
    {
        var token = "secret-token";
        var credentialsPath = WriteClaudeCredentials(token, "max");
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "five_hour": { "utilization": 12.5, "resets_at": "2026-05-31T15:00:00Z" },
                  "seven_day": { "utilization": 34, "resets_at": "2026-06-07T12:00:00Z" },
                  "seven_day_opus": { "utilization": 56, "resets_at": "2026-06-06T12:00:00Z" }
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
        var reader = CreateClaudeReader(handler, credentialsPath);

        var result = await reader.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(AgentUsageStatus.Ok);
        result.Plan.Should().Be("max");
        result.Windows.Should().Contain(window => window.Key == "five_hour" && window.UsedPercent == 12.5);
        result.Windows.Should().Contain(window => window.Key == "seven_day");
        result.Windows.Should().Contain(window => window.Key == "seven_day_opus");

        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be(token);
        request.Headers.Accept.Should().Contain(header => header.MediaType == "application/json");
        request.Headers.UserAgent.ToString().Should().Be("claude-code/test");
        request.Headers.GetValues("anthropic-beta").Should().ContainSingle("oauth-2025-04-20");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AgentUsageStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AgentUsageStatus.Unauthorized)]
    [InlineData((HttpStatusCode)429, AgentUsageStatus.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, AgentUsageStatus.HttpError)]
    public async Task Claude_reader_maps_http_errors(HttpStatusCode statusCode, AgentUsageStatus expected)
    {
        var credentialsPath = WriteClaudeCredentials("secret-token", "max");
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("upstream failure")
        });
        var reader = CreateClaudeReader(handler, credentialsPath);

        var result = await reader.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(expected);
        result.HttpStatusCode.Should().Be((int)statusCode);
        result.StatusDetail.Should().Contain(((int)statusCode).ToString());
    }

    [Fact]
    public async Task Claude_reader_does_not_call_api_without_token()
    {
        var credentialsPath = Path.Combine(_root, "missing-credentials.json");
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Should not call API"));
        var reader = CreateClaudeReader(handler, credentialsPath);

        var result = await reader.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(AgentUsageStatus.NoToken);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Claude_reader_redacts_token_from_status_detail_and_serialized_dto()
    {
        var token = "secret-token";
        var credentialsPath = WriteClaudeCredentials(token, "max");
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent($"failure contains {token}")
        });
        var reader = CreateClaudeReader(handler, credentialsPath);

        var result = await reader.ReadAsync(CancellationToken.None);
        var serialized = JsonConvert.SerializeObject(result);

        result.Status.Should().Be(AgentUsageStatus.HttpError);
        result.StatusDetail.Should().NotContain(token);
        serialized.Should().NotContain(token);
    }

    [Fact]
    public async Task Claude_reader_maps_network_and_timeout_failures()
    {
        var credentialsPath = WriteClaudeCredentials("secret-token", "max");
        var networkReader = CreateClaudeReader(
            new FakeHttpMessageHandler(_ => throw new HttpRequestException("network down")),
            credentialsPath);
        var timeoutReader = CreateClaudeReader(
            new FakeHttpMessageHandler(_ => throw new TaskCanceledException("timeout")),
            credentialsPath);

        (await networkReader.ReadAsync(CancellationToken.None)).Status.Should().Be(AgentUsageStatus.NetworkError);
        (await timeoutReader.ReadAsync(CancellationToken.None)).Status.Should().Be(AgentUsageStatus.Timeout);
    }

    [Fact]
    public async Task Codex_reader_maps_latest_active_rate_limits()
    {
        var sessionsDir = Path.Combine(_root, ".codex", "sessions", "2026", "05", "31");
        Directory.CreateDirectory(sessionsDir);
        var file = Path.Combine(sessionsDir, "rollout-active.jsonl");
        await File.WriteAllTextAsync(
            file,
            CreateCodexRateLimitLine(
                42,
                _clock.UtcNow.AddHours(2).ToUnixTimeSeconds(),
                18,
                _clock.UtcNow.AddDays(3).ToUnixTimeSeconds()));

        var reader = CreateCodexReader(sessionsDir);

        var result = await reader.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(AgentUsageStatus.Ok);
        result.Plan.Should().Be("pro");
        result.Windows.Should().Contain(window =>
            window.Key == "primary" &&
            window.UsedPercent == 42 &&
            window.WindowMinutes == 300);
        result.Windows.Should().Contain(window => window.Key == "secondary" && window.UsedPercent == 18);
    }

    [Fact]
    public async Task Codex_reader_discards_expired_rate_limits()
    {
        var sessionsDir = Path.Combine(_root, ".codex", "sessions", "2026", "05", "31");
        Directory.CreateDirectory(sessionsDir);
        var file = Path.Combine(sessionsDir, "rollout-expired.jsonl");
        await File.WriteAllTextAsync(
            file,
            CreateCodexRateLimitLine(
                99,
                _clock.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
                98,
                _clock.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()));

        var reader = CreateCodexReader(sessionsDir);

        var result = await reader.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(AgentUsageStatus.NoData);
        result.Windows.Should().BeEmpty();
    }

    [Fact]
    public async Task Codex_reader_returns_no_data_without_rate_limits()
    {
        var sessionsDir = Path.Combine(_root, ".codex", "sessions", "2026", "05", "31");
        Directory.CreateDirectory(sessionsDir);
        await File.WriteAllTextAsync(
            Path.Combine(sessionsDir, "rollout-null.jsonl"),
            """{"type":"event_msg","payload":{"type":"token_count","rate_limits":null}}""");

        var reader = CreateCodexReader(sessionsDir);

        var result = await reader.ReadAsync(CancellationToken.None);

        result.Status.Should().Be(AgentUsageStatus.NoData);
    }

    [Fact]
    public async Task Codex_reader_prefers_most_recent_file()
    {
        var sessionsDir = Path.Combine(_root, ".codex", "sessions", "2026", "05", "31");
        Directory.CreateDirectory(sessionsDir);
        var older = Path.Combine(sessionsDir, "rollout-older.jsonl");
        var newer = Path.Combine(sessionsDir, "rollout-newer.jsonl");
        await File.WriteAllTextAsync(
            older,
            CreateCodexRateLimitLine(10, _clock.UtcNow.AddHours(2).ToUnixTimeSeconds()));
        await File.WriteAllTextAsync(
            newer,
            CreateCodexRateLimitLine(70, _clock.UtcNow.AddHours(2).ToUnixTimeSeconds()));
        File.SetLastWriteTimeUtc(older, _clock.UtcNow.AddMinutes(-5).UtcDateTime);
        File.SetLastWriteTimeUtc(newer, _clock.UtcNow.UtcDateTime);

        var reader = CreateCodexReader(sessionsDir);

        var result = await reader.ReadAsync(CancellationToken.None);

        result.Windows.Should().ContainSingle(window => window.Key == "primary" && window.UsedPercent == 70);
    }

    private string WriteClaudeCredentials(string token, string plan)
    {
        var path = Path.Combine(_root, ".claude", ".credentials.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            $$"""
            {
              "claudeAiOauth": {
                "accessToken": "{{token}}",
                "subscriptionType": "{{plan}}",
                "rateLimitTier": "default_claude_max_20x"
              }
            }
            """);
        return path;
    }

    private static string CreateCodexRateLimitLine(
        double primaryUsedPercent,
        long primaryResetsAt,
        double? secondaryUsedPercent = null,
        long? secondaryResetsAt = null)
    {
        var rateLimits = new Dictionary<string, object?>
        {
            ["limit_id"] = "codex",
            ["plan_type"] = "pro",
            ["primary"] = new
            {
                used_percent = primaryUsedPercent,
                window_minutes = 300,
                resets_at = primaryResetsAt
            }
        };

        if (secondaryUsedPercent.HasValue && secondaryResetsAt.HasValue)
        {
            rateLimits["secondary"] = new
            {
                used_percent = secondaryUsedPercent.Value,
                window_minutes = 10080,
                resets_at = secondaryResetsAt.Value
            };
        }

        return JsonConvert.SerializeObject(new
        {
            type = "event_msg",
            payload = new
            {
                type = "token_count",
                rate_limits = rateLimits
            }
        });
    }

    private ClaudeUsageReader CreateClaudeReader(FakeHttpMessageHandler handler, string credentialsPath)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AgentUsageOptions
        {
            Claude =
            {
                CredentialsPath = credentialsPath,
                UsageEndpoint = "https://example.test/api/oauth/usage",
                UserAgent = "claude-code/test",
                CacheTtlSeconds = 1
            }
        });

        return new ClaudeUsageReader(httpClient, options, CreateCache(), _clock);
    }

    private CodexUsageReader CreateCodexReader(string sessionsDir)
    {
        var options = Options.Create(new AgentUsageOptions
        {
            Codex =
            {
                SessionsDir = sessionsDir,
                CacheTtlSeconds = 1,
                MaxFilesToScan = 5
            }
        });

        return new CodexUsageReader(options, CreateCache(), _clock);
    }

    private static MemoryCache CreateCache() => new(new MemoryCacheOptions());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequest(request));
            return Task.FromResult(handle(request));
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }

    private sealed class FakeDateTimeProvider(DateTimeOffset utcNow) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
