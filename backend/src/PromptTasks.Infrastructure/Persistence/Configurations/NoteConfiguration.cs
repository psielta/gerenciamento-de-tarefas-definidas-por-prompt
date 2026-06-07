using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Notebooks;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Id).ValueGeneratedNever();
        builder.Property(note => note.Title).HasMaxLength(Note.MaxTitleLength).IsRequired();
        builder.Property(note => note.ContentMarkdown).HasMaxLength(Note.MaxContentLength).IsRequired();
        builder.Property(note => note.IsPinned).HasDefaultValue(false).IsRequired();
        builder.Property(note => note.IsArchived).HasDefaultValue(false).IsRequired();
        builder.Property(note => note.CreatedAtUtc).IsRequired();
        builder.Property(note => note.UpdatedAtUtc).IsRequired();

        builder.HasIndex(note => new { note.NotebookId, note.IsArchived });
        builder.HasIndex(note => note.OwnerId);

        builder.HasOne(note => note.Owner)
            .WithMany()
            .HasForeignKey(note => note.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
