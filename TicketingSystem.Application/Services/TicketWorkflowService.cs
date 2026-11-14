using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    // Every step runs the same way (see RunAsync). The methods below only add what is
    // special about their step: TicketWorkflow alone decides whether the step is allowed.
    public class TicketWorkflowService(
        ITicketRepository tickets,
        IUserRepository users,
        ITeamRepository teams,
        ITicketService ticketService) : ITicketWorkflowService
    {
        public Task<TicketDetailsDto> RouteAsync(Guid userId, Guid ticketId, RouteTicketRequest request, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Route, async (ticket, user) =>
            {
                var team = await teams.GetWithMembersAsync(request.TeamId!.Value, cancellationToken)
                    ?? throw new BadRequestException("Team not found.");

                // Without a working leader nobody could assign members, and the ticket would be stuck
                if (!team.Leader.IsActive || team.Leader.IsDeleted || team.Leader.Role != UserRole.TeamLeader)
                    throw new BadRequestException("This team has no active leader.");

                ticket.TeamId = team.Id;
                ticket.ManagerId = user.Id; // From now on, only this manager does the final approval
                AddAudit(ticket, user, "TeamAssigned", null, team.Name);
            }, cancellationToken);

        public Task<TicketDetailsDto> RejectAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Reject, null, cancellationToken);

        public Task<TicketDetailsDto> AssignAsync(Guid userId, Guid ticketId, AssignMembersRequest request, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Assign, async (ticket, user) =>
            {
                // Only active TeamMembers of THIS ticket's team can be assigned
                var team = await teams.GetWithMembersAsync(ticket.TeamId!.Value, cancellationToken)
                    ?? throw new ConflictException("The ticket's team no longer exists.");

                var eligible = team.Members
                    .Where(m => !m.IsDeleted && m.User.IsActive && !m.User.IsDeleted && m.User.Role == UserRole.TeamMember)
                    .ToDictionary(m => m.UserId, m => m.User.Name);

                foreach (var memberId in request.MemberIds.Distinct())
                {
                    if (!eligible.TryGetValue(memberId, out var memberName))
                        throw new BadRequestException($"User {memberId} is not an active member of this team.");

                    // Already on it (e.g. a reopened ticket): keep the existing row.
                    // Assignments are never removed, so the unique (TicketId, UserId) index can't be hit.
                    if (ticket.Assignments.Any(a => a.UserId == memberId && !a.IsDeleted))
                        continue;

                    tickets.AddAssignment(new TicketAssignment { TicketId = ticket.Id, UserId = memberId, AssignedById = user.Id });
                    AddAudit(ticket, user, "MemberAssigned", null, memberName);
                }

                // Count only assignees who can still work on it: on a reopened ticket, an old
                // assignee may have been deactivated or moved off the team since
                if (!ticket.Assignments.Any(a => !a.IsDeleted && eligible.ContainsKey(a.UserId)))
                    throw new BadRequestException("Assign at least one active member of this team.");
            }, cancellationToken);

        public Task<TicketDetailsDto> SubmitAsync(Guid userId, Guid ticketId, SubmitFixRequest request, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Submit, (ticket, user) =>
            {
                // One timestamp for both rows: that's how a sent response is matched to its fix
                var submittedAt = DateTime.UtcNow;

                // The "After" snapshot is the fix. Same repo as the ticket; the branch and commit are the member's.
                tickets.AddSnapshot(new CodeSnapshot
                {
                    TicketId = ticket.Id,
                    CreatedAt = submittedAt,
                    Kind = SnapshotKind.After,
                    RepositoryUrl = ticket.RepositoryUrl,
                    Branch = request.Branch,
                    CommitSha = request.CommitSha,
                    SubmittedById = user.Id
                });

                // A draft until the manager approves it; the client can't see drafts
                tickets.AddResponse(new TicketResponse
                {
                    TicketId = ticket.Id,
                    CreatedAt = submittedAt,
                    AuthorId = user.Id,
                    Summary = request.Summary
                });
                return Task.CompletedTask;
            }, cancellationToken);

        public Task<TicketDetailsDto> LeaderApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.LeaderApprove, null, cancellationToken);

        public Task<TicketDetailsDto> LeaderSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.LeaderSendBack, DiscardDraftAsync, cancellationToken);

        public Task<TicketDetailsDto> ManagerApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.ManagerApprove, (ticket, user) =>
            {
                var draft = ticket.Responses
                    .Where(r => r.Status == ResponseStatus.Draft)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault()
                    ?? throw new ConflictException("There is no draft response to send.");

                // Approving is what sends it: the client sees it from now on
                draft.Status = ResponseStatus.Sent;
                draft.ApprovedById = user.Id;
                draft.SentAt = DateTime.UtcNow;
                return Task.CompletedTask;
            }, cancellationToken);

        public Task<TicketDetailsDto> ManagerSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.ManagerSendBack, DiscardDraftAsync, cancellationToken);

        public Task<TicketDetailsDto> CloseAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Close, (ticket, user) =>
            {
                ticket.ClosedAt = DateTime.UtcNow;
                return Task.CompletedTask;
            }, cancellationToken);

        public Task<TicketDetailsDto> ReopenAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Reopen, null, cancellationToken);

        // The same steps for every action:
        // 1. load the caller fresh from the database   2. load the ticket (404 if they can't see it)
        // 3. TicketWorkflow checks who + status, and changes the status   4. step-specific changes
        // 5. audit row   6. save everything in one transaction
        private async Task<TicketDetailsDto> RunAsync(
            Guid userId,
            Guid ticketId,
            WorkflowAction action,
            Func<Ticket, User, Task>? applyChanges,
            CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            var ticket = await tickets.GetForUpdateAsync(ticketId, TicketVisibility.VisibleTo(user), cancellationToken)
                ?? throw new NotFoundException("Ticket not found.");

            var oldStatus = TicketWorkflow.Apply(user, ticket, action);

            if (applyChanges is not null)
                await applyChanges(ticket, user);

            AddAudit(ticket, user, action.ToString(), oldStatus.ToString(), ticket.Status.ToString());

            // The status change, the step's rows and the audit rows are saved together or not at all
            await tickets.SaveChangesAsync(cancellationToken);

            return await ticketService.GetByIdAsync(userId, ticketId, cancellationToken);
        }

        // Sent back: the draft is hidden (soft-deleted) so the next submit starts a fresh one.
        // The audit log still records that it happened.
        private static Task DiscardDraftAsync(Ticket ticket, User user)
        {
            foreach (var draft in ticket.Responses.Where(r => r.Status == ResponseStatus.Draft))
                draft.IsDeleted = true;

            return Task.CompletedTask;
        }

        private void AddAudit(Ticket ticket, User user, string action, string? oldValue, string? newValue) =>
            tickets.AddAuditLog(new AuditLog
            {
                TicketId = ticket.Id,
                ActorId = user.Id,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue
            });
    }
}
