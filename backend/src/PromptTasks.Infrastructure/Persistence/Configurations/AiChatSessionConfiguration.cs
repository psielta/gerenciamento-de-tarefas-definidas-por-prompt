using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptTasks.Domain.Ai;

namespace PromptTasks.Infrastructure.Persistence.Configurations;

public sealed class AiChatSessionConfiguration : IEntityTypeConfiguration<AiChatSession>
{
    public void Configure(EntityTypeBuilder<AiChatSession> builder)
    {
        builder.ToTable("ai_chat_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Title).HasMaxLength(220).IsRequired();
        builder.Property(s => s.Model).HasMaxLength(100).IsRequired();
        builder.Property(s => s.GeminiCacheName).HasMaxLength(500);
        builder.Property(s => s.CacheSystemInstructionHash).HasMaxLength(64);
        builder.Property(s => s.ThinkingLevel).HasMaxLength(50);
        builder.HasIndex(s => new { s.OwnerId, s.UpdatedAtUtc });
        builder.HasIndex(s => s.PromptId);
    }
}
