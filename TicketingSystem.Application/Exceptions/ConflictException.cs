namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 409 in GlobalExceptionHandler: the request is fine, but the ticket's current
    // state doesn't allow it (wrong status, or someone else saved it at the same moment)
    public class ConflictException(string message) : Exception(message);
}
