using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.Features.Ai.Commands.GenerateNoteMarkdown;

public sealed class GenerateNoteMarkdownHandler(
    IGeminiClient gemini,
    IGeminiModelCatalog catalog,
    IApplicationDbContext context,
    IWorkspaceFileService workspaceFiles,
    ICurrentUser currentUser)
    : IRequestHandler<GenerateNoteMarkdownCommand, GeneratedNoteDto>
{
    private const string NoteSystemInstruction =
        "Você é um assistente que escreve notas em Markdown limpo e bem estruturado " +
        "para um editor de notas. " +
        "Responda APENAS com o conteúdo da nota em Markdown (use títulos, listas, " +
        "negrito, tabelas e code blocks quando ajudar). " +
        "NÃO envolva todo o conteúdo em cercas de código. " +
        "Você PODE começar com um único título de nível 1 (`# Título`) sugerindo o nome da nota. " +
        "Não adicione comentários ou explicações fora da nota.";

    public async Task<GeneratedNoteDto> Handle(GenerateNoteMarkdownCommand request, CancellationToken cancellationToken)
    {
        if (catalog.GetModel(request.Model) is null)
            throw new NotFoundException($"Modelo '{request.Model}' não encontrado.");

        var instructionBlocks = new List<string> { NoteSystemInstruction };

        if (NoteFormats.Instructions.TryGetValue(request.Format ?? string.Empty, out var formatInstruction))
        {
            instructionBlocks.Add("## Formato solicitado\n\n" + formatInstruction);
        }

        if (request.NotebookId is { } notebookId)
        {
            var notebook = context.Notebooks
                .FirstOrDefault(n => n.Id == notebookId && n.OwnerId == currentUser.UserId)
                ?? throw new NotFoundException($"Bloco de notas '{notebookId}' não encontrado.");

            if (notebook.WorkingDirectoryId is { } workspaceId)
            {
                var workspace = context.WorkingDirectories
                    .FirstOrDefault(directory => directory.Id == workspaceId && directory.OwnerId == currentUser.UserId);

                if (workspace is { EnableAiContext: true })
                {
                    var workspaceContext = await workspaceFiles.ReadWorkspaceContextAsync(workspace.AbsolutePath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(workspaceContext))
                    {
                        instructionBlocks.Add(workspaceContext);
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentContent))
        {
            instructionBlocks.Add(
                "## Conteúdo atual da nota (referência)\n\n"
              + "Use como contexto se a instrução pedir para complementar ou transformar:\n\n"
              + request.CurrentContent.Trim());
        }

        var systemInstruction = string.Join("\n\n", instructionBlocks);

        var geminiRequest = new GeminiGenerationRequest(
            Model: request.Model,
            Temperature: request.Temperature,
            Thinking: request.Thinking,
            IncludeThoughts: false,
            UseSystemCache: false,
            CachedContentName: null,
            SystemInstruction: systemInstruction,
            Contents: new[] { new GeminiTurn("user", request.Instruction) });

        var result = await gemini.RefineAsync(geminiRequest, cancellationToken);

        var (suggestedTitle, contentMarkdown) = SplitTitle(result.Text);
        return new GeneratedNoteDto(suggestedTitle, contentMarkdown, result.PromptTokens, result.CandidateTokens);
    }

    /// <summary>
    /// Pulls a leading single-level (`# `) heading out of the generated Markdown so
    /// the editor can pre-fill the separate note title field. Everything else stays
    /// in the body. When there is no leading H1 the title is left null.
    /// </summary>
    private static (string? Title, string Content) SplitTitle(string markdown)
    {
        var text = (markdown ?? string.Empty).Replace("\r\n", "\n").TrimStart('\n');
        var newlineIndex = text.IndexOf('\n');
        var firstLine = newlineIndex >= 0 ? text[..newlineIndex] : text;

        if (firstLine.StartsWith("# ", StringComparison.Ordinal))
        {
            var title = firstLine[2..].Trim();
            var body = newlineIndex >= 0 ? text[(newlineIndex + 1)..].TrimStart('\n') : string.Empty;
            if (!string.IsNullOrWhiteSpace(title))
            {
                return (title, body.Trim());
            }
        }

        return (null, (markdown ?? string.Empty).Trim());
    }
}
