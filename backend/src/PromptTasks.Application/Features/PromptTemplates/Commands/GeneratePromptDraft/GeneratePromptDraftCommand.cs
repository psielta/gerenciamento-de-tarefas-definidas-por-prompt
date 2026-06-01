using MediatR;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Commands.GeneratePromptDraft;

public sealed record GeneratePromptDraftCommand(
    Guid LinkedDocumentId,
    PromptTemplateKey TemplateKey,
    string? PullRequest = null)
    : IRequest<GeneratedPromptDraftDto>;
