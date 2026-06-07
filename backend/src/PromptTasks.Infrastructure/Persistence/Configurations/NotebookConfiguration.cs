using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Notebooks;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class NotebookConfiguration : IEntityTypeConfiguration<Notebook>
{
    public void Configure(EntityTypeBuilder<Notebook> builder)
    {
        builder.ToTable("notebooks");
        builder.HasKey(notebook => notebook.Id);
        builder.Property(notebook => notebook.Id).ValueGeneratedNever();
        builder.Property(notebook => notebook.Title).HasMaxLength(Notebook.MaxTitleLength).IsRequired();
        builder.Property(notebook => notebook.Description).HasMaxLength(Notebook.MaxDescriptionLength);
        builder.Property(notebook => notebook.IsArchived).HasDefaultValue(false).IsRequired();
        builder.Property(notebook => notebook.CreatedAtUtc).IsRequired();
        builder.Property(notebook => notebook.UpdatedAtUtc).IsRequired();

        builder.HasIndex(notebook => new { notebook.OwnerId, notebook.IsArchived });

        builder.HasOne(notebook => notebook.Owner)
            .WithMany()
            .HasForeignKey(notebook => notebook.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(notebook => notebook.WorkingDirectory)
            .WithMany()
            .HasForeignKey(notebook => notebook.WorkingDirectoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(notebook => notebook.Notes)
            .WithOne(note => note.Notebook)
            .HasForeignKey(note => note.NotebookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
