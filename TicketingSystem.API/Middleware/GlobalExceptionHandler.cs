using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TicketingSystem.API.Middleware
{
    // Any unhandled exception becomes the same ProblemDetails response.
    // The details go to the log only: clients must never see stack traces.
    public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred."
                }
            });
        }
    }
}
