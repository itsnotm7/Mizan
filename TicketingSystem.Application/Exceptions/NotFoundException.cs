namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 404 in GlobalExceptionHandler
    public class NotFoundException(string message) : Exception(message);
}
