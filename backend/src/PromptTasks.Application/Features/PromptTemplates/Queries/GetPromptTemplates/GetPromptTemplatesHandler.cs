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
            .Select(template => new PromptTemplateDto(
                template.Key,
                template.DisplayName,
                template.Description,
                template.DefaultTargetAgent,
                template.DefaultKind,
                template.Input is null
                    ? null
                    : new PromptTemplateInputDto(
                        template.Input.Key,
                        template.Input.Label,
                        template.Input.Placeholder,
                        template.Input.HelpText,
                        template.Input.Required)))
            .ToList();

        return Task.FromResult<IReadOnlyList<PromptTemplateDto>>(templates);
    }
}
