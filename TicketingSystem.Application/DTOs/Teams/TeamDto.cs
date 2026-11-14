using TicketingSystem.Application.DTOs.Tickets;

namespace TicketingSystem.Application.DTOs.Teams
{
    // Used by the manager to pick a team and by the leader to pick members
    public record TeamDto(Guid Id, string Name, TicketUserDto Leader, IReadOnlyList<TicketUserDto> Members);
}
