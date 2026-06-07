using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Queries.GetNotebooks;

public sealed record GetNotebooksQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<NotebookDto>>;
