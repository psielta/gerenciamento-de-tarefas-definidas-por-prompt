using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.PromptTemplates.Queries.GetPromptTemplates;

public sealed class GetPromptTemplatesHandler(IPromptTemplateCatalog catalog)
    : IRequestHandler<GetPromptTemplatesQuery, IReadOnlyList<PromptTemplateDto>>
{
    public Task<IReadOnlyList<PromptTemplateDto>> Handle(
        GetPromptTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = catalog
            .GetAll()
            .Select(template =>
            {
                var input = template.Input is null ? null : ToDto(template.Input);
                var inputs = template.Inputs.Select(ToDto).ToList();

                return new PromptTemplateDto(
                    template.Key,
                    template.DisplayName,
                    template.Description,
                    template.DefaultTargetAgent,
                    template.DefaultKind,
                    input,
                    inputs);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<PromptTemplateDto>>(templates);
    }

    private static PromptTemplateInputDto ToDto(PromptTemplateInputDefinition input) =>
        new(
            input.Key,
            input.Label,
            input.Placeholder,
            input.HelpText,
            input.Required,
            input.Multiline);
}
