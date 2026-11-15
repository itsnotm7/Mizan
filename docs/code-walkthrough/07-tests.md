# Chapter 7: Tests: proving the rules hold

The project `TicketingSystem.Tests` checks the two rule classes that matter most, **without a database**:
- `TicketWorkflow` (chapter 3): who may do which action, and from which status.
- `TicketVisibility` (chapter 2): who sees which ticket.

Both are pure C#: they take a `User` and a `Ticket` and give an answer. That makes them the easiest and most valuable thing to unit-test. There's nothing to fake: no database, no HTTP, no mocks.

**How to run them**
- Visual Studio: **Test → Test Explorer → Run All**.
- Command line, from `F:\Ticketing`: `dotnet test`.

The project references **Application** and **Domain** only (the same inward arrows as the rest of the solution). It uses **xUnit**, the test framework from the `dotnet new xunit` template.

| File | What it tests | Who wrote it |
|---|---|---|
| `TestData.cs` | Not a test: builds users and tickets for the tests | Claude |
| `TicketWorkflowRouteRejectAssignTests.cs` | Route, Reject, Assign, and the order of the checks | Claude (worked examples) |
| `TicketWorkflowAllowedActionsTests.cs` | Which buttons each person gets at each status | Claude |
| `TicketVisibilityTests.cs` | Who sees which ticket | Claude |
| `TicketWorkflowLaterStepsTests.cs` | Submit, LeaderApprove/SendBack, ManagerApprove/SendBack, Close, Reopen | **Mahmoud** |

---

## 7.1 `TestData.cs`: the cast and the tickets

```csharp
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
```

- **Lines 12–16:** one user per role. Each gets a new `Guid` Id automatically (from `BaseEntity`).
- **Lines 19–22:** the "Other" users are the important trick. `OtherLeader` **is** a TeamLeader, but not the leader of our ticket's team. If a test lets `OtherLeader` assign and it's refused, we've proved the rule checks *which* leader, not just the role. That's the "permission checks go beyond the role" requirement.

```csharp
        public Ticket TicketIn(TicketStatus status)
        {
            var ticket = new Ticket { ..., Status = status, ClientId = Client.Id, Client = Client };

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
```
*(shortened: the full object initializer is on lines 40–47)*

- **Line 38:** `TicketIn(status)` builds a ticket that looks the way the real workflow would have left it at that status. Each test can jump straight to "a ticket in LeaderReview" without running the steps before it.
- **Lines 49–50:** a Submitted or Rejected ticket was never routed: no team, no manager.
- **Lines 53–56:** from Routed on, it has a manager and our team. `ManagerId` matters because ManagerApprove checks it's **the manager who routed it**. `Team.LeaderId` matters because the leader checks use it.
- **Line 62:** from InProgress on, `Member` is assigned. Submit checks for that assignment.

**Q: Why not use the real database for tests?**
A: The rules don't touch the database. They're plain functions of a user and a ticket. Plain objects make the tests fast (all of them run in under a second) and they never fail because of data left over from another run.

---

## 7.2 `TicketWorkflowRouteRejectAssignTests.cs`: the pattern

Every action gets three kinds of test (lines 8–11 of the file say so):
1. **Right person, right status:** the status changes.
2. **Wrong person:** `ForbiddenException` (the API turns it into 403).
3. **Right person, wrong status:** `ConflictException` (the API turns it into 409).

```csharp
        private readonly TestData data = new();

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
```

- **Line 16:** xUnit creates a **new instance of the class for every test**, so every test gets fresh people and tickets. One test can never break another.
- **Line 20:** `[Fact]` marks a method as a test.
- **Lines 23–31:** **Arrange / Act / Assert**: set up, do the one thing, check the result. Every test follows this shape.
- **Line 31:** `Apply` returns the old status because `RunAsync` writes it into the audit log (`OldValue`). Testing it here protects the audit log too.

```csharp
            Assert.Throws<ForbiddenException>(() =>
                TicketWorkflow.Apply(data.Client, ticket, WorkflowAction.Route));

            // A refused action must not change anything
            Assert.Equal(TicketStatus.Submitted, ticket.Status);
```

- **Lines 51–52:** `Assert.Throws<T>` runs the code inside `() => ...` and passes only if it throws exactly that exception type.
- **Line 55:** a refused action must leave the ticket untouched. `Apply` checks before it changes anything, and this line proves it.

