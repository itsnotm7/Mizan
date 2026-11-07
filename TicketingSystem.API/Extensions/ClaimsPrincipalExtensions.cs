using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TicketingSystem.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        // Only call this on authenticated endpoints. Our tokens always carry a valid sub,
        // so a failure here means a bug, and it should surface as a 500.
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? throw new InvalidOperationException("The authenticated user has no 'sub' claim.");

            return Guid.Parse(sub);
        }
    }
}
