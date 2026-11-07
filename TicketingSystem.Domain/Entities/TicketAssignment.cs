namespace TicketingSystem.Domain.Entities
{
    // A team member assigned to work on a ticket
    public class TicketAssignment : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        // The member doing the work
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // The leader who assigned them
        public Guid AssignedById { get; set; }
        public User AssignedBy { get; set; } = null!;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
