namespace TicketingSystem.Application.DTOs.Tickets
{
    public record TicketListItemDto(
        Guid Id,
        string Title,
        string Status,
        string Priority,
        string ClientName,
        string? TeamName,
        DateTime CreatedAt);
}
