namespace TicketingSystem.Application.DTOs.Tickets
{
    public record AuditEntryDto(
        Guid Id,
        DateTime CreatedAt,
        string ActorName,
        string Action,
        string? OldValue,
        string? NewValue);
}
