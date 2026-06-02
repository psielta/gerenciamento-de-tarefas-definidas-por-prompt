using MediatR;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.Features.Ai.Commands.TranslatePrompt;

public sealed record TranslatePromptCommand(
    string Content,
    string Model,
    double Temperature,
    GeminiThinking Thinking) : IRequest<RefinedPromptDto>;
