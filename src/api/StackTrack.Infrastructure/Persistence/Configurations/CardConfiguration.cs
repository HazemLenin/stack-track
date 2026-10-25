using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StackTrack.Domain.Entities;

namespace StackTrack.Infrastructure.Persistence.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("card");
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Version).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        builder.HasIndex(x => new { x.ListId, x.Position });
        builder.HasOne<BoardList>().WithMany().HasForeignKey(x => x.ListId).OnDelete(DeleteBehavior.Cascade);
    }
}
