using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(u => u.Name).HasMaxLength(100).IsRequired();
            builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
            builder.Property(u => u.PasswordHash).IsRequired();

            // Store the enum as text ("TeamLeader") instead of a number (2): readable in SSMS
            builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(30);

            // No two users with the same email (it's the login)
            builder.HasIndex(u => u.Email).IsUnique();
        }
    }
}
