using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Prompts.Queries.GetPromptByTaskNumber;

public sealed record GetPromptByTaskNumberQuery(Guid WorkingDirectoryId, string TaskNumber) : IRequest<PromptDto>;
