using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates;

public sealed class PromptTemplateCatalog : IPromptTemplateCatalog
{
    private readonly IReadOnlyList<IPromptTemplateDefinition> _templates;
    private readonly Dictionary<PromptTemplateKey, IPromptTemplateDefinition> _templatesByKey;

    public PromptTemplateCatalog(IEnumerable<IPromptTemplateDefinition> templates)
    {
        _templates = templates
            .OrderBy(template => GetDisplayOrder(template.Key))
            .ThenBy(template => template.Key)
            .ToList();
        _templatesByKey = new Dictionary<PromptTemplateKey, IPromptTemplateDefinition>();

        foreach (var template in _templates)
        {
            if (!_templatesByKey.TryAdd(template.Key, template))
            {
                throw new InvalidOperationException($"Duplicate prompt template key '{template.Key}'.");
            }
        }
    }

    public IReadOnlyList<IPromptTemplateDefinition> GetAll() => _templates;

    public IPromptTemplateDefinition Get(PromptTemplateKey key) =>
        _templatesByKey.TryGetValue(key, out var template)
            ? template
            : throw new NotFoundException("Prompt template was not found.");

    private static int GetDisplayOrder(PromptTemplateKey key) =>
        key switch
        {
            PromptTemplateKey.ReviewPlan => 10,
            PromptTemplateKey.ImplementPlan => 20,
            PromptTemplateKey.ReviewPlanWithParentPrompt => 30,
            PromptTemplateKey.ReReviewPlan => 40,
            PromptTemplateKey.ImplementPlanInWorktree => 50,
            PromptTemplateKey.ReviewPullRequest => 60,
            PromptTemplateKey.ReReviewPullRequest => 61,
            PromptTemplateKey.RebaseCurrentBranch => 65,
            PromptTemplateKey.MergePullRequest => 70,
            _ => 1_000 + (int)key
        };
}
