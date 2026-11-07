using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketingSystem.Infrastructure.Persistence;

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

            return services;
        }
    }
}
