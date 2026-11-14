using TicketingSystem.Application.DTOs.Tickets;

namespace TicketingSystem.Application.Interfaces
{
    // Each method takes only the caller's id: the service loads the user from the database,
    // so the role and active status are never taken from a possibly old token
    public interface ITicketService
    {
        Task<TicketDetailsDto> CreateAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken);
        Task<IReadOnlyList<TicketListItemDto>> GetVisibleTicketsAsync(Guid userId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> GetByIdAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<IReadOnlyList<AuditEntryDto>> GetHistoryAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
    }
}
