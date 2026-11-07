using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Auth;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [AllowAnonymous] // The one public endpoint: Program.cs protects everything else
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
        {
            var response = await authService.LoginAsync(request, cancellationToken);

            return response is null
                ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.")
                : Ok(response);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
        {
            var user = await authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken);

            return user is null
                ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "This account is no longer active.")
                : Ok(user);
        }
    }
}
