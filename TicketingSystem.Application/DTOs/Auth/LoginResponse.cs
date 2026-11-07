namespace TicketingSystem.Application.DTOs.Auth
{
    public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
}
