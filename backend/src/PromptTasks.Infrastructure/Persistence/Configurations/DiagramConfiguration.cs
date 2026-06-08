using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class DiagramConfiguration : IEntityTypeConfiguration<Diagram>
{
    public void Configure(EntityTypeBuilder<Diagram> builder)
    {
        builder.ToTable("diagrams");
        builder.HasKey(diagram => diagram.Id);
        builder.Property(diagram => diagram.Id).ValueGeneratedNever();
        builder.Property(diagram => diagram.Title).HasMaxLength(Diagram.MaxTitleLength).IsRequired();
        builder.Property(diagram => diagram.Description).HasMaxLength(Diagram.MaxDescriptionLength);
        builder.Property(diagram => diagram.Type).HasConversion<int>().IsRequired();
        builder.Property(diagram => diagram.Content).HasColumnType("text").IsRequired();
        builder.Property(diagram => diagram.MetadataJson).HasColumnType("text");
        builder.Property(diagram => diagram.IsArchived).HasDefaultValue(false).IsRequired();
        builder.Property(diagram => diagram.CreatedAtUtc).IsRequired();
        builder.Property(diagram => diagram.UpdatedAtUtc).IsRequired();

        builder.HasIndex(diagram => new { diagram.OwnerId, diagram.IsArchived });
        builder.HasIndex(diagram => new { diagram.WorkingDirectoryId, diagram.IsArchived });
        builder.HasIndex(diagram => new { diagram.WorkingDirectoryId, diagram.UpdatedAtUtc });

        builder.HasOne(diagram => diagram.Owner)
            .WithMany()
            .HasForeignKey(diagram => diagram.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(diagram => diagram.WorkingDirectory)
            .WithMany()
            .HasForeignKey(diagram => diagram.WorkingDirectoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
