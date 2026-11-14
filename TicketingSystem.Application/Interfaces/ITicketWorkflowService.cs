using TicketingSystem.Application.DTOs.Tickets;

namespace TicketingSystem.Application.Interfaces
{
    // One method per workflow step. Each returns the updated ticket.
    public interface ITicketWorkflowService
    {
        Task<TicketDetailsDto> RouteAsync(Guid userId, Guid ticketId, RouteTicketRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> RejectAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> AssignAsync(Guid userId, Guid ticketId, AssignMembersRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> SubmitAsync(Guid userId, Guid ticketId, SubmitFixRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> LeaderApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> LeaderSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ManagerApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ManagerSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> CloseAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ReopenAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
    }
}
