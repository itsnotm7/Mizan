using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Enums
{
    // Today a response goes Draft -> Sent: the manager's approval sends it straight away.
    // Approved is kept for a future "approve now, send later" step, and so stored values never break.
    public enum ResponseStatus
    {
        Draft, Approved, Sent
    }
}
