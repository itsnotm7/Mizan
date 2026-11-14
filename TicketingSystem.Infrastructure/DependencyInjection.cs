using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Infrastructure.Auth;
using TicketingSystem.Infrastructure.Persistence;
using TicketingSystem.Infrastructure.Persistence.Repositories;

namespace TicketingSystem.Infrastructure
{
    // Everything Infrastructure needs registered lives here, so Program.cs stays one line:
    // builder.Services.AddInfrastructure(builder.Configuration);
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is missing in appsettings.json.");

            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

            services.AddSingleton(JwtOptions.Load(configuration));
            services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>(); // Scoped because it uses the scoped DbContext
            services.AddScoped<ITicketRepository, TicketRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();

            return services;
        }
    }
}
