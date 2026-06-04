using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class WorkflowTemplatePhaseConfiguration : IEntityTypeConfiguration<WorkflowTemplatePhase>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplatePhase> builder)
    {
        builder.ToTable("workflow_template_phases");
        builder.HasKey(phase => phase.Id);
        builder.Property(phase => phase.Id).ValueGeneratedNever();
        builder.Property(phase => phase.Name).HasMaxLength(120).IsRequired();
        builder.Property(phase => phase.DefaultActor).HasConversion<int>().IsRequired();
        builder.Property(phase => phase.OrderIndex).IsRequired();
        builder.Property(phase => phase.Color).HasMaxLength(32).IsRequired();
        builder.Property(phase => phase.Role).HasConversion<int?>();

        builder.HasIndex(phase => new { phase.WorkflowTemplateId, phase.OrderIndex });
    }
}
