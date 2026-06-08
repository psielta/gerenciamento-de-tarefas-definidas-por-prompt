using Microsoft.EntityFrameworkCore;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Domain.Ai;
using PromptTasks.Domain.Common;
using PromptTasks.Domain.Diagrams;
using PromptTasks.Domain.FutureTasks;
using PromptTasks.Domain.Notebooks;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkingDirectory> WorkingDirectories => Set<WorkingDirectory>();
    public DbSet<FutureTask> FutureTasks => Set<FutureTask>();
    public DbSet<FutureTaskLabel> FutureTaskLabels => Set<FutureTaskLabel>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<DailyTaskSequence> DailyTaskSequences => Set<DailyTaskSequence>();
    public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();
    public DbSet<PromptFileReference> PromptFileReferences => Set<PromptFileReference>();
    public DbSet<LinkedDocument> LinkedDocuments => Set<LinkedDocument>();
    public DbSet<LinkedDocumentVersion> LinkedDocumentVersions => Set<LinkedDocumentVersion>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowTemplatePhase> WorkflowTemplatePhases => Set<WorkflowTemplatePhase>();
    public DbSet<PromptWorkflow> PromptWorkflows => Set<PromptWorkflow>();
    public DbSet<PromptWorkflowPhase> PromptWorkflowPhases => Set<PromptWorkflowPhase>();
    public DbSet<PromptWorkflowEvent> PromptWorkflowEvents => Set<PromptWorkflowEvent>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();
    public DbSet<AiUserSettings> AiUserSettings => Set<AiUserSettings>();
    public DbSet<Notebook> Notebooks => Set<Notebook>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Diagram> Diagrams => Set<Diagram>();

    IQueryable<User> IApplicationDbContext.Users => Users;
    IQueryable<WorkingDirectory> IApplicationDbContext.WorkingDirectories => WorkingDirectories;
    IQueryable<FutureTask> IApplicationDbContext.FutureTasks => FutureTasks;
    IQueryable<FutureTaskLabel> IApplicationDbContext.FutureTaskLabels => FutureTaskLabels;
    IQueryable<Prompt> IApplicationDbContext.Prompts => Prompts;
    IQueryable<PromptVersion> IApplicationDbContext.PromptVersions => PromptVersions;
    IQueryable<PromptFileReference> IApplicationDbContext.PromptFileReferences => PromptFileReferences;
    IQueryable<LinkedDocument> IApplicationDbContext.LinkedDocuments => LinkedDocuments;
    IQueryable<LinkedDocumentVersion> IApplicationDbContext.LinkedDocumentVersions => LinkedDocumentVersions;
    IQueryable<WorkflowTemplate> IApplicationDbContext.WorkflowTemplates => WorkflowTemplates;
    IQueryable<WorkflowTemplatePhase> IApplicationDbContext.WorkflowTemplatePhases => WorkflowTemplatePhases;
    IQueryable<PromptWorkflow> IApplicationDbContext.PromptWorkflows => PromptWorkflows;
    IQueryable<PromptWorkflowPhase> IApplicationDbContext.PromptWorkflowPhases => PromptWorkflowPhases;
    IQueryable<PromptWorkflowEvent> IApplicationDbContext.PromptWorkflowEvents => PromptWorkflowEvents;
    IQueryable<AiChatSession> IApplicationDbContext.AiChatSessions => AiChatSessions;
    IQueryable<AiChatMessage> IApplicationDbContext.AiChatMessages => AiChatMessages;
    IQueryable<AiUserSettings> IApplicationDbContext.AiUserSettings => AiUserSettings;
    IQueryable<Notebook> IApplicationDbContext.Notebooks => Notebooks;
    IQueryable<Note> IApplicationDbContext.Notes => Notes;
    IQueryable<Diagram> IApplicationDbContext.Diagrams => Diagrams;

    void IApplicationDbContext.Add<TEntity>(TEntity entity) => Set<TEntity>().Add(entity);
    void IApplicationDbContext.AddRange<TEntity>(IEnumerable<TEntity> entities) => Set<TEntity>().AddRange(entities);
    void IApplicationDbContext.Remove<TEntity>(TEntity entity) => Set<TEntity>().Remove(entity);
    void IApplicationDbContext.RemoveRange<TEntity>(IEnumerable<TEntity> entities) => Set<TEntity>().RemoveRange(entities);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditing();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditing()
    {
        var now = dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.OwnerId = entry.Entity.OwnerId == Guid.Empty ? currentUser.UserId : entry.Entity.OwnerId;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<User>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
            {
                entry.Entity.CreatedAtUtc = now;
            }
        }
    }
}
