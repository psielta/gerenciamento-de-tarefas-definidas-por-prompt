using PromptTasks.Domain.Ai;
using PromptTasks.Domain.Diagrams;
using PromptTasks.Domain.FutureTasks;
using PromptTasks.Domain.Notebooks;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<WorkingDirectory> WorkingDirectories { get; }
    IQueryable<FutureTask> FutureTasks { get; }
    IQueryable<FutureTaskLabel> FutureTaskLabels { get; }
    IQueryable<Prompt> Prompts { get; }
    IQueryable<PromptVersion> PromptVersions { get; }
    IQueryable<PromptFileReference> PromptFileReferences { get; }
    IQueryable<LinkedDocument> LinkedDocuments { get; }
    IQueryable<LinkedDocumentVersion> LinkedDocumentVersions { get; }
    IQueryable<WorkflowTemplate> WorkflowTemplates { get; }
    IQueryable<WorkflowTemplatePhase> WorkflowTemplatePhases { get; }
    IQueryable<PromptWorkflow> PromptWorkflows { get; }
    IQueryable<PromptWorkflowPhase> PromptWorkflowPhases { get; }
    IQueryable<PromptWorkflowEvent> PromptWorkflowEvents { get; }
    IQueryable<AiChatSession> AiChatSessions { get; }
    IQueryable<AiChatMessage> AiChatMessages { get; }
    IQueryable<AiUserSettings> AiUserSettings { get; }
    IQueryable<Notebook> Notebooks { get; }
    IQueryable<Note> Notes { get; }
    IQueryable<Diagram> Diagrams { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;
    void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
