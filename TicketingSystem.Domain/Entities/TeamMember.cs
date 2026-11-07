namespace TicketingSystem.Domain.Entities
{
    // Join table between Team and User (many-to-many)
    public class TeamMember : BaseEntity
    {
        public Guid TeamId { get; set; }
        public Team Team { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
