using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    public class TicketService(ITicketRepository tickets, IUserRepository users) : ITicketService
    {
        public const string CreatedAction = "Created";

        // Clients see the outcome of their ticket, not the team's internal back-and-forth
        private static readonly HashSet<string> ClientVisibleActions =
        [
            CreatedAction,
            nameof(WorkflowAction.Route),
            nameof(WorkflowAction.Reject),
            nameof(WorkflowAction.ManagerApprove),
            nameof(WorkflowAction.Close),
            nameof(WorkflowAction.Reopen)
        ];

        public async Task<TicketDetailsDto> CreateAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            // The role comes from the database, so a role changed after login applies right away.
            // 403, not 404: there's nothing secret here, everyone knows this endpoint exists.
            if (user.Role != UserRole.Client)
                throw new ForbiddenException("Only clients can create tickets.");

            var ticket = new Ticket
            {
                Title = request.Title,
                Description = request.Description,
                RepositoryUrl = request.RepositoryUrl,
                Priority = request.Priority,
                ClientId = user.Id
            };

            // Added through the ticket, so one SaveChanges writes both rows in one transaction:
            // a ticket can never exist without its "Before" snapshot
            ticket.Snapshots.Add(new CodeSnapshot
            {
                Kind = SnapshotKind.Before,
                RepositoryUrl = request.RepositoryUrl,
                Branch = request.Branch,
                CommitSha = request.CommitSha,
                SubmittedById = user.Id
            });

            tickets.Add(ticket);
            tickets.AddAuditLog(new AuditLog
            {
                TicketId = ticket.Id,
                ActorId = user.Id,
                Action = CreatedAction,
                NewValue = ticket.Status.ToString()
            });
            await tickets.SaveChangesAsync(cancellationToken);

            return await GetDetailsDtoAsync(user, ticket.Id, cancellationToken);
        }

        public async Task<IReadOnlyList<TicketListItemDto>> GetVisibleTicketsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);
            var visibleTickets = await tickets.ListAsync(TicketVisibility.VisibleTo(user), cancellationToken);

            return visibleTickets.Select(ToListItemDto).ToList();
        }

        public async Task<TicketDetailsDto> GetByIdAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            return await GetDetailsDtoAsync(user, ticketId, cancellationToken);
        }

        public async Task<IReadOnlyList<AuditEntryDto>> GetHistoryAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            // Same rule as the details page: no history for a ticket you can't see
            if (!await tickets.IsVisibleAsync(ticketId, TicketVisibility.VisibleTo(user), cancellationToken))
                throw new NotFoundException("Ticket not found.");

            var entries = await tickets.GetAuditLogsAsync(ticketId, cancellationToken);
            if (user.Role == UserRole.Client)
                entries = entries.Where(e => ClientVisibleActions.Contains(e.Action)).ToList();

            var names = await users.GetNamesAsync(entries.Select(e => e.ActorId).Distinct(), cancellationToken);

            return entries
                .Select(e => new AuditEntryDto(
                    e.Id,
                    e.CreatedAt,
                    names.GetValueOrDefault(e.ActorId, "Unknown user"), // The log outlives users on purpose
                    e.Action,
                    e.OldValue,
                    e.NewValue))
                .ToList();
        }

        private async Task<TicketDetailsDto> GetDetailsDtoAsync(User user, Guid ticketId, CancellationToken cancellationToken)
        {
            var ticket = await tickets.GetDetailsAsync(ticketId, TicketVisibility.VisibleTo(user), cancellationToken);

            // Same answer for "doesn't exist" and "not yours", so nobody can probe which ticket ids exist
            return ticket is null
                ? throw new NotFoundException("Ticket not found.")
                : ToDetailsDto(ticket, user);
        }

        private static TicketListItemDto ToListItemDto(Ticket ticket) => new(
            ticket.Id,
            ticket.Title,
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.Client.Name,
            ticket.Team?.Name,
            ticket.CreatedAt);

        private static TicketDetailsDto ToDetailsDto(Ticket ticket, User user)
        {
            // A client only sees the response once the manager has sent it, never the team's drafts
            var responses = user.Role == UserRole.Client
                ? ticket.Responses.Where(r => r.Status == ResponseStatus.Sent)
                : ticket.Responses;

            return new TicketDetailsDto(
                ticket.Id,
                ticket.Title,
                ticket.Description,
                ticket.RepositoryUrl,
                ticket.Status.ToString(),
                ticket.Priority.ToString(),
                ticket.CreatedAt,
                ticket.ClosedAt,
                ToUserDto(ticket.Client),
                ticket.Manager is null ? null : ToUserDto(ticket.Manager),
                ticket.TeamId,
                ticket.Team?.Name,
                ticket.Assignments.Select(a => ToUserDto(a.User)).ToList(),
                SnapshotsVisibleTo(ticket, user).OrderBy(s => s.CreatedAt).Select(ToSnapshotDto).ToList(),
                responses.OrderBy(r => r.CreatedAt).Select(ToResponseDto).ToList(),
                TicketWorkflow.AllowedActions(user, ticket).Select(a => a.ToString()).ToList());
        }

        // Same idea as responses: a client sees the original code and only the fixes that were
        // actually sent to them, not attempts still in review or sent back by the team
        private static IEnumerable<CodeSnapshot> SnapshotsVisibleTo(Ticket ticket, User user)
        {
            if (user.Role != UserRole.Client)
                return ticket.Snapshots;

            var fixes = ticket.Snapshots.Where(s => s.Kind == SnapshotKind.After).ToList();

            // Submit saves the After snapshot and its response with the same CreatedAt,
            // so each sent response's fix is the latest snapshot not newer than it
            var sentFixes = ticket.Responses
                .Where(r => r.Status == ResponseStatus.Sent)
                .Select(r => fixes.Where(s => s.CreatedAt <= r.CreatedAt).MaxBy(s => s.CreatedAt))
                .OfType<CodeSnapshot>();

            return ticket.Snapshots.Where(s => s.Kind == SnapshotKind.Before).Concat(sentFixes).Distinct();
        }

        private static TicketUserDto ToUserDto(User user) => new(user.Id, user.Name);

        private static CodeSnapshotDto ToSnapshotDto(CodeSnapshot snapshot) => new(
            snapshot.Id,
            snapshot.Kind.ToString(),
            snapshot.RepositoryUrl,
            snapshot.Branch,
            snapshot.CommitSha,
            snapshot.CreatedAt);

        private static TicketResponseDto ToResponseDto(TicketResponse response) => new(
            response.Id,
            response.Summary,
            response.Status.ToString(),
            ToUserDto(response.Author),
            response.CreatedAt,
            response.SentAt);
    }
}
