using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.WorkingDirectories.Commands.CreateWorkingDirectory;

public sealed record CreateWorkingDirectoryCommand(
    string Name,
    string AbsolutePath,
    bool RespectGitignore = true,
    bool EnableAiContext = false,
    string? TaskNumberPattern = null) : IRequest<WorkingDirectoryDto>;
