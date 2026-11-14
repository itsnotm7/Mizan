using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Teams;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    [ApiController]
    [Route("api/teams")]
    public class TeamsController(ITeamService teamService) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TeamDto>>> List(CancellationToken cancellationToken)
        {
            var teams = await teamService.GetTeamsAsync(User.GetUserId(), cancellationToken);

            return Ok(teams);
        }
    }
}
