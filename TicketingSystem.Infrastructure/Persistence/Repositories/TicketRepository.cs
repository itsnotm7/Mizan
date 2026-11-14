using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Repositories
{
    public class TicketRepository(AppDbContext db) : ITicketRepository
    {
        public void Add(Ticket ticket) => db.Tickets.Add(ticket);

        public void AddAuditLog(AuditLog entry) => db.AuditLogs.Add(entry);

        // EF also links each one into the loaded ticket's collection right away
        public void AddAssignment(TicketAssignment assignment) => db.TicketAssignments.Add(assignment);

        public void AddSnapshot(CodeSnapshot snapshot) => db.CodeSnapshots.Add(snapshot);

        public void AddResponse(TicketResponse response) => db.TicketResponses.Add(response);

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Ticket.RowVersion changed since we loaded it: someone else saved first.
                // Turned into an Application exception here, so the API doesn't need to know EF.
                throw new ConflictException("Someone else changed this ticket at the same time. Reload it and try again.");
            }
        }

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
                .Include(t => t.Responses.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Author)
                .AsSplitQuery() // Several collections in one JOIN would multiply the rows
                .Where(t => t.Id == ticketId && !t.IsDeleted)
                .Where(filter)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> IsVisibleAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken)
        {
            return db.Tickets
                .Where(t => t.Id == ticketId && !t.IsDeleted)
                .Where(filter)
                .AnyAsync(cancellationToken);
        }

        public Task<Ticket?> GetForUpdateAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken)
        {
            // Only what the workflow rules need: Team (leader check), Assignments, Responses (drafts)
            return db.Tickets
                .Include(t => t.Team)
                .Include(t => t.Assignments.Where(a => !a.IsDeleted))
                .Include(t => t.Responses.Where(r => !r.IsDeleted))
                .AsSplitQuery()
                .Where(t => t.Id == ticketId && !t.IsDeleted)
                .Where(filter)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync(Guid ticketId, CancellationToken cancellationToken)
        {
            return await db.AuditLogs
                .AsNoTracking()
                .Where(a => a.TicketId == ticketId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