```csharp
        [Theory] // [Theory] = the same test run once per [InlineData] row
        [InlineData(TicketStatus.Routed)]
        [InlineData(TicketStatus.InProgress)]
        [InlineData(TicketStatus.Closed)]
        public void Route_ByManager_WhenNotSubmitted_IsConflict(TicketStatus status)
```

- **Lines 58–62:** a `[Theory]` is one test method run several times with different inputs. Test Explorer shows it as three tests. Use it when only the input changes.

The rest of the file follows the same pattern. Two tests are worth knowing by name:
- **`Assign_ByLeaderOfAnotherTeam_IsForbidden` (line 117):** the right role, the wrong leader. This is the "beyond the role" check.
- **`WhoIsCheckedBeforeStatus_ClientAssigningClosedTicket_IsForbiddenNotConflict` (line 151):** both checks would fail. 403 wins, because "you may never do this" is the permanent answer, while 409 ("not now") would invite a retry.

---

## 7.3 `TicketWorkflowAllowedActionsTests.cs`: the buttons

`AllowedActions` is the list the React app turns into buttons (chapter 6). These tests go through the statuses and check that **exactly one person** (or two managers, for a new ticket) gets buttons, and nobody else gets any.

```csharp
        [Fact]
        public void ManagerReview_OnlyTheManagerWhoRoutedItCanApproveOrSendBack()
        {
            var ticket = data.TicketIn(TicketStatus.ManagerReview);

            Assert.Equal([WorkflowAction.ManagerApprove, WorkflowAction.ManagerSendBack],
                TicketWorkflow.AllowedActions(data.Manager, ticket));
            // OtherManager is a TeamsManager too, but didn't route this ticket
            AssertNobodyElseHasActions(ticket, data.Manager);
        }
```

- **Lines 57–58:** the exact list, in table order.
- **Line 60 and lines 91–95:** the helper loops over **everyone** in the cast except the allowed people and checks each gets an empty list. One line covers eight "nobody else" checks.
- **Lines 83–88:** Admin gets no buttons at any status. Admin can *see* everything, but the workflow belongs to the people doing the work.

**Q: Why test AllowedActions separately if Apply is tested?**
A: They're two questions asked of the same table: "may I?" (Apply) and "what may I?" (AllowedActions). Testing both proves the buttons the user sees match what the server would accept.

---

## 7.4 `TicketVisibilityTests.cs`: who sees what

```csharp
        private static bool CanSee(User user, Ticket ticket) =>
            TicketVisibility.VisibleTo(user).Compile()(ticket);
```

- **Lines 14–15:** `VisibleTo` returns an **Expression**, a description of the rule that EF Core translates into SQL. Without a database, `.Compile()` turns that description into a normal C# function, and `(ticket)` calls it. It's the same rule, run in memory instead of in SQL.

The tests cover every branch of the rule:
- A client sees their own ticket, not another client's (line 18).
- Managers and Admin see every ticket (line 30).
- A leader sees their team's ticket, not another team's (line 40), and nothing before routing (line 49).
- A member sees only tickets assigned to them (line 58), and not after the assignment is deleted (line 68).
- An unknown role sees nothing (line 77): safe by default.

**Q: Does compiling the expression test the same thing as the SQL?**
A: It tests the same *logic*. The SQL translation is EF Core's job, and it was covered by the end-to-end runs against the real API (a member asking for an unassigned ticket gets 404).

---

## 7.5 `TicketWorkflowLaterStepsTests.cs`: Mahmoud's tests

The rest of the rules table. The file starts with a checklist comment and one finished example (`Submit_ByAssignedMember_WhenInProgress_MovesToLeaderReview`) to copy. *(This section gets the line-by-line once the tests are written.)*

---

## 7.6 Proof the tests test something

A test that can't fail is worthless. To check, we broke one rule on purpose: Route was allowed from `Routed` as well as `Submitted`. Two tests failed straight away:
- `Route_ByManager_WhenNotSubmitted_IsConflict(status: Routed)`
- `Routed_OnlyThisTeamsLeaderCanAssign` (a manager suddenly had a Route button on a routed ticket)

Then the rule was put back and `git diff` confirmed `TicketWorkflow.cs` was unchanged. This trick is called **mutation testing**.

## What the tests don't cover (and why that's OK)

- **EF Core queries, controllers, JSON, JWT:** covered by the end-to-end runs through the API and the browser (67 checks plus the full chain in the UI).
- **The next step** would be integration tests with `WebApplicationFactory`, which starts the real API in memory against a test database. That's future work.
