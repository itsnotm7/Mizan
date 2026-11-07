using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
    }
}
