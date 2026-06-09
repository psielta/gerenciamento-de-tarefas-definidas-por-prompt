using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.Features.Ai.Commands.GenerateMermaidDiagram;

public sealed class GenerateMermaidDiagramHandler(
    IGeminiClient gemini,
    IGeminiModelCatalog catalog,
    IApplicationDbContext context,
    IWorkspaceFileService workspaceFiles,
    ICurrentUser currentUser)
    : IRequestHandler<GenerateMermaidDiagramCommand, GeneratedMermaidDto>
{
    private const string MermaidSystemInstruction =
        "Você é um assistente que gera diagramas Mermaid. " +
        "Responda APENAS com código Mermaid válido que renderize sem erros. " +
        "NÃO use cercas de código Markdown (```), " +
        "NÃO inclua título, explicação ou qualquer texto fora do código Mermaid.";

    public async Task<GeneratedMermaidDto> Handle(GenerateMermaidDiagramCommand request, CancellationToken cancellationToken)
    {
        if (catalog.GetModel(request.Model) is null)
            throw new NotFoundException($"Modelo '{request.Model}' não encontrado.");

        var instructionBlocks = new List<string> { MermaidSystemInstruction };

        if (MermaidDiagramKinds.Hints.TryGetValue(request.DiagramKind ?? string.Empty, out var kindHint))
        {
            instructionBlocks.Add(kindHint);
        }

        // Resolve the workspace either from the existing diagram (validating its
        // ownership) or from the explicit working directory id.
        Guid? workspaceId;
        if (request.DiagramId is { } diagramId)
        {
            var diagram = context.Diagrams
                .FirstOrDefault(d => d.Id == diagramId && d.OwnerId == currentUser.UserId)
                ?? throw new NotFoundException($"Diagrama '{diagramId}' não encontrado.");
            workspaceId = diagram.WorkingDirectoryId;
        }
        else
        {
            workspaceId = request.WorkingDirectoryId;
        }

        if (workspaceId is { } wsId)
        {
            var workspace = context.WorkingDirectories
                .FirstOrDefault(directory => directory.Id == wsId && directory.OwnerId == currentUser.UserId);

            if (workspace is { EnableAiContext: true })
            {
                var workspaceContext = await workspaceFiles.ReadWorkspaceContextAsync(workspace.AbsolutePath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(workspaceContext))
                {
                    instructionBlocks.Add(workspaceContext);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentCode))
        {
            instructionBlocks.Add(
                "## Código Mermaid atual (referência)\n\n"
              + "Use como base se a instrução pedir para ajustar ou estender:\n\n"
              + request.CurrentCode.Trim());
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

        var warnings = new List<string>();
        var (code, stripped) = StripCodeFences(result.Text);
        if (stripped)
        {
            warnings.Add("O modelo retornou cercas de código Markdown que foram removidas.");
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            warnings.Add("O modelo não retornou código Mermaid. Tente reescrever a instrução.");
        }

        return new GeneratedMermaidDto(code, null, result.PromptTokens, result.CandidateTokens, warnings);
    }

    /// <summary>
    /// Defensively removes surrounding Markdown code fences (``` or ```mermaid)
    /// the model may have added despite being told not to.
    /// </summary>
    private static (string Code, bool Stripped) StripCodeFences(string raw)
    {
        var text = (raw ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return (text, false);
        }

        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0)
        {
            return (text.Trim('`').Trim(), true);
        }

        var body = text[(firstNewline + 1)..];
        var closingFence = body.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence >= 0)
        {
            body = body[..closingFence];
        }

        return (body.Trim(), true);
    }
}
