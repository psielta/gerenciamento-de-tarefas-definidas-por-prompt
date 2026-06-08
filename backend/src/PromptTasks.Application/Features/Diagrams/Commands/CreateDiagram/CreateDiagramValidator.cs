using FluentValidation;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Application.Features.Diagrams.Commands.CreateDiagram;

public sealed class CreateDiagramValidator : AbstractValidator<CreateDiagramCommand>
{
    public CreateDiagramValidator()
    {
        RuleFor(command => command.WorkingDirectoryId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Diagram.MaxTitleLength);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Description).MaximumLength(Diagram.MaxDescriptionLength);
        RuleFor(command => command.Content).MaximumLength(Diagram.MaxContentLength);
        RuleFor(command => command.MetadataJson).MaximumLength(Diagram.MaxMetadataLength);
    }
}
