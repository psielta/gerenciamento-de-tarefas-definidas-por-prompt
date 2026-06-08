using FluentValidation;

namespace PromptTasks.Application.Features.Diagrams.Queries.GetDiagrams;

public sealed class GetDiagramsValidator : AbstractValidator<GetDiagramsQuery>
{
    public GetDiagramsValidator()
    {
        RuleFor(query => query.WorkingDirectoryId).NotEmpty();
    }
}
