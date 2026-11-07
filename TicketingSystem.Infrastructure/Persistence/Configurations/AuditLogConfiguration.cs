using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
            builder.Property(a => a.IpAddress).HasMaxLength(45); // fits IPv6

            // No relationships configured on purpose (see AuditLog.cs).
            // Indexes so "history of ticket X, newest first" stays fast.
            builder.HasIndex(a => a.TicketId);
            builder.HasIndex(a => a.CreatedAt);
        }
    }
}
