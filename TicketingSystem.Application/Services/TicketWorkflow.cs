using TicketingSystem.Application.Exceptions;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    // The ONE place that decides which status changes exist and who may make them.
    // Each line of the table is one arrow of the workflow in CLAUDE.md.
    public static class TicketWorkflow
    {
        private record Rule(TicketStatus[] From, TicketStatus To, Func<User, Ticket, bool> IsAllowed);

        private static readonly Dictionary<WorkflowAction, Rule> Rules = new()
        {
            [WorkflowAction.Route]           = new([TicketStatus.Submitted], TicketStatus.Routed, IsTeamsManager),
            [WorkflowAction.Reject]          = new([TicketStatus.Submitted], TicketStatus.Rejected, IsTeamsManager),
            [WorkflowAction.Assign]          = new([TicketStatus.Routed, TicketStatus.Reopened], TicketStatus.InProgress, IsLeaderOfTicketTeam),
            [WorkflowAction.Submit]          = new([TicketStatus.InProgress], TicketStatus.LeaderReview, IsAssignedMember),
            [WorkflowAction.LeaderApprove]   = new([TicketStatus.LeaderReview], TicketStatus.ManagerReview, IsLeaderOfTicketTeam),
            [WorkflowAction.LeaderSendBack]  = new([TicketStatus.LeaderReview], TicketStatus.InProgress, IsLeaderOfTicketTeam),
            [WorkflowAction.ManagerApprove]  = new([TicketStatus.ManagerReview], TicketStatus.ResponseSent, IsRoutingManager),
            [WorkflowAction.ManagerSendBack] = new([TicketStatus.ManagerReview], TicketStatus.InProgress, IsRoutingManager),
            [WorkflowAction.Close]           = new([TicketStatus.ResponseSent], TicketStatus.Closed, IsOwner),
            [WorkflowAction.Reopen]          = new([TicketStatus.ResponseSent], TicketStatus.Reopened, IsOwner)
        };

        public static bool CanPerform(User user, Ticket ticket, WorkflowAction action)
        {
            var rule = Rules[action];
            return rule.From.Contains(ticket.Status) && rule.IsAllowed(user, ticket);
        }

        // Sent to the frontend so it shows only the buttons this user can use right now.
        // It comes from the same table, so the buttons and the server can never disagree.
        public static IReadOnlyList<WorkflowAction> AllowedActions(User user, Ticket ticket) =>
            Enum.GetValues<WorkflowAction>().Where(action => CanPerform(user, ticket, action)).ToList();

        // Checks the rule, changes the status, and returns the old one for the audit log
        public static TicketStatus Apply(User user, Ticket ticket, WorkflowAction action)
        {
            var rule = Rules[action];

            // Who first: a user who may never do this gets 403, whatever the status
            if (!rule.IsAllowed(user, ticket))
                throw new ForbiddenException($"You can't {action} this ticket.");

            if (!rule.From.Contains(ticket.Status))
                throw new ConflictException($"Can't {action} a ticket that is {ticket.Status}.");

            var oldStatus = ticket.Status;
            ticket.Status = rule.To;
            return oldStatus;
        }

        // The checks go beyond the role: it must be the leader of THIS ticket's team,
        // a member assigned to THIS ticket, the manager who routed it, the client who owns it
        private static bool IsTeamsManager(User user, Ticket ticket) =>
            user.Role == UserRole.TeamsManager;

        private static bool IsRoutingManager(User user, Ticket ticket) =>
            user.Role == UserRole.TeamsManager && ticket.ManagerId == user.Id;

        private static bool IsLeaderOfTicketTeam(User user, Ticket ticket) =>
            user.Role == UserRole.TeamLeader && ticket.Team != null && ticket.Team.LeaderId == user.Id;

        private static bool IsAssignedMember(User user, Ticket ticket) =>
            user.Role == UserRole.TeamMember && ticket.Assignments.Any(a => a.UserId == user.Id && !a.IsDeleted);

        private static bool IsOwner(User user, Ticket ticket) =>
            user.Role == UserRole.Client && ticket.ClientId == user.Id;
    }
}
