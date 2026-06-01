using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReviewPullRequestTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReviewPullRequest;
    public string DisplayName => "Revisar PR";
    public string Description => "Gera um prompt de revisao para a PR que implementou o plano.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public PromptTemplateInputDefinition? Input => new(
        "pullRequest",
        "PR",
        "#123 ou URL da PR",
        "Informe o numero ou link da PR criada apos a implementacao do plano.");

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken)
    {
        var pullRequestReference = FormatPullRequestReference(context.PullRequestReference);

        return Task.FromResult(new RenderedPromptTemplate(
            $"Revisar {pullRequestReference}: {context.DisplayName}",
            $"""
            Revise a {pullRequestReference} que implementa o plano `{context.AbsolutePath}`.

            Use o plano como fonte de verdade. Verifique se a PR implementa o plano por completo, se preserva a arquitetura existente, se nao introduz regressao e se as validacoes necessarias foram feitas.

            Priorize bugs, riscos comportamentais e testes faltantes. Traga os achados com severidade e referencias concretas de arquivos/linhas quando possivel.
            """));
    }

    private static string FormatPullRequestReference(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.StartsWith('#') ||
            normalized.StartsWith("PR ", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return $"PR #{normalized}";
    }
}
