using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
    {
        public void Configure(EntityTypeBuilder<TicketAssignment> builder)
        {
            builder.HasOne(a => a.Ticket)
                   .WithMany(t => t.Assignments)
                   .HasForeignKey(a => a.TicketId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.User)
                   .WithMany()
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.AssignedBy)
                   .WithMany()
                   .HasForeignKey(a => a.AssignedById)
                   .OnDelete(DeleteBehavior.Restrict);

            // The same member can't be assigned to the same ticket twice
            builder.HasIndex(a => new { a.TicketId, a.UserId }).IsUnique();
        }
    }
}
