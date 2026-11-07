using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Application.Exceptions;

namespace TicketingSystem.API.Middleware
{
    // Any unhandled exception becomes the same ProblemDetails response.
    // The details go to the log only: clients must never see stack traces.
    public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // Application throws these on purpose, so their messages are safe to show.
            // Application can't use status codes itself: it doesn't know about HTTP.
            var (status, title) = exception switch
            {
                UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message),
                NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };

            // A refused request is normal behaviour. Only real failures are logged as errors.
            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = status;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title
                }
            });
        }
    }
}
