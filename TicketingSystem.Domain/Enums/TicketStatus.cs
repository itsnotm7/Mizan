using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Enums
{
    public enum TicketStatus
    {
        Submitted, Routed, InProgress,
        LeaderReview, ManagerReview,
        ResponseSent, Closed, Rejected, Reopened
    }
}
