using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Queries.GetNotebook;

public sealed record GetNotebookQuery(Guid Id) : IRequest<NotebookDto>;
