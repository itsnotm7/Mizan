using System.Linq.Expressions;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    // The one place that decides who can see which ticket. The list and details endpoints both use it.
    public static class TicketVisibility
    {
        public static Expression<Func<Ticket, bool>> VisibleTo(User user)
        {
            // Capture just the id, so EF sends one simple SQL parameter
            var userId = user.Id;

            return user.Role switch
            {
                UserRole.Client => t => t.ClientId == userId,
                UserRole.TeamsManager or UserRole.Admin => t => true,
                // No team until the manager routes it, so a new ticket isn't visible to any leader yet
                UserRole.TeamLeader => t => t.Team != null && t.Team.LeaderId == userId,
                // Being on the team isn't enough: the member must be assigned to this ticket
                UserRole.TeamMember => t => t.Assignments.Any(a => a.UserId == userId && !a.IsDeleted),
                // A role added later sees nothing until someone writes its rule
                _ => t => false
            };
        }
    }
}
