using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class PromptConfiguration : IEntityTypeConfiguration<Prompt>
{
    public void Configure(EntityTypeBuilder<Prompt> builder)
    {
        builder.ToTable("prompts");
        builder.HasKey(prompt => prompt.Id);
        builder.Property(prompt => prompt.Id).ValueGeneratedNever();
        builder.Property(prompt => prompt.TaskNumber).HasMaxLength(64);
        builder.Property(prompt => prompt.Title).HasMaxLength(220).IsRequired();
        builder.Property(prompt => prompt.Content).HasColumnType("text").IsRequired();
        builder.Property(prompt => prompt.TargetAgent).HasConversion<int>().IsRequired();
        builder.Property(prompt => prompt.Kind).HasConversion<int>().IsRequired();
        builder.Property(prompt => prompt.Status).HasConversion<int>().IsRequired();
        builder.Property(prompt => prompt.CreatedAtUtc).IsRequired();
        builder.Property(prompt => prompt.UpdatedAtUtc).IsRequired();
        builder.Property(prompt => prompt.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(prompt => new { prompt.WorkingDirectoryId, prompt.Status });
        builder.HasIndex(prompt => new { prompt.WorkingDirectoryId, prompt.UpdatedAtUtc });
        builder.HasIndex(prompt => new { prompt.WorkingDirectoryId, prompt.TaskNumber })
            .IsUnique()
            .HasFilter("\"TaskNumber\" IS NOT NULL");
        builder.HasIndex(prompt => new { prompt.ParentPromptId, prompt.UpdatedAtUtc });

        builder.HasOne(prompt => prompt.Owner)
            .WithMany()
            .HasForeignKey(prompt => prompt.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(prompt => prompt.ParentPrompt)
            .WithMany(prompt => prompt.ChildPrompts)
            .HasForeignKey(prompt => prompt.ParentPromptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
