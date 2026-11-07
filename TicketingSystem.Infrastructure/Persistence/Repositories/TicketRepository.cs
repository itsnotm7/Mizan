using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Repositories
{
    public class TicketRepository(AppDbContext db) : ITicketRepository
    {
        public void Add(Ticket ticket) => db.Tickets.Add(ticket);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

        public async Task<IReadOnlyList<Ticket>> ListAsync(Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken)
        {
            return await db.Tickets
                .AsNoTracking()
                .Include(t => t.Client)
                .Include(t => t.Team)
                .Where(t => !t.IsDeleted) // Soft-delete query filters aren't set up, so excluded by hand
                .Where(filter)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task<Ticket?> GetDetailsAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken)
        {
            return db.Tickets
                .AsNoTracking()
                .Include(t => t.Client)
                .Include(t => t.Manager)
                .Include(t => t.Team)
                .Include(t => t.Assignments.Where(a => !a.IsDeleted))
                    .ThenInclude(a => a.User)
                .Include(t => t.Snapshots.Where(s => !s.IsDeleted))
                .AsSplitQuery() // Two collections in one JOIN would multiply the rows (assignments x snapshots)
                .Where(t => t.Id == ticketId && !t.IsDeleted)
                .Where(filter)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
