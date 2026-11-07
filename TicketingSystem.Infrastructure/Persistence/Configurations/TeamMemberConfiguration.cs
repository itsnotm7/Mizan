using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
    {
        public void Configure(EntityTypeBuilder<TeamMember> builder)
        {
            builder.HasOne(m => m.Team)
                   .WithMany(t => t.Members)
                   .HasForeignKey(m => m.TeamId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.User)
                   .WithMany()
                   .HasForeignKey(m => m.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            // A user can be in a given team only once
            builder.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique();
        }
    }
}
