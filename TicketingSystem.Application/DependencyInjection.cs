using Microsoft.Extensions.DependencyInjection;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Application.Services;

namespace TicketingSystem.Application
{
    // Same idea as AddInfrastructure: Program.cs stays one line per layer
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<ITicketService, TicketService>();

            return services;
        }
    }
}
