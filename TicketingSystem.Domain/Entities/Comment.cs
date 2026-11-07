namespace TicketingSystem.Domain.Entities
{
    public class Comment : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public Guid AuthorId { get; set; }
        public User Author { get; set; } = null!;

        public string Body { get; set; } = string.Empty;

        // true = team-only discussion, hidden from the client
        public bool IsInternal { get; set; }
    }
}
