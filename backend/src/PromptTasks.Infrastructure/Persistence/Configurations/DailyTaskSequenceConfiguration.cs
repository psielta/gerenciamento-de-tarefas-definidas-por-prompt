using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class DailyTaskSequenceConfiguration : IEntityTypeConfiguration<DailyTaskSequence>
{
    public void Configure(EntityTypeBuilder<DailyTaskSequence> builder)
    {
        builder.ToTable("daily_task_sequences");
        builder.HasKey(sequence => sequence.Id);
        builder.Property(sequence => sequence.Id).ValueGeneratedNever();
        builder.Property(sequence => sequence.SequenceDate).HasColumnType("date").IsRequired();
        builder.Property(sequence => sequence.CurrentValue).IsRequired();
        builder.Property(sequence => sequence.CreatedAtUtc).IsRequired();
        builder.Property(sequence => sequence.UpdatedAtUtc).IsRequired();

        builder.HasIndex(sequence => new { sequence.WorkingDirectoryId, sequence.SequenceDate }).IsUnique();

        builder.HasOne(sequence => sequence.WorkingDirectory)
            .WithMany()
            .HasForeignKey(sequence => sequence.WorkingDirectoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
