namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 401 in GlobalExceptionHandler: the token is valid but the account no longer is
    public class UnauthorizedException(string message) : Exception(message);
}
