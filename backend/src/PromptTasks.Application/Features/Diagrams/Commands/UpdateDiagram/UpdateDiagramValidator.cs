using FluentValidation;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Application.Features.Diagrams.Commands.UpdateDiagram;

public sealed class UpdateDiagramValidator : AbstractValidator<UpdateDiagramCommand>
{
    public UpdateDiagramValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Diagram.MaxTitleLength);
        RuleFor(command => command.Description).MaximumLength(Diagram.MaxDescriptionLength);
        RuleFor(command => command.Content).NotNull().MaximumLength(Diagram.MaxContentLength);
        RuleFor(command => command.MetadataJson).MaximumLength(Diagram.MaxMetadataLength);
    }
}
