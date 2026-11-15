using TicketingSystem.Application.Services;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Tests
{
    // AllowedActions is what the React app turns into buttons.
    // It comes from the same rules table as Apply, so these tests also prove
    // that the buttons a user sees are exactly the actions the server would accept.
    public class TicketWorkflowAllowedActionsTests
    {
        private readonly TestData data = new();

        [Fact]
        public void Submitted_OnlyManagersCanRouteOrReject()
        {
            var ticket = data.TicketIn(TicketStatus.Submitted);

            Assert.Equal([WorkflowAction.Route, WorkflowAction.Reject], TicketWorkflow.AllowedActions(data.Manager, ticket));
            Assert.Equal([WorkflowAction.Route, WorkflowAction.Reject], TicketWorkflow.AllowedActions(data.OtherManager, ticket));
            AssertNobodyElseHasActions(ticket, data.Manager, data.OtherManager);
        }

        [Fact]
        public void Routed_OnlyThisTeamsLeaderCanAssign()
        {
            var ticket = data.TicketIn(TicketStatus.Routed);

            Assert.Equal([WorkflowAction.Assign], TicketWorkflow.AllowedActions(data.Leader, ticket));
            AssertNobodyElseHasActions(ticket, data.Leader);
        }

        [Fact]
        public void InProgress_OnlyTheAssignedMemberCanSubmit()
        {
            var ticket = data.TicketIn(TicketStatus.InProgress);

            Assert.Equal([WorkflowAction.Submit], TicketWorkflow.AllowedActions(data.Member, ticket));
            AssertNobodyElseHasActions(ticket, data.Member);
        }

        [Fact]
        public void LeaderReview_OnlyThisTeamsLeaderCanApproveOrSendBack()
        {
            var ticket = data.TicketIn(TicketStatus.LeaderReview);

            Assert.Equal([WorkflowAction.LeaderApprove, WorkflowAction.LeaderSendBack],
                TicketWorkflow.AllowedActions(data.Leader, ticket));
            AssertNobodyElseHasActions(ticket, data.Leader);
        }

        [Fact]
        public void ManagerReview_OnlyTheManagerWhoRoutedItCanApproveOrSendBack()
        {
            var ticket = data.TicketIn(TicketStatus.ManagerReview);

            Assert.Equal([WorkflowAction.ManagerApprove, WorkflowAction.ManagerSendBack],
                TicketWorkflow.AllowedActions(data.Manager, ticket));
            // OtherManager is a TeamsManager too, but didn't route this ticket
            AssertNobodyElseHasActions(ticket, data.Manager);
        }

        [Fact]
        public void ResponseSent_OnlyTheOwningClientCanCloseOrReopen()
        {
            var ticket = data.TicketIn(TicketStatus.ResponseSent);

            Assert.Equal([WorkflowAction.Close, WorkflowAction.Reopen], TicketWorkflow.AllowedActions(data.Client, ticket));
            AssertNobodyElseHasActions(ticket, data.Client);
        }

        [Theory]
        [InlineData(TicketStatus.Closed)]
        [InlineData(TicketStatus.Rejected)]
        public void FinishedTickets_HaveNoActionsForAnyone(TicketStatus status)
        {
            var ticket = data.TicketIn(status);

            AssertNobodyElseHasActions(ticket);
        }

        [Fact]
        public void Admin_HasNoWorkflowPowers()
        {
            // Admin can see every ticket, but the workflow belongs to the people doing the work
            foreach (var status in Enum.GetValues<TicketStatus>())
                Assert.Empty(TicketWorkflow.AllowedActions(data.Admin, data.TicketIn(status)));
        }

        // Everyone in the cast except `allowed` must get an empty list of actions
        private void AssertNobodyElseHasActions(Ticket ticket, params User[] allowed)
        {
            foreach (var user in data.Everyone.Except(allowed))
                Assert.Empty(TicketWorkflow.AllowedActions(user, ticket));
        }
    }
}
