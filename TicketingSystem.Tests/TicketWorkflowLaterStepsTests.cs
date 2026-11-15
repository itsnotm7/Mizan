using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Services;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Tests
{
    // Tests for the rest of the rules table (TicketWorkflow.cs), following the same pattern as
    // TicketWorkflowRouteRejectAssignTests.cs. Every action gets the 3 kinds of test:
    //   1. right person + right status  -> the status changes (and Apply returns the old status)
    //   2. wrong person                 -> ForbiddenException (403)
    //      We pick the "sneaky" wrong person: the SAME role, but not connected to this ticket
    //      (data.OtherLeader, data.OtherManager, data.OtherMember, data.OtherClient).
    //      That proves the check looks at WHICH person, not just the role.
    //   3. right person + wrong status  -> ConflictException (409), and nothing changes
    //
    // Checklist:
    //   [x] Submit          InProgress    -> LeaderReview   by the ASSIGNED member
    //   [x] LeaderApprove   LeaderReview  -> ManagerReview  by the leader of this ticket's team
    //   [x] LeaderSendBack  LeaderReview  -> InProgress     by the leader of this ticket's team
    //   [x] ManagerApprove  ManagerReview -> ResponseSent   by the manager who ROUTED the ticket
    //   [x] ManagerSendBack ManagerReview -> InProgress     by the manager who ROUTED the ticket
    //   [x] Close           ResponseSent  -> Closed         by the client who OWNS the ticket
    //   [x] Reopen          ResponseSent  -> Reopened       by the client who OWNS the ticket
    //   each with its Forbidden test and its Conflict test
    //
    // data.TicketIn(status) gives a ticket already set up for that status
    // (team, manager and assigned member filled in). Run with: dotnet test
    public class TicketWorkflowLaterStepsTests
    {
        // A fresh TestData for every test (xUnit makes a new instance of the class per test)
        private readonly TestData data = new();

        // ---------- Submit: InProgress -> LeaderReview, by a member ASSIGNED to this ticket ----------

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

        [Fact]
        public void Submit_ByMemberNotAssigned_IsForbidden()
        {
            // OtherMember is a TeamMember, but nobody assigned them to this ticket
            var ticket = data.TicketIn(TicketStatus.InProgress);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherMember, ticket, WorkflowAction.Submit));
            Assert.Equal(TicketStatus.InProgress, ticket.Status);
        }

        [Fact]
        public void Submit_ByMemberWhoseAssignmentWasRemoved_IsForbidden()
        {
            // A soft-deleted assignment doesn't count: the rule checks !a.IsDeleted
            var ticket = data.TicketIn(TicketStatus.InProgress);
            ticket.Assignments.Single().IsDeleted = true;

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.Submit));
        }

        [Theory]
        [InlineData(TicketStatus.LeaderReview)] // already submitted: can't submit twice
        [InlineData(TicketStatus.ResponseSent)]
        [InlineData(TicketStatus.Closed)]
        public void Submit_ByAssignedMember_WhenNotInProgress_IsConflict(TicketStatus status)
        {
            var ticket = data.TicketIn(status);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.Submit));
            Assert.Equal(status, ticket.Status);
        }

        // ---------- LeaderApprove: LeaderReview -> ManagerReview, by the leader of THIS ticket's team ----------

        [Fact]
        public void LeaderApprove_ByLeaderOfTicketTeam_WhenLeaderReview_MovesToManagerReview()
        {
            var ticket = data.TicketIn(TicketStatus.LeaderReview);

            var oldStatus = TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderApprove);

            Assert.Equal(TicketStatus.ManagerReview, ticket.Status);
            Assert.Equal(TicketStatus.LeaderReview, oldStatus);
        }

        [Fact]
        public void LeaderApprove_ByLeaderOfAnotherTeam_IsForbidden()
        {
            var ticket = data.TicketIn(TicketStatus.LeaderReview);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherLeader, ticket, WorkflowAction.LeaderApprove));
            Assert.Equal(TicketStatus.LeaderReview, ticket.Status);
        }

        [Fact]
        public void LeaderApprove_ByAssignedMember_IsForbidden()
        {
            // The member who wrote the fix can't approve their own work
            var ticket = data.TicketIn(TicketStatus.LeaderReview);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.LeaderApprove));
        }

        [Fact]
        public void LeaderApprove_ByLeader_WhenInProgress_IsConflict()
        {
            // Nothing has been submitted yet, so there is nothing to approve
            var ticket = data.TicketIn(TicketStatus.InProgress);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderApprove));
            Assert.Equal(TicketStatus.InProgress, ticket.Status);
        }

        // ---------- LeaderSendBack: LeaderReview -> InProgress, by the leader of THIS ticket's team ----------

        [Fact]
        public void LeaderSendBack_ByLeaderOfTicketTeam_WhenLeaderReview_MovesToInProgress()
        {
            var ticket = data.TicketIn(TicketStatus.LeaderReview);

            var oldStatus = TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderSendBack);

            Assert.Equal(TicketStatus.InProgress, ticket.Status);
            Assert.Equal(TicketStatus.LeaderReview, oldStatus);
        }

        [Fact]
        public void LeaderSendBack_ByLeaderOfAnotherTeam_IsForbidden()
        {
            var ticket = data.TicketIn(TicketStatus.LeaderReview);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherLeader, ticket, WorkflowAction.LeaderSendBack));
        }

        [Fact]
        public void LeaderSendBack_ByLeader_WhenManagerReview_IsConflict()
        {
            // Once the leader has approved, the ticket is the manager's to review
            var ticket = data.TicketIn(TicketStatus.ManagerReview);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderSendBack));
            Assert.Equal(TicketStatus.ManagerReview, ticket.Status);
        }

        // ---------- ManagerApprove: ManagerReview -> ResponseSent, by the manager who ROUTED the ticket ----------

        [Fact]
        public void ManagerApprove_ByRoutingManager_WhenManagerReview_MovesToResponseSent()
        {
            var ticket = data.TicketIn(TicketStatus.ManagerReview);

            var oldStatus = TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.ManagerApprove);

            Assert.Equal(TicketStatus.ResponseSent, ticket.Status);
            Assert.Equal(TicketStatus.ManagerReview, oldStatus);
        }

        [Fact]
        public void ManagerApprove_ByAnotherManager_IsForbidden()
        {
            // Unlike Route (any manager), the final approval belongs to the manager who routed it
            var ticket = data.TicketIn(TicketStatus.ManagerReview);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherManager, ticket, WorkflowAction.ManagerApprove));
            Assert.Equal(TicketStatus.ManagerReview, ticket.Status);
        }

        [Fact]
        public void ManagerApprove_ByRoutingManager_WhenLeaderReview_IsConflict()
        {
            // The manager can't skip the leader's review
            var ticket = data.TicketIn(TicketStatus.LeaderReview);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.ManagerApprove));
            Assert.Equal(TicketStatus.LeaderReview, ticket.Status);
        }

        // ---------- ManagerSendBack: ManagerReview -> InProgress, by the manager who ROUTED the ticket ----------

        [Fact]
        public void ManagerSendBack_ByRoutingManager_WhenManagerReview_MovesToInProgress()
        {
            var ticket = data.TicketIn(TicketStatus.ManagerReview);

            var oldStatus = TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.ManagerSendBack);

            Assert.Equal(TicketStatus.InProgress, ticket.Status);
            Assert.Equal(TicketStatus.ManagerReview, oldStatus);
        }

        [Fact]
        public void ManagerSendBack_ByAnotherManager_IsForbidden()
        {
            var ticket = data.TicketIn(TicketStatus.ManagerReview);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherManager, ticket, WorkflowAction.ManagerSendBack));
        }

        [Fact]
        public void ManagerSendBack_ByRoutingManager_WhenResponseSent_IsConflict()
        {
            // Once the response has gone to the client, the manager can't pull it back
            var ticket = data.TicketIn(TicketStatus.ResponseSent);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.ManagerSendBack));
            Assert.Equal(TicketStatus.ResponseSent, ticket.Status);
        }

        // ---------- Close: ResponseSent -> Closed, by the client who OWNS the ticket ----------

        [Fact]
        public void Close_ByOwner_WhenResponseSent_MovesToClosed()
        {
            var ticket = data.TicketIn(TicketStatus.ResponseSent);

            var oldStatus = TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Close);

            Assert.Equal(TicketStatus.Closed, ticket.Status);
            Assert.Equal(TicketStatus.ResponseSent, oldStatus);
        }

        [Fact]
        public void Close_ByAnotherClient_IsForbidden()
        {
            var ticket = data.TicketIn(TicketStatus.ResponseSent);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherClient, ticket, WorkflowAction.Close));
            Assert.Equal(TicketStatus.ResponseSent, ticket.Status);
        }

        [Fact]
        public void Close_ByOwner_WhenInProgress_IsConflict()
        {
            // The client can only accept a fix after one has been sent to them
            var ticket = data.TicketIn(TicketStatus.InProgress);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Close));
            Assert.Equal(TicketStatus.InProgress, ticket.Status);
        }

        // ---------- Reopen: ResponseSent -> Reopened, by the client who OWNS the ticket ----------

        [Fact]
        public void Reopen_ByOwner_WhenResponseSent_MovesToReopened()
        {
            var ticket = data.TicketIn(TicketStatus.ResponseSent);

            var oldStatus = TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Reopen);

            Assert.Equal(TicketStatus.Reopened, ticket.Status);
            Assert.Equal(TicketStatus.ResponseSent, oldStatus);
        }

        [Fact]
        public void Reopen_ByAnotherClient_IsForbidden()
        {
            var ticket = data.TicketIn(TicketStatus.ResponseSent);

            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.OtherClient, ticket, WorkflowAction.Reopen));
        }

        [Fact]
        public void Reopen_ByOwner_WhenClosed_IsConflict()
        {
            // Closed is final: the client accepted the fix
            var ticket = data.TicketIn(TicketStatus.Closed);

            Assert.Throws<ConflictException>(() =>
                TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Reopen));
            Assert.Equal(TicketStatus.Closed, ticket.Status);
        }

        // ---------- The whole chain, start to finish ----------

        [Fact]
        public void FullChain_WithBothSendBacksAndAReopen_EndsClosed()
        {
            // Walks one ticket through every step in order, the way the demo does.
            // Each Apply would throw if a step were wrong, so reaching the end proves the path works.
            var ticket = data.TicketIn(TicketStatus.Submitted);

            // Apply only changes the STATUS. The other data each step sets (the team and manager
            // on Route, the assignment on Assign) is TicketWorkflowService's job, so we copy it here.
            TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.Route);
            ticket.TeamId = data.Team.Id;
            ticket.Team = data.Team;
            ticket.ManagerId = data.Manager.Id;
            ticket.Manager = data.Manager;

            TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.Assign);
            data.Assign(ticket, data.Member);

            TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.Submit);
            TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderSendBack);
            TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.Submit);
            TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderApprove);
            TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.ManagerSendBack);
            TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.Submit);
            TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderApprove);
            TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.ManagerApprove);
            TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Reopen);
            TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.Assign);
            TicketWorkflow.Apply(data.Member, ticket, WorkflowAction.Submit);
            TicketWorkflow.Apply(data.Leader, ticket, WorkflowAction.LeaderApprove);
            TicketWorkflow.Apply(data.Manager, ticket, WorkflowAction.ManagerApprove);
            TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Close);

            Assert.Equal(TicketStatus.Closed, ticket.Status);
        }
    }
}
