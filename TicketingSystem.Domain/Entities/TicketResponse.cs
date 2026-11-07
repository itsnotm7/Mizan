using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Domain.Entities
{
    // The answer that goes back to the client: Draft -> Approved -> Sent
    public class TicketResponse : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public Guid AuthorId { get; set; }
        public User Author { get; set; } = null!;

        public string Summary { get; set; } = string.Empty;
        public ResponseStatus Status { get; set; } = ResponseStatus.Draft;

        // Set when the teams manager approves it
        public Guid? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        // Null until it is actually sent to the client
        public DateTime? SentAt { get; set; }
    }
}
