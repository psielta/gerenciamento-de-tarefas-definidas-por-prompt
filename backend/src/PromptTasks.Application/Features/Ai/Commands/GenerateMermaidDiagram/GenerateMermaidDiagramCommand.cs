using MediatR;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;

namespace PromptTasks.Application.Features.Ai.Commands.GenerateMermaidDiagram;

public sealed record GenerateMermaidDiagramCommand(
    string Instruction,
    string? DiagramKind,
    string Model,
    double Temperature,
    GeminiThinking Thinking,
    Guid? WorkingDirectoryId,
    Guid? DiagramId,
    string? CurrentCode) : IRequest<GeneratedMermaidDto>;
