using MediatR;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Application.Features.Diagrams.Queries.GetDiagrams;

public sealed record GetDiagramsQuery(
    Guid? WorkingDirectoryId = null,
    string? Search = null,
    DiagramType? Type = null,
    bool IncludeArchived = false) : IRequest<IReadOnlyList<DiagramSummaryDto>>;
