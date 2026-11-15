using TicketingSystem.Application.Services;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Tests
{
    // TicketVisibility.VisibleTo returns an Expression: a rule EF Core turns into SQL.
    // Here there's no database, so we Compile() it into a normal C# function and run it
    // on tickets in memory. Same rule, same answers.
    public class TicketVisibilityTests
    {
        private readonly TestData data = new();

        private static bool CanSee(User user, Ticket ticket) =>
            TicketVisibility.VisibleTo(user).Compile()(ticket);

        [Fact]
        public void Client_SeesOwnTicket_ButNotAnotherClients()
        {
            var ticket = data.TicketIn(TicketStatus.InProgress);

            Assert.True(CanSee(data.Client, ticket));
            Assert.False(CanSee(data.OtherClient, ticket));
        }

        [Theory]
        [InlineData(TicketStatus.Submitted)]
        [InlineData(TicketStatus.ManagerReview)]
        [InlineData(TicketStatus.Closed)]
        public void ManagersAndAdmin_SeeEveryTicket(TicketStatus status)
        {
            var ticket = data.TicketIn(status);

            Assert.True(CanSee(data.Manager, ticket));
            Assert.True(CanSee(data.OtherManager, ticket));
            Assert.True(CanSee(data.Admin, ticket));
        }

        [Fact]
        public void Leader_SeesOwnTeamsTicket_ButNotAnotherTeams()
        {
            var ticket = data.TicketIn(TicketStatus.Routed);

            Assert.True(CanSee(data.Leader, ticket));
            Assert.False(CanSee(data.OtherLeader, ticket));
        }

        [Fact]
        public void Leader_SeesNothingBeforeRouting()
        {
            // A new ticket has no team yet, so no leader can see it
            var ticket = data.TicketIn(TicketStatus.Submitted);

            Assert.False(CanSee(data.Leader, ticket));
        }

        [Fact]
        public void Member_SeesOnlyTicketsAssignedToThem()
        {
            var ticket = data.TicketIn(TicketStatus.InProgress);

            // OtherMember isn't assigned, so the ticket doesn't exist for them (the API answers 404)
            Assert.True(CanSee(data.Member, ticket));
            Assert.False(CanSee(data.OtherMember, ticket));
        }

        [Fact]
        public void Member_DoesNotSeeTicket_WhenTheirAssignmentIsDeleted()
        {
            var ticket = data.TicketIn(TicketStatus.InProgress);
            ticket.Assignments.Single().IsDeleted = true;

            Assert.False(CanSee(data.Member, ticket));
        }

        [Fact]
        public void UnknownRole_SeesNothing()
        {
            // A role added later sees nothing until someone writes its rule: safe by default
            var stranger = new User { Name = "Stranger", Role = (UserRole)99 };

            Assert.False(CanSee(stranger, data.TicketIn(TicketStatus.Submitted)));
        }
    }
}
