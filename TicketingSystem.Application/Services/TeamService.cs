using TicketingSystem.Application.DTOs.Teams;
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    public class TeamService(ITeamRepository teams, IUserRepository users) : ITeamService
    {
        // The manager needs every team to route tickets; a leader only needs their own team to assign
        public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            if (user.Role is not (UserRole.TeamsManager or UserRole.Admin or UserRole.TeamLeader))
                throw new ForbiddenException("You can't view teams.");

            var all = await teams.ListWithMembersAsync(cancellationToken);
            var visible = user.Role == UserRole.TeamLeader
                ? all.Where(t => t.LeaderId == user.Id)
                : all;

            return visible
                .Select(t => new TeamDto(
                    t.Id,
                    t.Name,
                    new TicketUserDto(t.Leader.Id, t.Leader.Name),
                    t.Members
                        .Where(m => !m.IsDeleted && m.User.IsActive && !m.User.IsDeleted)
                        .Select(m => new TicketUserDto(m.User.Id, m.User.Name))
                        .ToList()))
                .ToList();
        }
    }
}
