using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StackTrack.Domain.Entities;

namespace StackTrack.Infrastructure.Persistence.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("label");
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Color).HasMaxLength(7);
        builder.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
    }
}
