namespace TicketingSystem.Application.DTOs.Auth
{
    // What the API shows about a user. PasswordHash is left out on purpose.
    public record UserDto(Guid Id, string Name, string Email, string Role);
}
