using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Application.DTOs.Auth;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Infrastructure.Persistence;

namespace TicketingSystem.Infrastructure.Auth
{
    public class AuthService(AppDbContext db, IPasswordHasher<User> passwordHasher, ITokenService tokenService) : IAuthService
    {
        // Only used to spend the same time on unknown emails as on real ones (see LoginAsync)
        private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString());

        public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            // Soft-delete query filters aren't set up, so deleted users are excluded here by hand
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive && !u.IsDeleted, cancellationToken);

            if (user is null)
            {
                // Hashing is deliberately slow. Skipping it here would let an attacker
                // spot which emails exist just by timing the response.
                passwordHasher.VerifyHashedPassword(new User(), DummyHash, request.Password);
                return null;
            }

            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
                return null;

            var (token, expiresAt) = tokenService.CreateToken(user);
            return new LoginResponse(token, expiresAt, ToDto(user));
        }

        public async Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            // Read from the database, not the token: a user deactivated after logging in is refused right away
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted, cancellationToken);

            return user is null ? null : ToDto(user);
        }

        private static UserDto ToDto(User user) => new(user.Id, user.Name, user.Email, user.Role.ToString());
    }
}
