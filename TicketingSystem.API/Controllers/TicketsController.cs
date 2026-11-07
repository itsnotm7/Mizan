using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    // No [Authorize(Roles=...)] here: that reads the role from the token, which can be up to
    // 2 hours old. TicketService checks the current role in the database instead.
    [ApiController]
    [Route("api/tickets")]
    public class TicketsController(ITicketService ticketService) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<TicketDetailsDto>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
        {
            var ticket = await ticketService.CreateAsync(User.GetUserId(), request, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TicketListItemDto>>> List(CancellationToken cancellationToken)
        {
            var tickets = await ticketService.GetVisibleTicketsAsync(User.GetUserId(), cancellationToken);

            return Ok(tickets);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TicketDetailsDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var ticket = await ticketService.GetByIdAsync(User.GetUserId(), id, cancellationToken);

            return Ok(ticket);
        }
    }
}
