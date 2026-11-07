using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Domain.Entities
{
    // A point-in-time reference to the code: "Before" (as submitted) or "After" (the fix)
    public class CodeSnapshot : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public SnapshotKind Kind { get; set; }
        public string RepositoryUrl { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string CommitSha { get; set; } = string.Empty;
        public string? ArchivePath { get; set; }

        public Guid SubmittedById { get; set; }
        public User SubmittedBy { get; set; } = null!;
    }
}
