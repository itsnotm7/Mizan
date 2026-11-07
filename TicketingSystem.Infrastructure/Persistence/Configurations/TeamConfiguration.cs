using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class TeamConfiguration : IEntityTypeConfiguration<Team>
    {
        public void Configure(EntityTypeBuilder<Team> builder)
        {
            builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
            builder.HasIndex(t => t.Name).IsUnique();

            // Restrict = deleting a user who leads a team is blocked by the database.
            // We use Restrict everywhere: SQL Server refuses "multiple cascade paths"
            // (several FKs pointing at Users), and we soft-delete anyway.
            builder.HasOne(t => t.Leader)
                   .WithMany()
                   .HasForeignKey(t => t.LeaderId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
