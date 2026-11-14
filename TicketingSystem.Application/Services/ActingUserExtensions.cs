using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Services
{
    public static class ActingUserExtensions
    {
        // JwtBearer only checks the token, so a deactivated user's token still passes it.
        // Reading the user here means the role and status are always the current ones.
        // Every ticket, workflow and team service starts with this.
        public static async Task<User> GetActingUserAsync(this IUserRepository users, Guid userId, CancellationToken cancellationToken)
        {
            var user = await users.GetByIdAsync(userId, cancellationToken);

            if (user is null || user.IsDeleted || !user.IsActive)
                throw new UnauthorizedException("This account is no longer active.");

            return user;
        }
    }
}
