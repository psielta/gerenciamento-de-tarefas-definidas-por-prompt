using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.Features.Ai.Commands.RefinePrompt;

public sealed class RefinePromptHandler(
    IGeminiClient gemini,
    IGeminiModelCatalog catalog,
    IApplicationDbContext context,
    IWorkspaceFileService workspaceFiles,
    ICurrentUser currentUser)
    : IRequestHandler<RefinePromptCommand, RefinedPromptDto>
{
    private const string RefineSystemInstruction =
        "Você é um especialista em engenharia de prompts. " +
        "Otimize o prompt do usuário para clareza, completude e eficácia. " +
        "Responda APENAS com o prompt otimizado em Markdown compatível com TipTap " +
        "(use títulos, listas, negrito e code blocks; sem HTML). " +
        "Preserve menções @caminho/arquivo intactas. " +
        "Não adicione explicações, apenas o prompt melhorado.";

    public async Task<RefinedPromptDto> Handle(RefinePromptCommand request, CancellationToken cancellationToken)
    {
        if (catalog.GetModel(request.Model) is null)
            throw new NotFoundException($"Modelo '{request.Model}' não encontrado.");

        var systemInstruction = RefineSystemInstruction;
        if (request.WorkingDirectoryId is { } workspaceId)
        {
            var workspace = context.WorkingDirectories
                .FirstOrDefault(directory => directory.Id == workspaceId && directory.OwnerId == currentUser.UserId);

            if (workspace is { EnableAiContext: true })
            {
                var workspaceContext = await workspaceFiles.ReadWorkspaceContextAsync(workspace.AbsolutePath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(workspaceContext))
                {
                    systemInstruction = $"{RefineSystemInstruction}\n\n{workspaceContext}";
                }
            }
        }

        var geminiRequest = new GeminiGenerationRequest(
            Model: request.Model,
            Temperature: request.Temperature,
            Thinking: request.Thinking,
            IncludeThoughts: false,
            UseSystemCache: false,
            CachedContentName: null,
            SystemInstruction: systemInstruction,
            Contents: new[] { new GeminiTurn("user", request.Content) });

        var result = await gemini.RefineAsync(geminiRequest, cancellationToken);
        return new RefinedPromptDto(result.Text, result.PromptTokens, result.CandidateTokens);
    }
}
