namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 403 in GlobalExceptionHandler: we know who you are, but you may not do this
    public class ForbiddenException(string message) : Exception(message);
}
