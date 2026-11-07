using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class TicketResponseConfiguration : IEntityTypeConfiguration<TicketResponse>
    {
        public void Configure(EntityTypeBuilder<TicketResponse> builder)
        {
            builder.Property(r => r.Summary).HasMaxLength(4000).IsRequired();
            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(r => r.Ticket)
                   .WithMany(t => t.Responses)
                   .HasForeignKey(r => r.TicketId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.Author)
                   .WithMany()
                   .HasForeignKey(r => r.AuthorId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.ApprovedBy)
                   .WithMany()
                   .HasForeignKey(r => r.ApprovedById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
