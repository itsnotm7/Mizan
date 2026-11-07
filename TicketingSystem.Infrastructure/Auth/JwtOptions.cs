using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace TicketingSystem.Infrastructure.Auth
{
    // The "Jwt" section of configuration. SigningKey comes from user-secrets, never from a tracked file.
    public class JwtOptions
    {
        public const string SectionName = "Jwt";
        public const string RoleClaimType = "role";

        // HMAC-SHA256 needs a key of at least 256 bits (32 bytes)
        private const int MinKeyBytes = 32;

        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SigningKey { get; set; } = string.Empty;
        public int ExpiryMinutes { get; set; } = 120;

        // Stop at startup with a clear message, instead of failing later on the first login
        public static JwtOptions Load(IConfiguration configuration)
        {
            var options = configuration.GetSection(SectionName).Get<JwtOptions>()
                ?? throw new InvalidOperationException($"Configuration section '{SectionName}' is missing.");

            if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
                throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be set in appsettings.json.");

            if (Encoding.UTF8.GetByteCount(options.SigningKey) < MinKeyBytes)
                throw new InvalidOperationException(
                    $"Jwt:SigningKey must be at least {MinKeyBytes} bytes. Set it with: " +
                    "dotnet user-secrets set \"Jwt:SigningKey\" \"<key>\" --project TicketingSystem.API");

            if (options.ExpiryMinutes <= 0)
                throw new InvalidOperationException("Jwt:ExpiryMinutes must be greater than zero.");

            return options;
        }

        // One place builds the key, so signing and validating can't disagree
        public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
    }
}
