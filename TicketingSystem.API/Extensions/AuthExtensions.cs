using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketingSystem.Infrastructure.Auth;

namespace TicketingSystem.API.Extensions
{
    public static class AuthExtensions
    {
        public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration configuration)
        {
            var jwt = JwtOptions.Load(configuration);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Keep claim names as written in the token ("sub", "role").
                    // By default .NET renames them to long URIs, which breaks FindFirst("sub").
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = jwt.CreateSigningKey(),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // Accept only the algorithm we sign with
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1), // The default allows 5 extra minutes after expiry

                        // [Authorize(Roles = ...)] looks for roles in the claim named here
                        RoleClaimType = JwtOptions.RoleClaimType,
                        NameClaimType = JwtRegisteredClaimNames.Sub
                    };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
