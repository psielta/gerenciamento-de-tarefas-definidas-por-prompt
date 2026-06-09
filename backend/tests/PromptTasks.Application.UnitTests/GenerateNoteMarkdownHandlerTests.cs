using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.GenerateNoteMarkdown;
using PromptTasks.Domain.Users;

namespace PromptTasks.Application.UnitTests;

public sealed class GenerateNoteMarkdownHandlerTests
{
    private static GenerateNoteMarkdownHandler CreateHandler(
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

    private static GenerateNoteMarkdownCommand Command(
        string instruction = "Crie uma nota de arquitetura",
        string? format = null,
        Guid? notebookId = null,
        string? currentContent = null) =>
        new(
            instruction,
            format,
            StubModelCatalog.ModelId,
            0.4,
            new GeminiThinking("none", null, null),
            notebookId,
            currentContent);

    [Fact]
    public async Task Generate_sends_note_instruction_and_parses_suggested_title()
    {
        var gemini = new RecordingGeminiClient { ResponseText = "# Arquitetura de Auth\n\nCorpo da nota em Markdown." };
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.SuggestedTitle.Should().Be("Arquitetura de Auth");
        result.ContentMarkdown.Should().Be("Corpo da nota em Markdown.");
        result.PromptTokens.Should().Be(11);
        result.CandidateTokens.Should().Be(7);

        var instruction = gemini.LastRefineRequest!.SystemInstruction;
        instruction.Should().Contain("Markdown");
        instruction.Should().Contain("NÃO envolva todo o conteúdo em cercas de código");
        gemini.LastRefineRequest.Contents.Should()
            .ContainSingle(turn => turn.Role == "user" && turn.Text == "Crie uma nota de arquitetura");
    }

    [Fact]
    public async Task Generate_returns_null_title_when_no_leading_h1()
    {
        var gemini = new RecordingGeminiClient { ResponseText = "Apenas corpo, sem titulo." };
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.SuggestedTitle.Should().BeNull();
        result.ContentMarkdown.Should().Be("Apenas corpo, sem titulo.");
    }

    [Fact]
    public async Task Generate_includes_format_block_when_provided()
    {
        var gemini = new RecordingGeminiClient();
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        await handler.Handle(Command(format: "adr"), CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("Formato solicitado");
        gemini.LastRefineRequest.SystemInstruction.Should().Contain("ADR");
    }

    [Fact]
    public async Task Generate_injects_workspace_context_when_notebook_workspace_enabled_and_owned()
    {
        var context = new InMemoryAiDbContext();
        var workspace = context.SeedWorkspace(enableAiContext: true);
        var notebook = context.SeedNotebook(workingDirectoryId: workspace.Id);
        var gemini = new RecordingGeminiClient();
        var workspaceFiles = new StubWorkspaceFileService { Context = "workspace context" };
        var handler = CreateHandler(gemini, context, workspaceFiles);

        await handler.Handle(Command(notebookId: notebook.Id), CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("workspace context");
        workspaceFiles.ReadCount.Should().Be(1);
    }

    [Fact]
    public async Task Generate_does_not_inject_workspace_context_when_disabled()
    {
        var context = new InMemoryAiDbContext();
        var workspace = context.SeedWorkspace(enableAiContext: false);
        var notebook = context.SeedNotebook(workingDirectoryId: workspace.Id);
        var gemini = new RecordingGeminiClient();
        var workspaceFiles = new StubWorkspaceFileService { Context = "workspace context" };
        var handler = CreateHandler(gemini, context, workspaceFiles);

        await handler.Handle(Command(notebookId: notebook.Id), CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().NotContain("workspace context");
        workspaceFiles.ReadCount.Should().Be(0);
    }

    [Fact]
    public async Task Generate_includes_current_content_reference_when_provided()
    {
        var gemini = new RecordingGeminiClient();
        var handler = CreateHandler(gemini, new InMemoryAiDbContext());

        await handler.Handle(Command(currentContent: "pontos soltos a transformar"), CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("Conteúdo atual da nota");
        gemini.LastRefineRequest.SystemInstruction.Should().Contain("pontos soltos a transformar");
    }

    [Fact]
    public async Task Generate_rejects_unknown_model()
    {
        var handler = CreateHandler(new RecordingGeminiClient(), new InMemoryAiDbContext(), includeModel: false);

        var act = () => handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Generate_rejects_notebook_owned_by_other_user()
    {
        var context = new InMemoryAiDbContext();
        var notebook = context.SeedNotebook(ownerId: Guid.CreateVersion7());
        var handler = CreateHandler(new RecordingGeminiClient(), context);

        var act = () => handler.Handle(Command(notebookId: notebook.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
