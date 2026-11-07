using System.Linq.Expressions;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    // Application says what data it needs and Infrastructure decides how to get it,
    // so the business logic doesn't depend on EF Core
    public interface ITicketRepository
    {
        void Add(Ticket ticket);
        Task SaveChangesAsync(CancellationToken cancellationToken);

        // The filter is an Expression (not a Func) so EF can turn it into SQL
        // instead of loading every ticket and filtering in memory
        Task<IReadOnlyList<Ticket>> ListAsync(Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);
        Task<Ticket?> GetDetailsAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);
    }
}
