using MediatR;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.Features.Ai.Commands.RefinePrompt;

public sealed record RefinePromptCommand(
    string Content,
    string Model,
    double Temperature,
    GeminiThinking Thinking,
    Guid? WorkingDirectoryId) : IRequest<RefinedPromptDto>;
