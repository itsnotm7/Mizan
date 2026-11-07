using TicketingSystem.Application.DTOs.Auth;

namespace TicketingSystem.Application.Interfaces
{
    public interface IAuthService
    {
        // Returns null when the login fails, without saying why:
        // the caller must not learn whether the email exists
        Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

        // Returns null when the user no longer exists or was deactivated
        Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    }
}
