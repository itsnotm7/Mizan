using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.Property(c => c.Body).HasMaxLength(4000).IsRequired();

            builder.HasOne(c => c.Ticket)
                   .WithMany(t => t.Comments)
                   .HasForeignKey(c => c.TicketId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.Author)
                   .WithMany()
                   .HasForeignKey(c => c.AuthorId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
