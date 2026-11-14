using Microsoft.EntityFrameworkCore;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Repositories
{
    public class TeamRepository(AppDbContext db) : ITeamRepository
    {
        public async Task<IReadOnlyList<Team>> ListWithMembersAsync(CancellationToken cancellationToken)
        {
            return await TeamsWithMembers()
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<Team?> GetWithMembersAsync(Guid teamId, CancellationToken cancellationToken)
        {
            return TeamsWithMembers().FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        }

        private IQueryable<Team> TeamsWithMembers() => db.Teams
            .AsNoTracking()
            .Include(t => t.Leader)
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .Where(t => !t.IsDeleted);
    }
}
