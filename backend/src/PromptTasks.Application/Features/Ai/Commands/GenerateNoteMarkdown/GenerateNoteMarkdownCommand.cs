using MediatR;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.Features.Ai.Commands.GenerateNoteMarkdown;

public sealed record GenerateNoteMarkdownCommand(
    string Instruction,
    string? Format,
    string Model,
    double Temperature,
    GeminiThinking Thinking,
    Guid? NotebookId,
    string? CurrentContent) : IRequest<GeneratedNoteDto>;
