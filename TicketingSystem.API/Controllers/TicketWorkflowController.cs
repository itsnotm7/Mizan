using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    // One POST per workflow step. Who may call each one is decided in TicketWorkflow,
    // not here, so the rules live in one place. Each returns the updated ticket.
    [ApiController]
    [Route("api/tickets/{id:guid}")]
    public class TicketWorkflowController(ITicketWorkflowService workflow) : ControllerBase
    {
        [HttpPost("route")]
        public async Task<ActionResult<TicketDetailsDto>> RouteToTeam(Guid id, RouteTicketRequest request, CancellationToken cancellationToken) =>
            Ok(await workflow.RouteAsync(User.GetUserId(), id, request, cancellationToken));

        [HttpPost("reject")]
        public async Task<ActionResult<TicketDetailsDto>> Reject(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.RejectAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("assign")]
        public async Task<ActionResult<TicketDetailsDto>> Assign(Guid id, AssignMembersRequest request, CancellationToken cancellationToken) =>
            Ok(await workflow.AssignAsync(User.GetUserId(), id, request, cancellationToken));

        [HttpPost("submit")]
        public async Task<ActionResult<TicketDetailsDto>> Submit(Guid id, SubmitFixRequest request, CancellationToken cancellationToken) =>
            Ok(await workflow.SubmitAsync(User.GetUserId(), id, request, cancellationToken));

        [HttpPost("leader-approve")]
        public async Task<ActionResult<TicketDetailsDto>> LeaderApprove(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.LeaderApproveAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("leader-send-back")]
        public async Task<ActionResult<TicketDetailsDto>> LeaderSendBack(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.LeaderSendBackAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("manager-approve")]
        public async Task<ActionResult<TicketDetailsDto>> ManagerApprove(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.ManagerApproveAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("manager-send-back")]
        public async Task<ActionResult<TicketDetailsDto>> ManagerSendBack(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.ManagerSendBackAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("close")]
        public async Task<ActionResult<TicketDetailsDto>> Close(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.CloseAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("reopen")]
        public async Task<ActionResult<TicketDetailsDto>> Reopen(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.ReopenAsync(User.GetUserId(), id, cancellationToken));
    }
}
