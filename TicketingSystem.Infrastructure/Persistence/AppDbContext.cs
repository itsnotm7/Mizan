using Microsoft.EntityFrameworkCore;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // One DbSet per table
        public DbSet<User> Users => Set<User>();
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketAssignment> TicketAssignments => Set<TicketAssignment>();
        public DbSet<CodeSnapshot> CodeSnapshots => Set<CodeSnapshot>();
        public DbSet<TicketResponse> TicketResponses => Set<TicketResponse>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Finds every IEntityTypeConfiguration<T> class in this project (the
            // Configurations folder) and applies it, so we don't register them one by one
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        // Runs on every save: stamps UpdatedAt automatically so no service has to remember it
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
