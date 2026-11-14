using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

        // Audit rows store plain ids (no foreign keys), so names are looked up separately
        Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
    }
}
