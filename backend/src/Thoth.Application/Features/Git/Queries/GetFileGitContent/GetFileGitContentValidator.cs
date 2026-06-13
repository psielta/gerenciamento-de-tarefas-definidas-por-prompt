using FluentValidation;

namespace Thoth.Application.Features.Git.Queries.GetFileGitContent;

public sealed class GetFileGitContentValidator : AbstractValidator<GetFileGitContentQuery>
{
    public GetFileGitContentValidator()
    {
        RuleFor(query => query.WorkingDirectoryId).NotEmpty();
        RuleFor(query => query.Path).NotEmpty().MaximumLength(1024);
        RuleFor(query => query.Hash).NotEmpty().Matches(@"\A[0-9a-fA-F]{7,40}\z");
    }
}