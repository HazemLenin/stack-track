using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StackTrack.Domain.Entities;

namespace StackTrack.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.DisplayName).HasMaxLength(100);
        builder.Property(x => x.PasswordHash).HasMaxLength(200);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
    }
}
