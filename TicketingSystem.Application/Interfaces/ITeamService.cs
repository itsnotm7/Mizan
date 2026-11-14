using TicketingSystem.Application.DTOs.Teams;

namespace TicketingSystem.Application.Interfaces
{
    public interface ITeamService
    {
        Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid userId, CancellationToken cancellationToken);
    }
}
