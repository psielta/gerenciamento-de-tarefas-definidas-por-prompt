using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.GenerateMermaidDiagram;

namespace PromptTasks.Application.UnitTests;

public sealed class GenerateMermaidDiagramHandlerTests
{
    private static GenerateMermaidDiagramHandler CreateHandler(
        RecordingGeminiClient gemini,
        InMemoryAiDbContext context,
        StubWorkspaceFileService? workspaceFiles = null,
        bool includeModel = true) =>
        new(
            gemini,
            new StubModelCatalog(includeModel),
            context,
            workspaceFiles ?? new StubWorkspaceFileService(),
            new StubCurrentUser());

    private static GenerateMermaidDiagramCommand Command(
        string instruction = "Fluxo de login",
        string? diagramKind = null,
        Guid? workingDirectoryId = null,
        Guid? diagramId = null,
        string? currentCode = null) =>
        new(
            instruction,
            diagramKind,
            StubModelCatalog.ModelId,
            0.4,
            new GeminiThinking("none", null, null),
            workingDirectoryId,
            diagramId,
            currentCode);

    [Fact]
    public async Task Generate_sends_mermaid_only_instruction_and_maps_result()
    {
        var gemini = new RecordingGeminiClient { ResponseText = "flowchart TD\n  A --> B" };
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.MermaidCode.Should().Be("flowchart TD\n  A --> B");
        result.PromptTokens.Should().Be(11);
        result.CandidateTokens.Should().Be(7);
        result.Warnings.Should().BeEmpty();

        var instruction = gemini.LastRefineRequest!.SystemInstruction;
        instruction.Should().Contain("APENAS com código Mermaid válido");
        instruction.Should().Contain("NÃO use cercas de código Markdown");
    }

    [Fact]
    public async Task Generate_strips_code_fences_and_warns()
    {
        var gemini = new RecordingGeminiClient { ResponseText = "```mermaid\nflowchart TD\n  A --> B\n```" };
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.MermaidCode.Should().Be("flowchart TD\n  A --> B");
        result.Warnings.Should().ContainSingle().Which.Should().Contain("cercas de código");
    }

    [Fact]
    public async Task Generate_includes_diagram_kind_hint()
    {
        var gemini = new RecordingGeminiClient();
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        await handler.Handle(Command(diagramKind: "sequence"), CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("sequenceDiagram");
    }

    [Fact]
    public async Task Generate_includes_current_code_reference_when_provided()
    {
        var gemini = new RecordingGeminiClient();
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        await handler.Handle(Command(currentCode: "flowchart TD\n  X --> Y"), CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("Código Mermaid atual");
        gemini.LastRefineRequest.SystemInstruction.Should().Contain("X --> Y");
    }

    [Fact]
    public async Task Generate_injects_workspace_context_from_owned_diagram()
    {
        var context = new InMemoryAiDbContext();
        var workspace = context.SeedWorkspace(enableAiContext: true);
        var diagram = context.SeedDiagram(workspace.Id);
        var gemini = new RecordingGeminiClient();
        var workspaceFiles = new StubWorkspaceFileService { Context = "workspace context" };
        var handler = CreateHandler(gemini, context, workspaceFiles);

        await handler.Handle(Command(diagramId: diagram.Id), CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("workspace context");
        workspaceFiles.ReadCount.Should().Be(1);
    }

    [Fact]
    public async Task Generate_rejects_diagram_owned_by_other_user()
    {
        var context = new InMemoryAiDbContext();
        var workspace = context.SeedWorkspace(enableAiContext: true);
        var diagram = context.SeedDiagram(workspace.Id, ownerId: Guid.CreateVersion7());
        var handler = CreateHandler(new RecordingGeminiClient(), context);

        var act = () => handler.Handle(Command(diagramId: diagram.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Generate_rejects_unknown_model()
    {
        var handler = CreateHandler(new RecordingGeminiClient(), new InMemoryAiDbContext(), includeModel: false);

        var act = () => handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
