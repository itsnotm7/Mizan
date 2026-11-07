using System.Linq.Expressions;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Services
{
    // The one place that decides who can see which ticket. The list and details endpoints both use it.
    public static class TicketVisibility
    {
        // WALKTHROUGH piece 2
        public static Expression<Func<Ticket, bool>> VisibleTo(User user)
        {
            throw new NotImplementedException();
        }
    }
}
