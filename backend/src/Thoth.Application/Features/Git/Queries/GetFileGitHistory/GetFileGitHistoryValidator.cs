using FluentValidation;

namespace Thoth.Application.Features.Git.Queries.GetFileGitHistory;

public sealed class GetFileGitHistoryValidator : AbstractValidator<GetFileGitHistoryQuery>
{
    public GetFileGitHistoryValidator()
    {
        RuleFor(query => query.WorkingDirectoryId).NotEmpty();
        RuleFor(query => query.Path).NotEmpty().MaximumLength(1024);
    }
}