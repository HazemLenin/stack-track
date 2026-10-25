using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StackTrack.Domain.Entities;

namespace StackTrack.Infrastructure.Persistence.Configurations;

public class CardAssigneeConfiguration : IEntityTypeConfiguration<CardAssignee>
{
    public void Configure(EntityTypeBuilder<CardAssignee> builder)
    {
        builder.ToTable("card_assignee");
        builder.HasIndex(x => new { x.CardId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
