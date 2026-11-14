using Microsoft.EntityFrameworkCore;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence.Repositories
{
    public class UserRepository(AppDbContext db) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            return db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
        {
            var ids = userIds.ToList();

            // Deleted users are included on purpose: history should still show who did what
            return await db.Users
                .AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        }
    }
}
