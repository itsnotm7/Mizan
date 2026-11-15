using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Services;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Tests
{
    // ===================== MAHMOUD'S FILE =====================
    // Tests for the rest of the rules table, written by following the pattern in
    // TicketWorkflowRouteRejectAssignTests.cs. For each action, write the 3 kinds of test:
    //   1. right person + right status  -> the status changes (and Apply returns the old status)
    //   2. wrong person                 -> ForbiddenException
    //      Pick the "sneaky" wrong person: SAME role, but not connected to this ticket
    //      (data.OtherLeader, data.OtherManager, data.OtherMember, data.OtherClient).
    //   3. right person + wrong status  -> ConflictException
    //
    // Checklist (tick them off as you go):
    //   [x] Submit          InProgress    -> LeaderReview   by the ASSIGNED member     (example below)
    //   [ ] Submit          wrong person  (a member who is NOT assigned)  -> Forbidden
    //   [ ] Submit          wrong status                                   -> Conflict
    //   [ ] LeaderApprove   LeaderReview  -> ManagerReview  by the leader of this ticket's team
    //   [ ] LeaderSendBack  LeaderReview  -> InProgress     by the leader of this ticket's team
    //   [ ] ManagerApprove  ManagerReview -> ResponseSent   by the manager who ROUTED the ticket
    //   [ ] ManagerSendBack ManagerReview -> InProgress     by the manager who ROUTED the ticket
    //   [ ] Close           ResponseSent  -> Closed         by the client who OWNS the ticket
    //   [ ] Reopen          ResponseSent  -> Reopened       by the client who OWNS the ticket
    //   (each of the last six also needs its Forbidden test and its Conflict test)
    //
    // Tip: data.TicketIn(status) gives you a ticket already set up for that status
    // (team, manager and assigned member filled in). Run with: dotnet test
    public class TicketWorkflowLaterStepsTests
    {
        private readonly TestData data = new();

        [Fact]
        public void Submit_ByAssignedMember_WhenInProgress_MovesToLeaderReview()
        {
            // Arrange: a ticket the leader has assigned to data.Member
            var ticket = data.TicketIn(TicketStatus.InProgress);

            // Act
            var oldStatus = TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.Submit);

            // Assert
            Assert.Equal(TicketStatus.LeaderReview, ticket.Status);
            Assert.Equal(TicketStatus.InProgress, oldStatus);
        }

        // Your tests go here
    }
}
