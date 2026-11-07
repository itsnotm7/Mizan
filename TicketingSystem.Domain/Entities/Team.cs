namespace TicketingSystem.Domain.Entities
{
    public class Team : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        // The team leader (a User with the TeamLeader role)
        public Guid LeaderId { get; set; }
        public User Leader { get; set; } = null!; // null! = "EF will fill this in when loading"

        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
    }
}
