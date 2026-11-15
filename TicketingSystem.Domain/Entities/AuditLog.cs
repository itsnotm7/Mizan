namespace TicketingSystem.Domain.Entities
{
    // Append-only history. Not a BaseEntity: audit rows are never updated or soft-deleted.
    // No navigation properties on purpose: the log must survive even if the ticket/user it
    // mentions is removed, so we store plain IDs without database foreign keys.
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? TicketId { get; set; }   // null for non-ticket events (e.g. login)
        public Guid ActorId { get; set; }
        public string Action { get; set; } = string.Empty; // "Created", a workflow action name ("Route", "Submit"...), "TeamAssigned" or "MemberAssigned"
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
