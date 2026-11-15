using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Tests
{
    // The cast of people every test can use, and a way to build a ticket that has
    // already reached a given status. No database: these are plain C# objects,
    // which is all TicketWorkflow and TicketVisibility need.
    public class TestData
    {
        // The people involved in "our" ticket
        public User Client { get; } = NewUser("Client", UserRole.Client);
        public User Manager { get; } = NewUser("Manager", UserRole.TeamsManager);
        public User Leader { get; } = NewUser("Leader", UserRole.TeamLeader);
        public User Member { get; } = NewUser("Member", UserRole.TeamMember);
        public User Admin { get; } = NewUser("Admin", UserRole.Admin);

        // Same roles, but NOT connected to our ticket: they prove the checks go beyond the role
        public User OtherClient { get; } = NewUser("Other client", UserRole.Client);
        public User OtherManager { get; } = NewUser("Other manager", UserRole.TeamsManager);
        public User OtherLeader { get; } = NewUser("Other leader", UserRole.TeamLeader);
        public User OtherMember { get; } = NewUser("Other member", UserRole.TeamMember);

        public Team Team { get; }

        public TestData()
        {
            Team = new Team { Name = "Backend Team", LeaderId = Leader.Id, Leader = Leader };
        }

        // Every user in the cast, handy for "nobody else may do this" checks
        public IEnumerable<User> Everyone =>
            [Client, Manager, Leader, Member, Admin, OtherClient, OtherManager, OtherLeader, OtherMember];

        // A ticket owned by Client that has reached `status`, set up the way the real
        // workflow would have left it: routed tickets have a team and a manager,
        // and tickets that work has started on have Member assigned.
        public Ticket TicketIn(TicketStatus status)
        {
            var ticket = new Ticket
            {
                Title = "Test ticket",
                RepositoryUrl = "https://github.com/demo/repo",
                Status = status,
                ClientId = Client.Id,
                Client = Client
            };

            if (status is TicketStatus.Submitted or TicketStatus.Rejected)
                return ticket;

            // Routed or later: the manager has sent it to our team
            ticket.ManagerId = Manager.Id;
            ticket.Manager = Manager;
            ticket.TeamId = Team.Id;
            ticket.Team = Team;

            if (status == TicketStatus.Routed)
                return ticket;

            // InProgress or later: the leader has assigned Member
            Assign(ticket, Member);
            return ticket;
        }

        public void Assign(Ticket ticket, User member) =>
            ticket.Assignments.Add(new TicketAssignment
            {
                TicketId = ticket.Id,
                Ticket = ticket,
                UserId = member.Id,
                User = member,
                AssignedById = Leader.Id,
                AssignedBy = Leader
            });

        private static User NewUser(string name, UserRole role) =>
            new() { Name = name, Email = $"{name.Replace(' ', '.').ToLower()}@test.local", Role = role };
    }
}
