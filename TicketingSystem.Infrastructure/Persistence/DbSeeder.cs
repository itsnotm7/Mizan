using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Infrastructure.Persistence
{
    // Fills an empty database with demo data: one user per role and one team.
    // Runs at startup in Development only. Does nothing if users already exist.
    public static class DbSeeder
    {
        // DEV/DEMO ONLY. Every seeded account uses this password.
        private const string DemoPassword = "Mizan@123";

        public static async Task SeedAsync(AppDbContext db)
        {
            if (await db.Users.AnyAsync())
                return;

            var hasher = new PasswordHasher<User>();

            User NewUser(string name, string email, UserRole role)
            {
                var user = new User { Name = name, Email = email, Role = role };
                // Never store the plain password: store a salted hash
                user.PasswordHash = hasher.HashPassword(user, DemoPassword);
                return user;
            }

            var admin   = NewUser("System Admin",   "admin@mizan.local",   UserRole.Admin);
            var manager = NewUser("Teams Manager",  "manager@mizan.local", UserRole.TeamsManager);
            var leader  = NewUser("Team Leader",    "leader@mizan.local",  UserRole.TeamLeader);
            var member1 = NewUser("Member One",     "member1@mizan.local", UserRole.TeamMember);
            var member2 = NewUser("Member Two",     "member2@mizan.local", UserRole.TeamMember);
            var client  = NewUser("Demo Client",    "client@mizan.local",  UserRole.Client);

            db.Users.AddRange(admin, manager, leader, member1, member2, client);

            var team = new Team { Name = "Backend Team", Leader = leader };
            team.Members.Add(new TeamMember { User = member1 });
            team.Members.Add(new TeamMember { User = member2 });
            db.Teams.Add(team);

            await db.SaveChangesAsync();
        }
    }
}
