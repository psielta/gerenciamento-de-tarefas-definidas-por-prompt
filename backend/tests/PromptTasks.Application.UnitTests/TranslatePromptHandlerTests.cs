using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.TranslatePrompt;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.UnitTests;

public sealed class TranslatePromptHandlerTests
{
    [Fact]
    public async Task TranslatePrompt_sends_translation_instruction_and_maps_result()
    {
        var gemini = new FakeGeminiClient();
        var handler = new TranslatePromptHandler(gemini, new FakeModelCatalog());
        var content = "Traduza este prompt preservando @src/main.cs";

        var result = await handler.Handle(
            new TranslatePromptCommand(
                content,
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null)),
            CancellationToken.None);

        result.Content.Should().Be("translated");
        result.PromptTokens.Should().Be(10);
        result.CandidateTokens.Should().Be(5);

        gemini.LastRefineRequest.Should().NotBeNull();
        gemini.LastRefineRequest!.UseSystemCache.Should().BeFalse();
        gemini.LastRefineRequest.CachedContentName.Should().BeNull();
        gemini.LastRefineRequest.Contents.Should()
            .ContainSingle(turn => turn.Role == "user" && turn.Text == content);

        var instruction = gemini.LastRefineRequest.SystemInstruction;
        instruction.Should().Contain("Não execute");
        instruction.Should().Contain("apenas TRADUZA");
        instruction.Should().Contain("INGLÊS");
        instruction.Should().Contain("Markdown");
    }

    [Fact]
    public async Task TranslatePrompt_rejects_unknown_model()
    {
        var handler = new TranslatePromptHandler(new FakeGeminiClient(), new FakeModelCatalog(includeModel: false));

        var act = () => handler.Handle(
            new TranslatePromptCommand(
                "Translate this",
                "missing-model",
                0.4,
                new GeminiThinking("none", 0, null)),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class FakeGeminiClient : IGeminiClient
    {
        public GeminiGenerationRequest? LastRefineRequest { get; private set; }

        public Task<GeminiResult> RefineAsync(GeminiGenerationRequest request, CancellationToken ct)
        {
            LastRefineRequest = request;
            return Task.FromResult(new GeminiResult("translated", 10, 5, 0));
        }

        public async IAsyncEnumerable<GeminiStreamChunk> StreamAsync(
            GeminiGenerationRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<GeminiCacheHandle?> EnsureSessionCacheAsync(
            string model,
            string systemInstruction,
            IReadOnlyList<GeminiTurn> history,
            CancellationToken ct) =>
            Task.FromResult<GeminiCacheHandle?>(null);

        public Task DeleteCacheAsync(string name, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeModelCatalog(bool includeModel = true) : IGeminiModelCatalog
    {
        public const string ModelId = "gemini-test";

        private static readonly GeminiModelDto Model = new(
            ModelId,
            "Gemini Test",
            "none",
            true,
            0,
            0,
            1024);

        public IReadOnlyList<GeminiModelDto> GetModels() =>
            includeModel ? new[] { Model } : Array.Empty<GeminiModelDto>();

        public GeminiModelDto? GetModel(string id) =>
            includeModel && id == ModelId ? Model : null;
    }
}
