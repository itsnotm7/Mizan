using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Services;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Tests
{
    // Worked examples for the first three rows of the rules table (TicketWorkflow.cs).
    // Every action gets the same three kinds of test:
    //   1. right person + right status  -> the status changes
    //   2. wrong person                 -> ForbiddenException (the API turns it into 403)
    //   3. right person + wrong status  -> ConflictException  (the API turns it into 409)
    public class TicketWorkflowRouteRejectAssignTests
    {
        // xUnit creates a new instance of this class for EVERY test,
        // so each test gets fresh people and tickets and can't affect another test
        private readonly TestData data = new();

        // ---------- Route: Submitted -> Routed, by any TeamsManager ----------

        [Fact] // [Fact] = one test with no inputs
        public void Route_ByManager_WhenSubmitted_MovesToRouted()
        {
            // Arrange: a brand-new ticket
            var ticket = data.TicketIn(TicketStatus.Submitted);

            // Act
            var oldStatus = TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.Route);

            // Assert: the new status is set, and the old one comes back for the audit log
            Assert.Equal(TicketStatus.Routed, ticket.Status);
            Assert.Equal(TicketStatus.Submitted, oldStatus);
        }

        [Fact]
        public void Route_ByAnyManager_IsAllowed()
        {
            // Routing is the one step any manager may take: nobody "owns" a new ticket yet
            var ticket = data.TicketIn(TicketStatus.Submitted);

            TicketWorkflow.Apply(data.OtherManager, ticket, WorkflowAction.Route);

            Assert.Equal(TicketStatus.Routed, ticket.Status);
        }

        [Fact]
        public void Route_ByClient_IsForbidden()
        {
            var ticket = data.TicketIn(TicketStatus.Submitted);

            // Assert.Throws runs the code and passes only if it throws exactly this exception
            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Route));

            // A refused action must not change anything
            Assert.Equal(TicketStatus.Submitted, ticket.Status);
        }

        [Theory] // [Theory] = the same test run once per [InlineData] row
        [InlineData(TicketStatus.Routed)]
        [InlineData(TicketStatus.InProgress)]
        [InlineData(TicketStatus.Closed)]
        public void Route_ByManager_WhenNotSubmitted_IsConflict(TicketStatus status)
        {
            var ticket = data.TicketIn(status);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.Route));
            Assert.Equal(status, ticket.Status);
        }

        // ---------- Reject: Submitted -> Rejected, by any TeamsManager ----------

        [Fact]
        public void Reject_ByManager_WhenSubmitted_MovesToRejected()
        {
            var ticket = data.TicketIn(TicketStatus.Submitted);

            TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.Reject);

            Assert.Equal(TicketStatus.Rejected, ticket.Status);
        }

        [Fact]
        public void Reject_ByLeader_IsForbidden()
        {
            var ticket = data.TicketIn(TicketStatus.Submitted);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.Reject));
        }

        [Fact]
        public void Reject_ByManager_WhenAlreadyRouted_IsConflict()
        {
            var ticket = data.TicketIn(TicketStatus.Routed);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.Reject));
        }

        // ---------- Assign: Routed or Reopened -> InProgress, by the leader of THIS ticket's team ----------

        [Theory]
        [InlineData(TicketStatus.Routed)]
        [InlineData(TicketStatus.Reopened)] // a reopened ticket goes back to work the same way
        public void Assign_ByLeaderOfTicketTeam_MovesToInProgress(TicketStatus status)
        {
            var ticket = data.TicketIn(status);

            var oldStatus = TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.Assign);

            Assert.Equal(TicketStatus.InProgress, ticket.Status);
            Assert.Equal(status, oldStatus);
        }

        [Fact]
        public void Assign_ByLeaderOfAnotherTeam_IsForbidden()
        {
            // The role is right (TeamLeader), but this ticket's team has a different leader.
            // This is the "beyond the role" check.
            var ticket = data.TicketIn(TicketStatus.Routed);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherLeader, ticket, WorkflowAction.Assign));
        }

        [Fact]
        public void Assign_BeforeRouting_IsForbidden_BecauseThereIsNoTeamYet()
        {
            // No team means no team leader, so nobody passes the "who" check.
            // Who is checked before status, so this is Forbidden, not Conflict.
            var ticket = data.TicketIn(TicketStatus.Submitted);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.Assign));
        }

        [Fact]
        public void Assign_ByLeader_WhenClosed_IsConflict()
        {
            // The right leader, but a closed ticket can't go back to work
            var ticket = data.TicketIn(TicketStatus.Closed);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.Assign));
        }

        // ---------- The order of the checks ----------

        [Fact]
        public void WhoIsCheckedBeforeStatus_ClientAssigningClosedTicket_IsForbiddenNotConflict()
        {
            // Both checks would fail here. "You may never do this" (403) is the permanent
            // answer, so it wins over "not now" (409), which would suggest trying again later.
            var ticket = data.TicketIn(TicketStatus.Closed);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Assign));
        }
    }
}
