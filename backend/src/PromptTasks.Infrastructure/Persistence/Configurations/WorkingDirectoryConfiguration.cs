using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class WorkingDirectoryConfiguration : IEntityTypeConfiguration<WorkingDirectory>
{
    public void Configure(EntityTypeBuilder<WorkingDirectory> builder)
    {
        builder.ToTable("working_directories");
        builder.HasKey(directory => directory.Id);
        builder.Property(directory => directory.Id).ValueGeneratedNever();
        builder.Property(directory => directory.Name).HasMaxLength(160).IsRequired();
        builder.Property(directory => directory.AbsolutePath).HasMaxLength(1024).IsRequired();
        builder.Property(directory => directory.EnableAiContext).HasDefaultValue(false).IsRequired();
        builder.Property(directory => directory.CreatedAtUtc).IsRequired();
        builder.Property(directory => directory.UpdatedAtUtc).IsRequired();

        builder.HasIndex(directory => new { directory.OwnerId, directory.AbsolutePath }).IsUnique();

        builder.HasOne(directory => directory.Owner)
            .WithMany()
            .HasForeignKey(directory => directory.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(directory => directory.Prompts)
            .WithOne(prompt => prompt.WorkingDirectory)
            .HasForeignKey(prompt => prompt.WorkingDirectoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
