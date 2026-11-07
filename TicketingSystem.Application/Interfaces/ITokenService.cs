using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) CreateToken(User user);
    }
}
