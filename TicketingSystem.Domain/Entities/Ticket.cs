using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Domain.Entities
{
    public class Ticket : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RepositoryUrl { get; set; } = string.Empty;
        public Priority Priority { get; set; } = Priority.Medium;
        public TicketStatus Status { get; set; } = TicketStatus.Submitted;
        public DateTime? DueDate { get; set; }
        public DateTime? ClosedAt { get; set; }

        // Who submitted it (always set)
        public Guid ClientId { get; set; }
        public User Client { get; set; } = null!;

        // Set when a teams manager picks it up
        public Guid? ManagerId { get; set; }
        public User? Manager { get; set; }

        // Set when the manager routes it to a team (the leader is Team.Leader)
        public Guid? TeamId { get; set; }
        public Team? Team { get; set; }

        // Concurrency token: SQL Server changes it on every update, so two people
        // saving the same ticket at once get an error instead of overwriting each other
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
        public ICollection<CodeSnapshot> Snapshots { get; set; } = new List<CodeSnapshot>();
        public ICollection<TicketResponse> Responses { get; set; } = new List<TicketResponse>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
