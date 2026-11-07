using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
    {
        public void Configure(EntityTypeBuilder<Ticket> builder)
        {
            builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
            builder.Property(t => t.Description).HasMaxLength(4000).IsRequired();
            builder.Property(t => t.RepositoryUrl).HasMaxLength(500).IsRequired();

            builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
            builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);

            // Maps to SQL Server's rowversion type (auto-updated concurrency token)
            builder.Property(t => t.RowVersion).IsRowVersion();

            // Ticket has TWO foreign keys to Users (Client and Manager), so EF can't
            // guess which navigation goes with which key: we spell each one out.
            builder.HasOne(t => t.Client)
                   .WithMany()
                   .HasForeignKey(t => t.ClientId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.Manager)
                   .WithMany()
                   .HasForeignKey(t => t.ManagerId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.Team)
                   .WithMany()
                   .HasForeignKey(t => t.TeamId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Indexes on the columns we filter by most (ticket lists per role)
            builder.HasIndex(t => t.Status);
            builder.HasIndex(t => t.ClientId);
            builder.HasIndex(t => t.TeamId);
            builder.HasIndex(t => t.CreatedAt);
        }
    }
}
