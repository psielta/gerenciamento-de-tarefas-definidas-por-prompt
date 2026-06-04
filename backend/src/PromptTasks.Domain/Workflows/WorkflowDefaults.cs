namespace PromptTasks.Domain.Workflows;

public sealed record WorkflowPhaseSeed(string Name, WorkflowActor DefaultActor, string Color, WorkflowPhaseRole Role);

public static class WorkflowDefaults
{
    public const string TemplateName = "Fluxo padrão";

    public static IReadOnlyList<WorkflowPhaseSeed> Phases { get; } =
    [
        new("Engenharia de prompt", WorkflowActor.Human, "#9333ea", WorkflowPhaseRole.PromptEngineering),
        new("Planejamento", WorkflowActor.ClaudeCode, "#2563eb", WorkflowPhaseRole.Planning),
        new("Revisão do plano", WorkflowActor.Codex, "#7c3aed", WorkflowPhaseRole.PlanReview),
        new("Correção do plano", WorkflowActor.ClaudeCode, "#d97706", WorkflowPhaseRole.PlanCorrection),
        new("Implementação", WorkflowActor.Codex, "#0d9488", WorkflowPhaseRole.Implementation),
        new("Revisão de código", WorkflowActor.ClaudeCode, "#0891b2", WorkflowPhaseRole.CodeReview),
        new("Correção da revisão", WorkflowActor.Codex, "#dc2626", WorkflowPhaseRole.ReviewCorrection),
        new("Teste prático", WorkflowActor.Human, "#db2777", WorkflowPhaseRole.PracticalTest),
        new("Atualizar branch com main", WorkflowActor.Codex, "#15803d", WorkflowPhaseRole.Rebase),
        new("Commit/Merge", WorkflowActor.Codex, "#16a34a", WorkflowPhaseRole.Merge)
    ];

    public static WorkflowTemplate BuildTemplate(Guid ownerId)
    {
        var template = new WorkflowTemplate
        {
            Name = TemplateName,
            IsDefault = true,
            OwnerId = ownerId
        };

        var orderIndex = 0;
        foreach (var phase in Phases)
        {
            template.Phases.Add(new WorkflowTemplatePhase
            {
                WorkflowTemplateId = template.Id,
                Name = phase.Name,
                DefaultActor = phase.DefaultActor,
                OrderIndex = orderIndex++,
                Color = phase.Color,
                Role = phase.Role
            });
        }

        return template;
    }
}
