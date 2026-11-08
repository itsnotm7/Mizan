using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    public class TicketService(ITicketRepository tickets, IUserRepository users) : ITicketService
    {
        public async Task<TicketDetailsDto> CreateAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken)
        {
            var user = await GetActingUserAsync(userId, cancellationToken);

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
            await tickets.SaveChangesAsync(cancellationToken);

            return await GetDetailsDtoAsync(user, ticket.Id, cancellationToken);
        }

        public async Task<IReadOnlyList<TicketListItemDto>> GetVisibleTicketsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await GetActingUserAsync(userId, cancellationToken);
            var visibleTickets = await tickets.ListAsync(TicketVisibility.VisibleTo(user), cancellationToken);

            return visibleTickets.Select(ToListItemDto).ToList();
        }

        public async Task<TicketDetailsDto> GetByIdAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken)
        {
            var user = await GetActingUserAsync(userId, cancellationToken);

            return await GetDetailsDtoAsync(user, ticketId, cancellationToken);
        }

        // JwtBearer only checks the token, so a deactivated user's token still passes it.
        // Reading the user here means the role and status are always the current ones.
        private async Task<User> GetActingUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await users.GetByIdAsync(userId, cancellationToken);

            if (user is null || user.IsDeleted || !user.IsActive)
                throw new UnauthorizedException("This account is no longer active.");

            return user;
        }

        private async Task<TicketDetailsDto> GetDetailsDtoAsync(User user, Guid ticketId, CancellationToken cancellationToken)
        {
            var ticket = await tickets.GetDetailsAsync(ticketId, TicketVisibility.VisibleTo(user), cancellationToken);

            // Same answer for "doesn't exist" and "not yours", so nobody can probe which ticket ids exist
            return ticket is null
                ? throw new NotFoundException("Ticket not found.")
                : ToDetailsDto(ticket);
        }

        private static TicketListItemDto ToListItemDto(Ticket ticket) => new(
            ticket.Id,
            ticket.Title,
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.Client.Name,
            ticket.Team?.Name,
            ticket.CreatedAt);

        private static TicketDetailsDto ToDetailsDto(Ticket ticket) => new(
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
            ticket.Snapshots.OrderBy(s => s.CreatedAt).Select(ToSnapshotDto).ToList());

        private static TicketUserDto ToUserDto(User user) => new(user.Id, user.Name);

        private static CodeSnapshotDto ToSnapshotDto(CodeSnapshot snapshot) => new(
            snapshot.Id,
            snapshot.Kind.ToString(),
            snapshot.RepositoryUrl,
            snapshot.Branch,
            snapshot.CommitSha,
            snapshot.CreatedAt);
    }
}
