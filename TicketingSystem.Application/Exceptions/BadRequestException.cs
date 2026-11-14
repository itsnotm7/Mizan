namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 400 in GlobalExceptionHandler: input that passes the DTO checks but is still
    // wrong once compared with the database (e.g. a member who isn't in the ticket's team)
    public class BadRequestException(string message) : Exception(message);
}
