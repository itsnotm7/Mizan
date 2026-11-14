using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    public interface ITeamRepository
    {
        // Both load the leader and the members with their users
        Task<IReadOnlyList<Team>> ListWithMembersAsync(CancellationToken cancellationToken);
        Task<Team?> GetWithMembersAsync(Guid teamId, CancellationToken cancellationToken);
    }
}
