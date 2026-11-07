using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Configurations
{
    public class CodeSnapshotConfiguration : IEntityTypeConfiguration<CodeSnapshot>
    {
        public void Configure(EntityTypeBuilder<CodeSnapshot> builder)
        {
            builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(10);
            builder.Property(s => s.RepositoryUrl).HasMaxLength(500).IsRequired();
            builder.Property(s => s.Branch).HasMaxLength(200).IsRequired();
            builder.Property(s => s.CommitSha).HasMaxLength(64).IsRequired();
            builder.Property(s => s.ArchivePath).HasMaxLength(500);

            builder.HasOne(s => s.Ticket)
                   .WithMany(t => t.Snapshots)
                   .HasForeignKey(s => s.TicketId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.SubmittedBy)
                   .WithMany()
                   .HasForeignKey(s => s.SubmittedById)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
