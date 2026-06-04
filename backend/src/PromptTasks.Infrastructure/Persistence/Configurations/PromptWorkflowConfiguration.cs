using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class PromptWorkflowConfiguration : IEntityTypeConfiguration<PromptWorkflow>
{
    public void Configure(EntityTypeBuilder<PromptWorkflow> builder)
    {
        builder.ToTable("prompt_workflows");
        builder.HasKey(workflow => workflow.Id);
        builder.Property(workflow => workflow.Id).ValueGeneratedNever();
        builder.Property(workflow => workflow.Status).HasConversion<int>().IsRequired();
        builder.Property(workflow => workflow.CurrentPhaseName).HasMaxLength(120);
        builder.Property(workflow => workflow.CurrentPhaseColor).HasMaxLength(32);
        builder.Property(workflow => workflow.CurrentActor).HasConversion<int>();
        builder.Property(workflow => workflow.CurrentPhaseIteration).IsRequired().HasDefaultValue(1);
        builder.Property(workflow => workflow.StartedAtUtc).IsRequired();
        builder.Property(workflow => workflow.CreatedAtUtc).IsRequired();
        builder.Property(workflow => workflow.UpdatedAtUtc).IsRequired();
        builder.Property(workflow => workflow.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(workflow => workflow.PromptId).IsUnique();

        builder.HasOne(workflow => workflow.Prompt)
            .WithOne()
            .HasForeignKey<PromptWorkflow>(workflow => workflow.PromptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(workflow => workflow.Phases)
            .WithOne(phase => phase.Workflow)
            .HasForeignKey(phase => phase.PromptWorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(workflow => workflow.Events)
            .WithOne(@event => @event.Workflow)
            .HasForeignKey(@event => @event.PromptWorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
