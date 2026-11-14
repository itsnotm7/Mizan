using System.Linq.Expressions;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    // Application says what data it needs and Infrastructure decides how to get it,
    // so the business logic doesn't depend on EF Core
    public interface ITicketRepository
    {
        void Add(Ticket ticket);
        void AddAuditLog(AuditLog entry);

        // New rows for an existing ticket must be added explicitly. Our ids are set in C#
        // (Guid.NewGuid), so if they were only put in ticket.Assignments, EF would think they
        // already exist and send an UPDATE instead of an INSERT.
        void AddAssignment(TicketAssignment assignment);
        void AddSnapshot(CodeSnapshot snapshot);
        void AddResponse(TicketResponse response);

        // Throws ConflictException if someone else saved the same ticket in the meantime
        Task SaveChangesAsync(CancellationToken cancellationToken);

        // The filter is an Expression (not a Func) so EF can turn it into SQL
        // instead of loading every ticket and filtering in memory
        Task<IReadOnlyList<Ticket>> ListAsync(Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);
        Task<Ticket?> GetDetailsAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);
        Task<bool> IsVisibleAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);

        // Tracked (unlike the reads above), so changes made to it are saved by SaveChangesAsync
        Task<Ticket?> GetForUpdateAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);

        Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync(Guid ticketId, CancellationToken cancellationToken);
    }
}
