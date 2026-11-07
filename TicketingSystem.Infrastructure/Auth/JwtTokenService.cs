using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Auth
{
    public class JwtTokenService(JwtOptions options) : ITokenService
    {
        public (string Token, DateTime ExpiresAt) CreateToken(User user)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(options.ExpiryMinutes);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = options.Issuer,
                Audience = options.Audience,
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
                // The token is signed, not encrypted: anyone can read these, so no sensitive data here
                Claims = new Dictionary<string, object>
                {
                    [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                    [JwtRegisteredClaimNames.Email] = user.Email,
                    [JwtOptions.RoleClaimType] = user.Role.ToString()
                }
            };

            return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
        }
    }
}
