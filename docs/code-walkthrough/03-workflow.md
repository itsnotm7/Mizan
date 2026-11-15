# 3. The workflow: the heart of Mizan

This chapter is the one the committee will dig into. A ticket moves through a fixed set of statuses. Each move (an "arrow") can be made only by one specific person, and only when the ticket is in the right status. All of that is decided in **one place**: the rules table in `TicketWorkflow.cs`. Everything else in this chapter either feeds that table or saves its result.

## The status diagram

```
                         Client creates the ticket
                                    |
                                    v
                              [ Submitted ]
                               /         \
        TeamsManager: Route   /           \   TeamsManager: Reject
                             v             v
                        [ Routed ]     [ Rejected ]   (end)
                             |
                             |  Leader of THIS team: Assign (>= 1 member)
                             v
         +------------> [ InProgress ] <-----------------------------+
         |                   |                                       |
         |                   |  Assigned member: Submit              |
         |                   |  (After snapshot + Draft response)    |
         |                   v                                       |
         |  Leader:     [ LeaderReview ]                             |
         |  SendBack <------/    |                                   |
         |                       |  Leader of THIS team: LeaderApprove
         |                       v                                   |
         |               [ ManagerReview ] ---- Routing manager: ----+
         |                       |              ManagerSendBack
         |                       |  Routing manager: ManagerApprove
         |                       |  (Draft response becomes Sent)
         |                       v
         |               [ ResponseSent ]
         |                /            \
         |  Owner client: Close     Owner client: Reopen
         |              v                \
         |         [ Closed ] (end)    [ Reopened ]
         |                                  |
         +----------------------------------+
            Leader of THIS team: Assign (keeps or adds members)
```

Who does each arrow, in one list:

| Arrow | Action | Who |
|---|---|---|
| Submitted -> Routed | `Route` | Any TeamsManager (who then becomes "the routing manager") |
| Submitted -> Rejected | `Reject` | Any TeamsManager |
| Routed or Reopened -> InProgress | `Assign` | The leader of the ticket's team |
| InProgress -> LeaderReview | `Submit` | A member assigned to this ticket |
| LeaderReview -> ManagerReview | `LeaderApprove` | The leader of the ticket's team |
| LeaderReview -> InProgress | `LeaderSendBack` | The leader of the ticket's team |
| ManagerReview -> ResponseSent | `ManagerApprove` | The manager who routed the ticket |
| ManagerReview -> InProgress | `ManagerSendBack` | The manager who routed the ticket |
| ResponseSent -> Closed | `Close` | The client who owns the ticket |
| ResponseSent -> Reopened | `Reopen` | The client who owns the ticket |

## The files in this chapter

| File | Layer | Role |
|---|---|---|
| [WorkflowAction.cs](../../TicketingSystem.Domain/Enums/WorkflowAction.cs) | Domain | The list of the 10 actions (the names of the arrows). |
| [TicketWorkflow.cs](../../TicketingSystem.Application/Services/TicketWorkflow.cs) | Application | **The rules table.** Decides if an action is allowed and changes the status. |
| [TicketWorkflowService.cs](../../TicketingSystem.Application/Services/TicketWorkflowService.cs) | Application | The 6-step pipeline (`RunAsync`) and the step-specific work for each action. |
| [ITicketWorkflowService.cs](../../TicketingSystem.Application/Interfaces/ITicketWorkflowService.cs) | Application | The interface the controller depends on. |
| [WorkflowRequests.cs](../../TicketingSystem.Application/DTOs/Tickets/WorkflowRequests.cs) | Application | The request bodies for Route, Assign and Submit. |
| [TicketWorkflowController.cs](../../TicketingSystem.API/Controllers/TicketWorkflowController.cs) | API | 10 thin POST endpoints. |

Supporting code from other chapters that this chapter relies on: `TicketVisibility` (who can see which ticket, chapter 2), `ActingUserExtensions.GetActingUserAsync` (load the caller fresh, chapter 2), `TicketRepository` (EF Core, chapter 4), `GlobalExceptionHandler` (exceptions to status codes, chapter 5).

How exceptions become HTTP codes (from `GlobalExceptionHandler`), because you will see them all over this chapter:

| Exception | HTTP | Meaning in the workflow |
|---|---|---|
| `UnauthorizedException` | 401 | Your account is deleted or deactivated. |
| `NotFoundException` | 404 | The ticket doesn't exist, **or you can't see it**. |
| `ForbiddenException` | 403 | You can see the ticket, but you are never the right person for this action. |
| `ConflictException` | 409 | Right person, wrong moment (wrong status), or someone saved the ticket at the same time. |
| `BadRequestException` | 400 | The data you sent is wrong (unknown team, member not on the team, ...). |

---

## [WorkflowAction.cs](../../TicketingSystem.Domain/Enums/WorkflowAction.cs)

**What it is:** an enum listing every step a user can take on a ticket. It lives in Domain because it is part of the business vocabulary, like `TicketStatus`.

**The code**

```csharp
namespace TicketingSystem.Domain.Enums
{
    // Every step a user can take on a ticket. Each one maps to one rule in TicketWorkflow.
    public enum WorkflowAction
    {
        Route, Reject,
        Assign, Submit,
        LeaderApprove, LeaderSendBack,
        ManagerApprove, ManagerSendBack,
        Close, Reopen
    }
}
```

**Line by line**
- **Line 1:** the namespace. Domain has no dependencies on any other project.
- **Line 3:** the comment states the contract: one enum value = one rule in the table.
- **Line 4:** an `enum` is a named set of constants. Under the hood each value is a number (Route = 0, Reject = 1, ...), but we always use the names.
- **Lines 6-10:** the 10 actions, grouped in pairs by who does them (manager, leader/member, leader, manager, client). The grouping is only for reading; the compiler ignores the line breaks.

Why an enum and not strings? The compiler checks the names. A typo like `WorkflowAction.Aproove` doesn't compile, while a typo in a string would only fail at runtime. The names are also reused as text: `action.ToString()` becomes the `Action` column in the audit log and the strings in `allowedActions` sent to React.

---

## [TicketWorkflow.cs](../../TicketingSystem.Application/Services/TicketWorkflow.cs)

**What it is:** the rules table. It is the **one place** in the whole project that decides which status changes exist and who may make them. Each row of the table is one arrow from the diagram above. It is a `static class`: it has no state and no dependencies, it is just rules, which also makes it easy to unit-test later.

### Part 1: the usings and the class

```csharp
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    // The ONE place that decides which status changes exist and who may make them.
    // Each line of the table is one arrow of the workflow in CLAUDE.md.
    public static class TicketWorkflow
    {
```

**Line by line**
- **Lines 1-3:** we need our exceptions (`ForbiddenException`, `ConflictException`), the entities (`User`, `Ticket`) and the enums. Note there is no EF Core and no ASP.NET here. Pure C#.
- **Line 5:** Application layer, `Services` folder.
- **Lines 7-8:** the comment is the design decision in one sentence. If the committee asks "where are the workflow rules?", the answer is "here, and only here".
- **Line 9:** `static class` means you can't create an instance (`new TicketWorkflow()` doesn't compile). You call its methods directly: `TicketWorkflow.Apply(...)`. It has no fields that change, so it is safe to share across all requests.

### Part 2: the rule type and the table

```csharp
        private record Rule(TicketStatus[] From, TicketStatus To, Func<User, Ticket, bool> IsAllowed);

        private static readonly Dictionary<WorkflowAction, Rule> Rules = new()
        {
            [WorkflowAction.Route]           = new([TicketStatus.Submitted], TicketStatus.Routed, IsTeamsManager),
            [WorkflowAction.Reject]          = new([TicketStatus.Submitted], TicketStatus.Rejected, IsTeamsManager),
            [WorkflowAction.Assign]          = new([TicketStatus.Routed, TicketStatus.Reopened], TicketStatus.InProgress, IsLeaderOfTicketTeam),
            [WorkflowAction.Submit]          = new([TicketStatus.InProgress], TicketStatus.LeaderReview, IsAssignedMember),
            [WorkflowAction.LeaderApprove]   = new([TicketStatus.LeaderReview], TicketStatus.ManagerReview, IsLeaderOfTicketTeam),
            [WorkflowAction.LeaderSendBack]  = new([TicketStatus.LeaderReview], TicketStatus.InProgress, IsLeaderOfTicketTeam),
            [WorkflowAction.ManagerApprove]  = new([TicketStatus.ManagerReview], TicketStatus.ResponseSent, IsRoutingManager),
            [WorkflowAction.ManagerSendBack] = new([TicketStatus.ManagerReview], TicketStatus.InProgress, IsRoutingManager),
            [WorkflowAction.Close]           = new([TicketStatus.ResponseSent], TicketStatus.Closed, IsOwner),
            [WorkflowAction.Reopen]          = new([TicketStatus.ResponseSent], TicketStatus.Reopened, IsOwner)
        };
```

**Line by line**
- **Line 11:** a **record** is a short way to declare a small class that just holds data. `record Rule(A, B, C)` creates a class with three read-only properties (`From`, `To`, `IsAllowed`) and a constructor that takes them in that order. It is `private` and nested inside `TicketWorkflow`, so nobody outside this file even knows the type exists. The three parts of a rule:
  - `TicketStatus[] From`: the statuses this arrow can start from. An array, because `Assign` starts from two statuses.
  - `TicketStatus To`: where the arrow ends.
  - `Func<User, Ticket, bool> IsAllowed`: a **function stored in a variable**. `Func<User, Ticket, bool>` means "a function that takes a `User` and a `Ticket` and returns a `bool`". So each rule carries its own "who" check that we can call later like a method: `rule.IsAllowed(user, ticket)`.
- **Line 13:** the table itself, a `Dictionary` from action to rule. `private static readonly`: only this class sees it, there is one copy for the whole app, and the variable can't be pointed at another dictionary after it is built. `new()` is target-typed `new`: the compiler knows the type from the left side, so we don't repeat `Dictionary<WorkflowAction, Rule>`.
- **Lines 14 and 25:** the braces after `new()` are a **dictionary initializer**. Each `[key] = value` line adds one entry, exactly like writing `Rules[key] = value;` ten times.
- **Lines 15-24:** the 10 rows. Read each one left to right as a sentence. Line 17, for example: "**Assign** is allowed **from Routed or Reopened**, goes **to InProgress**, and only **IsLeaderOfTicketTeam** may do it."
  - `new(...)` again is target-typed: it calls the `Rule` constructor.
  - `[TicketStatus.Submitted]` is a **collection expression** (C# 12). The square brackets build the `TicketStatus[]` array. `[TicketStatus.Routed, TicketStatus.Reopened]` on line 17 builds an array with two items. It is short for `new TicketStatus[] { TicketStatus.Routed, TicketStatus.Reopened }`.
  - `IsTeamsManager`, `IsLeaderOfTicketTeam`, ... (no parentheses!) are **static methods used as values**. This is called a **method group**. We don't call the method here; we hand the method itself to the rule, and the compiler turns it into a `Func<User, Ticket, bool>` because the signatures match (`User`, `Ticket` in, `bool` out). The methods are defined at the bottom of the file (lines 57-70).
- The column alignment (extra spaces after `]`) is only for reading the table like a table.

Every arrow in CLAUDE.md's "Workflow" section matches exactly one of these lines. If the business wanted a new arrow (say "Leader can reject"), you add one enum value and one line here. Nothing else decides transitions.

### Part 3: CanPerform and AllowedActions

```csharp
        public static bool CanPerform(User user, Ticket ticket, WorkflowAction action)
        {
            var rule = Rules[action];
            return rule.From.Contains(ticket.Status) && rule.IsAllowed(user, ticket);
        }

        // Sent to the frontend so it shows only the buttons this user can use right now.
        // It comes from the same table, so the buttons and the server can never disagree.
        public static IReadOnlyList<WorkflowAction> AllowedActions(User user, Ticket ticket) =>
            Enum.GetValues<WorkflowAction>().Where(action => CanPerform(user, ticket, action)).ToList();
```

**Line by line**
- **Line 27:** "can this user do this action on this ticket right now?" Returns true or false, never throws.
- **Line 29:** look up the rule for the action. `Rules[action]` would throw `KeyNotFoundException` if an action had no rule. All 10 have one.
- **Line 30:** both checks must pass: the ticket's status is one of the `From` statuses **and** the user passes the rule's "who" function. `rule.IsAllowed(user, ticket)` calls the function stored in the record. `&&` stops early: if the status is wrong, the "who" function isn't even called. For a true/false answer the order doesn't matter (both must be true anyway). It only matters in `Apply`, which throws different errors.
- **Lines 33-34:** the reason this method exists: the React app asks "which buttons do I show?" and the answer comes from this same table.
- **Line 35:** `=>` here makes this an **expression-bodied member**: a method whose whole body is one expression. It is the same as `{ return ...; }`. `IReadOnlyList<WorkflowAction>` tells the caller "you get a list you can read but not change".
- **Line 36:**
  - `Enum.GetValues<WorkflowAction>()` returns all 10 values of the enum (Route, Reject, ..., Reopen).
  - `.Where(action => CanPerform(user, ticket, action))` keeps only those this user can perform now. `action => ...` is a lambda (a small inline function).
  - `.ToList()` runs the filter and stores the result.

So for a TeamsManager looking at a Submitted ticket, the result is `[Route, Reject]`. For the same manager on an InProgress ticket, it is `[]`. `TicketService.ToDetailsDto` calls this and puts the names in `allowedActions` of the ticket details. Because the buttons and the server use the **same table**, the UI can't show a button the server would refuse, and the server can't allow something the UI hides. If someone bypasses the UI (Swagger, curl), `Apply` still checks everything.

Note: if a new value is added to `WorkflowAction` without a row in `Rules`, `AllowedActions` would hit `Rules[action]` and throw, so every ticket details call would fail with 500. That is loud on purpose in practice, but worth knowing.

### Part 4: Apply, the method that actually changes the status

```csharp
        // Checks the rule, changes the status, and returns the old one for the audit log
        public static TicketStatus Apply(User user, Ticket ticket, WorkflowAction action)
        {
            var rule = Rules[action];

            // Who first: a user who may never do this gets 403, whatever the status
            if (!rule.IsAllowed(user, ticket))
                throw new ForbiddenException($"You can't {action} this ticket.");

            if (!rule.From.Contains(ticket.Status))
                throw new ConflictException($"Can't {action} a ticket that is {ticket.Status}.");

            var oldStatus = ticket.Status;
            ticket.Status = rule.To;
            return oldStatus;
        }
```

**Line by line**
- **Line 38:** the comment lists the three jobs: check, change, return the old status.
- **Line 39:** returns a `TicketStatus`: the status **before** the change, because the audit log needs "old -> new".
- **Line 41:** same lookup as in `CanPerform`.
- **Lines 43-45:** the **who** check comes first. If the user fails it, we throw `ForbiddenException` (403). The message uses string interpolation: `$"...{action}..."` puts the action name into the text, for example "You can't Assign this ticket."
- **Lines 47-48:** only then the **status** check. Wrong status throws `ConflictException` (409), for example "Can't Assign a ticket that is Closed."
- **Lines 50-52:** both checks passed. Remember the old status, set the new one from the rule, return the old one. Note this only changes the C# object in memory. Nothing is in the database until `SaveChangesAsync` runs later in `RunAsync`.

**Why "who" before "status"?** The two errors mean different things:
- 403 means "**never you**". A client pressing `assign` will never be allowed, whatever the status. That is permanent.
- 409 means "**not now**". The right person tried at the wrong moment. Trying again later (after the status changes) could work.

If we checked status first, a client calling `assign` on a Routed ticket would get 403, but on a Closed ticket would get 409, which suggests "try later". That is misleading, and it also leaks information about the status to someone who has no business with the action. Checking "who" first gives a stable, honest answer.

**Both checks must pass.** A leader assigning members to a Closed ticket passes the "who" check (they are the leader of that team) but fails the status check, so 409. A client assigning on a Routed ticket fails the "who" check, so 403.

### Part 5: the "who" functions

```csharp
        // The checks go beyond the role: it must be the leader of THIS ticket's team,
        // a member assigned to THIS ticket, the manager who routed it, the client who owns it
        private static bool IsTeamsManager(User user, Ticket ticket) =>
            user.Role == UserRole.TeamsManager;

        private static bool IsRoutingManager(User user, Ticket ticket) =>
            user.Role == UserRole.TeamsManager && ticket.ManagerId == user.Id;

        private static bool IsLeaderOfTicketTeam(User user, Ticket ticket) =>
            user.Role == UserRole.TeamLeader && ticket.Team != null && ticket.Team.LeaderId == user.Id;

        private static bool IsAssignedMember(User user, Ticket ticket) =>
            user.Role == UserRole.TeamMember && ticket.Assignments.Any(a => a.UserId == user.Id && !a.IsDeleted);

        private static bool IsOwner(User user, Ticket ticket) =>
            user.Role == UserRole.Client && ticket.ClientId == user.Id;
    }
}
```

**Line by line**
- **Lines 55-56:** the key idea. A role alone is not enough. Being "a leader" doesn't let you touch every ticket; you must be the leader of **this** ticket's team.
- **Lines 57-58:** `IsTeamsManager`: only the role. Used for Route and Reject, because a Submitted ticket has no manager yet; any manager can pick it up. It takes a `ticket` parameter it doesn't use, because every rule function must have the same shape `(User, Ticket) -> bool` to fit in `Func<User, Ticket, bool>`.
- **Lines 60-61:** `IsRoutingManager`: a manager **and** the one whose id was saved in `ticket.ManagerId` when they routed it (see `RouteAsync`, line 28 of the service). So another manager can't do the final approval of a ticket they didn't route.
- **Lines 63-64:** `IsLeaderOfTicketTeam`: a leader **and** the ticket has a team **and** that team's `LeaderId` is this user. `ticket.Team != null` protects against a ticket that isn't routed yet; without it, `ticket.Team.LeaderId` would throw a `NullReferenceException`. This needs `Team` to be loaded, which is why `GetForUpdateAsync` does `.Include(t => t.Team)`.
- **Lines 66-67:** `IsAssignedMember`: a member **and** there is an assignment on this ticket for this user that isn't soft-deleted. `.Any(...)` returns true if at least one item matches. Being on the team isn't enough; you must be assigned. This needs `Assignments` loaded (`GetForUpdateAsync` includes them).
- **Lines 69-70:** `IsOwner`: a client **and** the ticket's `ClientId` is this user. Another client can't close your ticket.
- **Lines 71-72:** close the class and the namespace.

Admin appears in none of these functions, so Admin has no workflow powers (a decision recorded in the session notes). Admin can **see** all tickets (visibility rule) but every action returns 403.

**Committee might ask:**
- **Q: Where are the workflow rules enforced?** A: In one place: the `Rules` table in `TicketWorkflow`. Each row is one arrow. Controllers don't check anything; the service just calls `Apply`.
- **Q: Why 403 before 409?** A: 403 is "never you", 409 is "not now". Checking "who" first means a user who can never do an action always gets the same answer, and we don't hint at the status to them.
- **Q: How does the frontend know which buttons to show?** A: Ticket details include `allowedActions`, computed by `AllowedActions` from the same table. UI and server can't disagree. And the server re-checks on every call anyway, so a hidden button is not the security; `Apply` is.
- **Q: Isn't checking the role enough?** A: No. Only the leader of **that** ticket's team can assign, only an **assigned** member can submit, only the manager who **routed** it can do the final review, only the **owner** can close. Each `Is...` function checks the role plus the link to this ticket.
- **Q: What is `Func<User, Ticket, bool>`?** A: A variable that holds a function. Each rule stores its "who" check as a function, so the table can hold behavior, not just data. We pass method names like `IsOwner` (method groups) into it.
- **Q: Why a static class and not a domain method on `Ticket`?** A: CLAUDE.md allows either. A static class keeps the whole table in one readable screen and has no dependencies, so it is easy to test. `Ticket` stays a plain data entity for EF.

---

## [ITicketWorkflowService.cs](../../TicketingSystem.Application/Interfaces/ITicketWorkflowService.cs)

**What it is:** the interface the controller depends on. Application defines it, Application also implements it (`TicketWorkflowService`), and it is registered in `Application/DependencyInjection.cs` with `AddScoped<ITicketWorkflowService, TicketWorkflowService>()`.

**The code**

```csharp
using TicketingSystem.Application.DTOs.Tickets;

namespace TicketingSystem.Application.Interfaces
{
    // One method per workflow step. Each returns the updated ticket.
    public interface ITicketWorkflowService
    {
        Task<TicketDetailsDto> RouteAsync(Guid userId, Guid ticketId, RouteTicketRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> RejectAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> AssignAsync(Guid userId, Guid ticketId, AssignMembersRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> SubmitAsync(Guid userId, Guid ticketId, SubmitFixRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> LeaderApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> LeaderSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ManagerApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ManagerSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> CloseAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ReopenAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
    }
}
```

**Line by line**
- **Line 5:** one method per action, each returns the ticket after the change, so the UI can redraw without a second request.
- **Lines 8-17:** every method takes the caller's id (`userId`, from the JWT), the ticket id, and a `CancellationToken` (cancel the database work if the browser disconnects). Only Route, Assign and Submit need extra data, so only they take a request object. The return type is a DTO (`TicketDetailsDto`), never the `Ticket` entity.

---

## [WorkflowRequests.cs](../../TicketingSystem.Application/DTOs/Tickets/WorkflowRequests.cs) (briefly)

**What it is:** the three request bodies. Validation attributes are checked by `[ApiController]` **before** the controller method runs; a failure returns 400 with field errors and the service is never called.

```csharp
    public class RouteTicketRequest
    {
        // Nullable so a missing teamId fails [Required] instead of arriving as an empty Guid
        [Required]
        public Guid? TeamId { get; set; }
    }

    public class AssignMembersRequest
    {
        // May be empty when continuing a reopened ticket with the members already assigned
        [Required]
        [MaxLength(20)]
        public List<Guid> MemberIds { get; set; } = [];
    }

    public class SubmitFixRequest
    {
        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[0-9a-fA-F]{7,64}$",
            ErrorMessage = "CommitSha must be 7 to 64 hexadecimal characters.")]
        public string CommitSha { get; set; } = string.Empty;

        // What was fixed: this becomes the response the client receives once approved
        [Required]
        [MaxLength(4000)]
        public string Summary { get; set; } = string.Empty;
    }
```

Key points (lines 5-35 of the file):
- **Lines 7-9:** `Guid?` instead of `Guid`. A plain `Guid` can't be null, so a missing `teamId` would arrive as `00000000-...` and `[Required]` would pass. With `Guid?` it arrives as null and fails. That is also why the service can safely write `request.TeamId!.Value`.
- **Lines 14-17:** `MemberIds` can be an empty list (`[]` is a collection expression for an empty list). That is allowed on purpose: on a Reopened ticket the leader can continue with the members already assigned. `[MaxLength(20)]` limits the list size.
- **Lines 26-29:** a commit SHA is checked with a regular expression: 7 to 64 hex characters (short SHA up to SHA-256).
- **Lines 31-34:** `Summary` becomes the text of the `TicketResponse` the client eventually sees.

---

## [TicketWorkflowService.cs](../../TicketingSystem.Application/Services/TicketWorkflowService.cs)

**What it is:** the service behind the 10 endpoints. Every action runs through the same private method, `RunAsync`, which does 6 fixed steps. Each public method only adds the work that is special to its step (for example "set the team" for Route). It never decides **whether** a step is allowed; it asks `TicketWorkflow.Apply`.

### Part 1: usings and the constructor

```csharp
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    // Every step runs the same way (see RunAsync). The methods below only add what is
    // special about their step: TicketWorkflow alone decides whether the step is allowed.
    public class TicketWorkflowService(
        ITicketRepository tickets,
        IUserRepository users,
        ITeamRepository teams,
        ITicketService ticketService) : ITicketWorkflowService
    {
```

**Line by line**
- **Lines 1-5:** DTOs, exceptions, interfaces, entities, enums. Notice: no `Microsoft.EntityFrameworkCore`. The service talks to the database only through interfaces (`ITicketRepository`, ...). That is the Clean Architecture dependency rule: Application defines the interfaces, Infrastructure implements them.
- **Lines 9-10:** the design in one sentence: same pipeline for everyone, and the rules table alone decides.
- **Lines 11-15:** a **primary constructor** (C# 12). The parameters in parentheses after the class name are the constructor. ASP.NET Core's dependency injection creates the service and passes in the four dependencies. Inside the class we use them directly by name (`tickets`, `users`, ...), no fields to declare.
  - `tickets`: load the ticket for update, add rows, save.
  - `users`: load the caller fresh.
  - `teams`: load a team with its leader and members (Route and Assign).
  - `ticketService`: reuse the existing "ticket details" code to build the return value.
  - `: ITicketWorkflowService` means this class implements the interface above.

All four are registered as **scoped**, and so is the `AppDbContext`. Scoped means one instance per HTTP request. So every repository in one request shares the **same** DbContext, and that DbContext tracks every change of this request until the one `SaveChangesAsync`.

### Part 2: RouteAsync

```csharp
        public Task<TicketDetailsDto> RouteAsync(Guid userId, Guid ticketId, RouteTicketRequest request, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Route, async (ticket, user) =>
            {
                var team = await teams.GetWithMembersAsync(request.TeamId!.Value, cancellationToken)
                    ?? throw new BadRequestException("Team not found.");

                // Without a working leader nobody could assign members, and the ticket would be stuck
                if (!team.Leader.IsActive || team.Leader.IsDeleted || team.Leader.Role != UserRole.TeamLeader)
                    throw new BadRequestException("This team has no active leader.");

                ticket.TeamId = team.Id;
                ticket.ManagerId = user.Id; // From now on, only this manager does the final approval
                AddAudit(ticket, user, "TeamAssigned", null, team.Name);
            }, cancellationToken);
```

**Line by line**
- **Line 17:** an expression-bodied method (`=>`): the whole method is one call to `RunAsync`. It returns `Task<TicketDetailsDto>` without `async`/`await`, because it just hands back the task that `RunAsync` returns.
- **Line 18:** call `RunAsync` with the action `Route` and, as the 4th argument, **a lambda with `async`**: `async (ticket, user) => { ... }`. This is an inline function that takes the loaded ticket and user and returns a `Task` (because it is `async`). `RunAsync` will call it at step 4, **after** the rules check passed. The lambda can use `request` and `cancellationToken` from the outer method; capturing outer variables like this is called a closure.
- **Line 20:** load the team (with its leader and members). `request.TeamId!.Value`: `TeamId` is a `Guid?`, `.Value` gets the `Guid` out of it, and `!` tells the compiler "I know it isn't null" (validation already guaranteed that).
- **Line 21:** `?? throw ...`: the `??` operator means "if the left side is null, use the right side". Here the right side throws. So: "get the team, or if there is no such team, throw 400". One line instead of an `if`.
- **Lines 23-25:** a team whose leader is inactive, deleted, or no longer a TeamLeader would leave the ticket stuck forever (nobody could pass `IsLeaderOfTicketTeam` to assign). So we refuse with 400. This was a fix from the code review.
- **Line 27:** link the ticket to the team. From now on the team's leader can see it (`TicketVisibility`) and passes `IsLeaderOfTicketTeam`.
- **Line 28:** save which manager routed it. This is what `IsRoutingManager` checks at ManagerReview.
- **Line 29:** an extra audit row: action `TeamAssigned`, old value null, new value the team's name. The status audit row ("Route", Submitted -> Routed) is added later by `RunAsync` step 5.
- **Line 30:** close the lambda and pass the `cancellationToken` as the last argument of `RunAsync`.

Note: by the time this lambda runs, `Apply` has already set `ticket.Status = Routed` in memory. If line 21 or 25 throws, that change is simply never saved (see "all or nothing" in `RunAsync`).

### Part 3: RejectAsync

```csharp
        public Task<TicketDetailsDto> RejectAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Reject, null, cancellationToken);
```

**Line by line**
- **Lines 32-33:** Reject has nothing special to do: the status change and the audit row are enough. So it passes `null` as `applyChanges`, and `RunAsync` skips step 4. No reason text is stored (MVP; listed as future work).

### Part 4: AssignAsync

```csharp
        public Task<TicketDetailsDto> AssignAsync(Guid userId, Guid ticketId, AssignMembersRequest request, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Assign, async (ticket, user) =>
            {
                // Only active TeamMembers of THIS ticket's team can be assigned
                var team = await teams.GetWithMembersAsync(ticket.TeamId!.Value, cancellationToken)
                    ?? throw new ConflictException("The ticket's team no longer exists.");

                var eligible = team.Members
                    .Where(m => !m.IsDeleted && m.User.IsActive && !m.User.IsDeleted && m.User.Role == UserRole.TeamMember)
                    .ToDictionary(m => m.UserId, m => m.User.Name);

                foreach (var memberId in request.MemberIds.Distinct())
                {
                    if (!eligible.TryGetValue(memberId, out var memberName))
                        throw new BadRequestException($"User {memberId} is not an active member of this team.");

                    // Already on it (e.g. a reopened ticket): keep the existing row.
                    // Assignments are never removed, so the unique (TicketId, UserId) index can't be hit.
                    if (ticket.Assignments.Any(a => a.UserId == memberId && !a.IsDeleted))
                        continue;

                    tickets.AddAssignment(new TicketAssignment { TicketId = ticket.Id, UserId = memberId, AssignedById = user.Id });
                    AddAudit(ticket, user, "MemberAssigned", null, memberName);
                }

                // Count only assignees who can still work on it: on a reopened ticket, an old
                // assignee may have been deactivated or moved off the team since
                if (!ticket.Assignments.Any(a => !a.IsDeleted && eligible.ContainsKey(a.UserId)))
                    throw new BadRequestException("Assign at least one active member of this team.");
            }, cancellationToken);
```

**Line by line**
- **Lines 35-36:** same shape as Route: action `Assign`, with an `async` lambda for the special work.
- **Lines 38-40:** load the ticket's own team. `ticket.TeamId!.Value` is safe because Assign is only allowed from Routed or Reopened, and both come after Route set the team (and `IsLeaderOfTicketTeam` already required `ticket.Team != null`). If the team was deleted meanwhile, 409.
- **Lines 42-44:** build the list of people who may be assigned:
  - `.Where(...)` keeps team members whose membership isn't deleted, whose user is active, not deleted, and still has the TeamMember role.
  - `.ToDictionary(m => m.UserId, m => m.User.Name)` turns that list into a dictionary: key = user id, value = user name. Two lambdas: one picks the key, one picks the value. A dictionary gives a fast "is this id in it?" lookup and the name for the audit row in one step.
- **Line 46:** loop over the requested ids. `.Distinct()` removes duplicates, so `[A, A]` is treated as `[A]`.
- **Line 48:** `eligible.TryGetValue(memberId, out var memberName)`: "try to find this key". It returns true or false, and if true, puts the value into a new variable `memberName` (declared right there with `out var`). One call does both the check and the read.
- **Line 49:** an id that isn't an eligible member of this team: 400, and the whole request is cancelled (nothing saved).
- **Lines 51-54:** if this member already has an assignment on the ticket (for example on a Reopened ticket), `continue` skips the rest of this loop round and goes to the next id. So we never insert a duplicate. Why this matters: the database has a unique index on `(TicketId, UserId)` in `TicketAssignmentConfiguration`. Assignments are never removed, so skipping is enough to never hit that index.
- **Line 56:** create the new assignment row and add it **through the repository** (`tickets.AddAssignment`), not with `ticket.Assignments.Add(...)`. This is the EF bug fix; see "The EF bug story" below. `{ TicketId = ..., UserId = ..., AssignedById = ... }` is an object initializer: set properties right after `new`.
- **Line 57:** one audit row per newly assigned member: `MemberAssigned`, new value = the member's name.
- **Lines 60-63:** after the loop, there must be at least one assignee who can still work: not deleted and in `eligible`. `eligible.ContainsKey(...)` checks the dictionary. This line sees the new assignments too, because `db.TicketAssignments.Add(...)` makes EF link the new row into the tracked ticket's `Assignments` collection right away (the repository comment says so). It catches two cases: an empty request on a fresh ticket, and a Reopened ticket whose old assignees were all deactivated or moved off the team. This was a fix from the code review.
- **Line 64:** close the lambda.

### Part 5: SubmitAsync

```csharp
        public Task<TicketDetailsDto> SubmitAsync(Guid userId, Guid ticketId, SubmitFixRequest request, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Submit, (ticket, user) =>
            {
                // One timestamp for both rows: that's how a sent response is matched to its fix
                var submittedAt = DateTime.UtcNow;

                // The "After" snapshot is the fix. Same repo as the ticket; the branch and commit are the member's.
                tickets.AddSnapshot(new CodeSnapshot
                {
                    TicketId = ticket.Id,
                    CreatedAt = submittedAt,
                    Kind = SnapshotKind.After,
                    RepositoryUrl = ticket.RepositoryUrl,
                    Branch = request.Branch,
                    CommitSha = request.CommitSha,
                    SubmittedById = user.Id
                });

                // A draft until the manager approves it; the client can't see drafts
                tickets.AddResponse(new TicketResponse
                {
                    TicketId = ticket.Id,
                    CreatedAt = submittedAt,
                    AuthorId = user.Id,
                    Summary = request.Summary
                });
                return Task.CompletedTask;
            }, cancellationToken);
```

**Line by line**
- **Lines 66-67:** action `Submit`. This lambda is **not** `async`: it doesn't await anything. It still has to return a `Task` to match `Func<Ticket, User, Task>`, so it returns one at the end (line 92).
- **Lines 69-70:** take the time once. Both rows get the exact same `CreatedAt`. `TicketService.SnapshotsVisibleTo` uses this to match a Sent response to its After snapshot, so a client sees only the fixes that were actually sent to them. (Code review fix: clients could see SHAs of fixes still in review.)
- **Lines 72-82:** add the "After" `CodeSnapshot`: the member's branch and commit, on the same repository as the ticket, recording who submitted it. Added through the repository (`AddSnapshot`).
- **Lines 84-91:** add a `TicketResponse` with the member's summary. `Status` isn't set here, so it keeps its default from the entity, `ResponseStatus.Draft`. Clients never see drafts (`TicketService.ToDetailsDto` filters to Sent for clients).
- **Line 92:** `Task.CompletedTask` is a ready-made task that is already finished. It is how a non-async method says "done, nothing to wait for".
- **Line 93:** close the lambda.

### Part 6: LeaderApproveAsync and LeaderSendBackAsync

```csharp
        public Task<TicketDetailsDto> LeaderApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.LeaderApprove, null, cancellationToken);

        public Task<TicketDetailsDto> LeaderSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.LeaderSendBack, DiscardDraftAsync, cancellationToken);
```

**Line by line**
- **Lines 95-96:** leader approval only moves the status (LeaderReview -> ManagerReview). Nothing else changes, so `null`.
- **Lines 98-99:** sending back passes `DiscardDraftAsync` (no parentheses) as `applyChanges`. That is a **method group** again: the method itself is passed as a value, like the `Is...` functions in the rules table. Its signature `(Ticket, User) -> Task` matches `Func<Ticket, User, Task>`.

### Part 7: ManagerApproveAsync and ManagerSendBackAsync

```csharp
        public Task<TicketDetailsDto> ManagerApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.ManagerApprove, (ticket, user) =>
            {
                var draft = ticket.Responses
                    .Where(r => r.Status == ResponseStatus.Draft)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault()
                    ?? throw new ConflictException("There is no draft response to send.");

                // Approving is what sends it: the client sees it from now on
                draft.Status = ResponseStatus.Sent;
                draft.ApprovedById = user.Id;
                draft.SentAt = DateTime.UtcNow;
                return Task.CompletedTask;
            }, cancellationToken);

        public Task<TicketDetailsDto> ManagerSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.ManagerSendBack, DiscardDraftAsync, cancellationToken);
```

**Line by line**
- **Lines 101-102:** action `ManagerApprove`, non-async lambda.
- **Lines 104-108:** find the draft to send: among the ticket's responses (already loaded without soft-deleted ones), keep Drafts, newest first, take the first one or null. `?? throw` turns "no draft" into 409. In normal flow there is exactly one draft here, because each send-back hides the old one.
- **Lines 110-113:** this is the moment the response goes to the client. The Draft becomes `Sent`, we record who approved it and when. These are changes to an entity EF is already tracking, so EF will send an UPDATE for this row at save time.
- **Line 114:** nothing awaited, so return the finished task.
- **Lines 117-118:** manager send-back reuses the same `DiscardDraftAsync` as the leader.

**Why ManagerApprove is what sends the response.** The client should only ever see an answer that both the leader and the manager approved. The response is created as a Draft at Submit and only becomes Sent here, at the last approval. The `Approved` value of `ResponseStatus` isn't used: CLAUDE.md says the manager's approval sends it, so Draft goes straight to Sent. **Where drafts are hidden:** `ManagerApprove` *sets* Sent; `TicketService.ToDetailsDto` *filters* so clients see only Sent responses (and `SnapshotsVisibleTo` only their matching fixes). The workflow service doesn't hide anything itself.

### Part 8: CloseAsync and ReopenAsync

```csharp
        public Task<TicketDetailsDto> CloseAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Close, (ticket, user) =>
            {
                ticket.ClosedAt = DateTime.UtcNow;
                return Task.CompletedTask;
            }, cancellationToken);

        public Task<TicketDetailsDto> ReopenAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken) =>
            RunAsync(userId, ticketId, WorkflowAction.Reopen, null, cancellationToken);
```

**Line by line**
- **Lines 120-125:** Close also records when it was closed (`ClosedAt`), as CLAUDE.md requires.
- **Lines 127-128:** Reopen only changes the status to Reopened. From there the leader uses Assign again (Reopened -> InProgress), possibly with an empty member list to continue with the same people.

### Part 9: RunAsync, the 6 steps every action follows

```csharp
        // The same steps for every action:
        // 1. load the caller fresh from the database   2. load the ticket (404 if they can't see it)
        // 3. TicketWorkflow checks who + status, and changes the status   4. step-specific changes
        // 5. audit row   6. save everything in one transaction
        private async Task<TicketDetailsDto> RunAsync(
            Guid userId,
            Guid ticketId,
            WorkflowAction action,
            Func<Ticket, User, Task>? applyChanges,
            CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            var ticket = await tickets.GetForUpdateAsync(ticketId, TicketVisibility.VisibleTo(user), cancellationToken)
                ?? throw new NotFoundException("Ticket not found.");

            var oldStatus = TicketWorkflow.Apply(user, ticket, action);

            if (applyChanges is not null)
                await applyChanges(ticket, user);

            AddAudit(ticket, user, action.ToString(), oldStatus.ToString(), ticket.Status.ToString());

            // The status change, the step's rows and the audit rows are saved together or not at all
            await tickets.SaveChangesAsync(cancellationToken);

            return await ticketService.GetByIdAsync(userId, ticketId, cancellationToken);
        }
```

**Line by line**
- **Lines 130-133:** the 6 steps, written as a comment so anyone can find them.
- **Line 134:** `private async`: only this class uses it, and it awaits database calls.
- **Lines 135-139:** the parameters. The interesting one is **line 138**, `Func<Ticket, User, Task>? applyChanges`:
  - `Func<Ticket, User, Task>`: a function that takes a ticket and a user and returns a `Task` (so it can be async).
  - `?`: it may be null, for actions with nothing special to do (Reject, LeaderApprove, Reopen).
  - This is how one pipeline serves all 10 actions: the fixed steps are here, the variable step is passed in.
- **Line 141 (step 1, load the caller):** `GetActingUserAsync` reads the user from the database by id. A deleted or deactivated user gets 401 even if their JWT is still valid, and the role is the **current** one from the database, not the one in the token. It is a primary-key lookup, about a millisecond.
- **Lines 143-144 (step 2, load the ticket):** `GetForUpdateAsync` loads the ticket **only if this user can see it**. `TicketVisibility.VisibleTo(user)` builds the visibility filter (client: own tickets; manager/admin: all; leader: their team's; member: assigned ones). If the ticket doesn't exist **or** isn't visible, the result is null, and `?? throw` gives 404. Both cases get the same answer, so nobody can probe which ticket ids exist.
- **Line 146 (step 3, the rules):** `TicketWorkflow.Apply` checks who (403) then status (409), sets the new status, and returns the old one.
- **Lines 148-149 (step 4, step-specific changes):** if the action passed a function, call it now with the loaded ticket and user, and wait for it. `is not null` is a pattern check, same as `!= null`. This runs **after** the rules, so Route never loads a team for someone who isn't allowed to route.
- **Line 151 (step 5, the audit row):** one row per status change: action name (for example `"Submit"`), old status, new status. `.ToString()` on enums gives their names. The extra rows from step 4 (`TeamAssigned`, `MemberAssigned`) are already added.
- **Lines 153-154 (step 6, save once):** one `SaveChangesAsync`. EF wraps everything it sends in this call in one database transaction: the ticket UPDATE (status, team, manager, closed time...), the new assignments/snapshots/responses, the response updates, and the audit rows. Either all of them are written, or none.
- **Line 156:** build the answer by reusing `TicketService.GetByIdAsync`: a fresh read of the ticket details with the same mapping and filtering as `GET /api/tickets/{id}`, including the new `allowedActions`. Note: this means a second user lookup and a second ticket query per action. It keeps one mapping function for details instead of two.

**All or nothing, explained.** Until line 154, every change is only in memory, in the DbContext's change tracker. If anything throws before line 154 (403, 409, a 400 from Route or Assign, any bug), we never reach the save, so **nothing is written**: not the status, not the audit row. And the half-changed objects don't leak into the next request, because the DbContext is **scoped**: it is created for this request and thrown away at its end. If the save itself fails, EF rolls back the transaction, so again nothing is written. That answers the session-notes question: "if writing the audit row failed, would the status change still be saved?" No: it's one `SaveChanges`, so one transaction.

**Why visibility (404) comes before the rules (403).** Step 2 runs before step 3. A leader of **another** team calling `leader-approve` on our ticket is filtered out by `VisibleTo` (the ticket's team isn't theirs), so the ticket comes back null and they get **404**. They never reach `Apply`, so they never get 403. That is on purpose: a 403 would confirm "this ticket exists, you just can't touch it". For tickets you can't see, the API pretends they don't exist. A 403 only happens to someone who **can** see the ticket but isn't the right person for this action, for example an Admin (sees everything, has no workflow powers), a client pressing `assign` on their own ticket, or a manager who didn't route the ticket trying `manager-approve`.

### Part 10: DiscardDraftAsync

```csharp
        // Sent back: the draft is hidden (soft-deleted) so the next submit starts a fresh one.
        // The audit log still records that it happened.
        private static Task DiscardDraftAsync(Ticket ticket, User user)
        {
            foreach (var draft in ticket.Responses.Where(r => r.Status == ResponseStatus.Draft))
                draft.IsDeleted = true;

            return Task.CompletedTask;
        }
```

**Line by line**
- **Lines 159-160:** why it exists. On a send-back the team must fix the code again, so the old draft answer is no longer the answer.
- **Line 161:** `private static`: it uses no fields of the service. Its signature `(Ticket, User) -> Task` is exactly `Func<Ticket, User, Task>`, so it can be passed as `applyChanges`. `user` isn't used, it is there only to match that shape.
- **Lines 163-164:** mark every draft as soft-deleted (`IsDeleted = true`). The row stays in the database, it is just hidden: `GetForUpdateAsync` and `GetDetailsAsync` both load responses with `.Where(r => !r.IsDeleted)`.
- **Line 166:** finished task, nothing awaited.

**Why soft-delete and not hard delete or keep it?** If we kept it as a Draft, the next Submit would create a second draft and `ManagerApprove` would have to guess which one to send (it takes the newest, but old ones would linger in the team's view). If we hard-deleted it, we would lose data. Soft-delete hides it from every query while the row (and the audit row "LeaderSendBack" / "ManagerSendBack") stays as history. Note: the After `CodeSnapshot` from the sent-back attempt is **not** soft-deleted. The team still sees it in the details; clients don't, because `SnapshotsVisibleTo` only shows fixes matched to a Sent response.

### Part 11: AddAudit

```csharp
        private void AddAudit(Ticket ticket, User user, string action, string? oldValue, string? newValue) =>
            tickets.AddAuditLog(new AuditLog
            {
                TicketId = ticket.Id,
                ActorId = user.Id,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue
            });
    }
}
```

**Line by line**
- **Line 169:** a small helper so every audit row is built the same way. Not `static`, because it uses the `tickets` repository. `string?` means old/new values may be null (for example `TeamAssigned` has no old value).
- **Lines 170-177:** create an `AuditLog` and add it through the repository. `Id` and `CreatedAt` get their defaults in the entity (`Guid.NewGuid()`, `DateTime.UtcNow`). `AuditLog` has no navigation properties and no foreign keys on purpose, so the log survives even if a user is later removed. This only adds the row to the change tracker; it is written by the one save in `RunAsync`.
- **Lines 178-179:** close the class and namespace.

Note: `CreatedAt` is set when the `AuditLog` object is created. Extra rows from step 4 are created before the status row from step 5, so in the history `TeamAssigned` appears just before `Route`, and `MemberAssigned` rows just before `Assign`.

### What happens on save (TicketRepository)

The service never sees EF Core. These are the repository methods it calls, from [TicketRepository.cs](../../TicketingSystem.Infrastructure/Persistence/Repositories/TicketRepository.cs):

```csharp
        // EF also links each one into the loaded ticket's collection right away
        public void AddAssignment(TicketAssignment assignment) => db.TicketAssignments.Add(assignment);
```

`AddAssignment`, `AddSnapshot`, `AddResponse` and `AddAuditLog` all call `DbSet.Add(...)`, which marks the new object as **Added** for sure. Nothing touches the database yet.

`GetForUpdateAsync` loads the ticket **with tracking** (no `AsNoTracking`), plus only what the rules need: `Team` (leader check), non-deleted `Assignments` (assigned-member check), non-deleted `Responses` (drafts). It applies the visibility filter in the same SQL query.

```csharp
            catch (DbUpdateConcurrencyException)
            {
                // Ticket.RowVersion changed since we loaded it: someone else saved first.
                // Turned into an Application exception here, so the API doesn't need to know EF.
                throw new ConflictException("Someone else changed this ticket at the same time. Reload it and try again.");
            }
```

**RowVersion and 409 on concurrent changes.** `Ticket.RowVersion` is configured with `.IsRowVersion()`. SQL Server changes it automatically on every update of the row. When EF saves the ticket, it sends `UPDATE Tickets ... WHERE Id = @id AND RowVersion = @versionWeLoaded`. Every workflow action changes `ticket.Status`, so every action sends this UPDATE. If another request saved the same ticket in between (two leaders pressing approve at once), the row version no longer matches, the UPDATE changes 0 rows, EF throws `DbUpdateConcurrencyException`, and the repository turns it into `ConflictException`, so 409 "Reload it and try again". The transaction is rolled back, so the second request writes nothing (no duplicate audit row). Without this, the second save would silently overwrite the first.

Also on save: `AppDbContext.SaveChangesAsync` stamps `UpdatedAt` on every modified `BaseEntity` (the ticket, an approved response, a soft-deleted draft).

### The EF bug story

A good story for the committee, because the end-to-end tests caught it.

- **What we did first:** new rows were added through the ticket's collections, for example `ticket.Assignments.Add(new TicketAssignment { ... })`.
- **What happened:** our ids are set in C# (`BaseEntity.Id = Guid.NewGuid()`), so a brand-new object already has an id. When EF found the new object through the tracked ticket's collection, it saw a key that was already set and assumed the row **already existed**, so it sent an `UPDATE` instead of an `INSERT`. That UPDATE matched 0 rows. EF reads "0 rows affected" as "someone else changed or deleted this row", so it threw `DbUpdateConcurrencyException`, which our repository turns into a 409. The test saw "Someone else changed this ticket" on a request nobody else was touching.
- **The fix:** add new rows explicitly through the repository (`AddAssignment`, `AddSnapshot`, `AddResponse`), which call `DbSet.Add(...)`. That tells EF "this is new, INSERT it" with no guessing. EF still links it into `ticket.Assignments` in memory, which is why `AssignAsync` line 62 sees the new rows.

Note: `TicketService.CreateAsync` still uses `ticket.Snapshots.Add(...)` for the Before snapshot. That works because there the ticket itself is new and is passed to `tickets.Add(ticket)`, which marks the whole object graph as Added. The bug only happens when the parent ticket was **loaded** from the database.

**Committee might ask:**
- **Q: Walk me through what happens when someone calls a workflow endpoint.** A: `RunAsync`'s 6 steps: load the user fresh (401), load the ticket only if visible (404), `Apply` checks who (403) and status (409) and changes the status, step-specific changes, audit row, one save.
- **Q: If the audit write failed, would the status change be saved?** A: No. Everything goes through one `SaveChangesAsync`, which is one transaction. Any exception before the save means nothing is written, and the DbContext is per request, so nothing half-done carries over.
- **Q: A leader of another team calls `leader-approve` on our ticket. What do they get?** A: 404. The visibility filter in step 2 hides the ticket from them, so they never reach the rules. We don't confirm that tickets they can't see exist.
- **Q: What if two people act on the same ticket at the same time?** A: `RowVersion`. The second save's UPDATE matches 0 rows, EF throws a concurrency exception, the repository turns it into 409, and nothing from the second request is saved.
- **Q: Why reload the user from the database on every call? Isn't that slow? You made it async.** A: Async doesn't make a query faster. It frees the thread while waiting, so the server handles more requests at once. The lookup is cheap anyway, a primary-key lookup of about 1 ms, and it makes deactivation and role changes apply instantly instead of when the token expires.
- **Q: Why is the draft soft-deleted on send-back, and when does the client see the response?** A: So the next submit starts a fresh draft, while the old row and the audit entry stay as history. The client sees a response only after ManagerApprove sets it to Sent; `TicketService` filters responses to Sent for clients.
- **Q: Why does Route check the team's leader?** A: A team without an active leader would leave the ticket stuck, because nobody could pass the "leader of this team" check to assign members. Better to refuse with 400 at routing time.

---

## [TicketWorkflowController.cs](../../TicketingSystem.API/Controllers/TicketWorkflowController.cs) (briefly)

**What it is:** 10 POST endpoints, one per action. Look how thin they are.

```csharp
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
```

(The other 8 endpoints, lines 22-52, follow exactly the same pattern: `assign`, `submit`, `leader-approve`, `leader-send-back`, `manager-approve`, `manager-send-back`, `close`, `reopen`.)

Key points:
- **Lines 8-9:** the comment states the design: no rules here.
- **Line 10:** `[ApiController]` gives automatic 400 for invalid request bodies (the attributes in `WorkflowRequests.cs`).
- **Line 11:** the route template. `{id:guid}` only matches a valid Guid, otherwise 404 before our code runs. `[HttpPost("route")]` adds to it, so the full URL is `POST /api/tickets/{id}/route`.
- **Line 12:** depends on the **interface**, injected by DI.
- **Lines 15-16:** each endpoint is one line: get the caller's id from the JWT's `sub` claim (`User.GetUserId()`), call the service, wrap the result in `Ok(...)` (200). No `[Authorize(Roles = ...)]`: login is required for every controller by `MapControllers().RequireAuthorization()` in `Program.cs`, and the "who" is decided by the rules table, which knows more than a role (this team, this ticket).
- No try/catch: exceptions from the service flow to `GlobalExceptionHandler`, which turns them into ProblemDetails with the right status code.

---

## Walk one ticket through the code

Cast: the client, the teams manager, the leader of "Backend Team", a member of that team. Each step below is one HTTP call. Every call goes through `RunAsync` (lines 141-156): user loaded (line 141), ticket loaded with visibility (line 143), `Apply` (line 146), step work (line 149), status audit row (line 151), one save (line 154), details returned (line 156).

| # | Who calls | Endpoint | Rules row (TicketWorkflow) | Step work (TicketWorkflowService) | Status | Audit rows written |
|---|---|---|---|---|---|---|
| 0 | Client | `POST /api/tickets` | (not the workflow) | `TicketService.CreateAsync`: ticket + Before snapshot | -> Submitted | `Created` (null -> Submitted) |
| 1 | Manager | `POST .../route` `{ teamId }` | Line 15: from Submitted, `IsTeamsManager` | `RouteAsync` lines 20-29: load team, check leader, set `TeamId` and `ManagerId` | Submitted -> Routed | `TeamAssigned` (null -> "Backend Team"), `Route` (Submitted -> Routed) |
| 2 | Leader | `POST .../assign` `{ memberIds: [member] }` | Line 17: from Routed, `IsLeaderOfTicketTeam` | `AssignAsync` lines 39-63: build `eligible`, `AddAssignment` (line 56), at-least-one check | Routed -> InProgress | `MemberAssigned` (null -> member name), `Assign` (Routed -> InProgress) |
| 3 | Member | `POST .../submit` `{ branch, commitSha, summary }` | Line 18: from InProgress, `IsAssignedMember` | `SubmitAsync` lines 70-91: `AddSnapshot` (After) + `AddResponse` (Draft), same `CreatedAt` | InProgress -> LeaderReview | `Submit` (InProgress -> LeaderReview) |
| 4 | Leader | `POST .../leader-send-back` | Line 20: from LeaderReview, `IsLeaderOfTicketTeam` | `DiscardDraftAsync` lines 163-164: draft `IsDeleted = true` | LeaderReview -> InProgress | `LeaderSendBack` (LeaderReview -> InProgress) |
| 5 | Member | `POST .../submit` (new commit) | Line 18 again | `SubmitAsync` again: a second After snapshot and a fresh Draft | InProgress -> LeaderReview | `Submit` (InProgress -> LeaderReview) |
| 6 | Leader | `POST .../leader-approve` | Line 19: from LeaderReview, `IsLeaderOfTicketTeam` | none (`null`) | LeaderReview -> ManagerReview | `LeaderApprove` (LeaderReview -> ManagerReview) |
| 7 | Manager | `POST .../manager-approve` | Line 21: from ManagerReview, `IsRoutingManager` (`ManagerId` set in step 1) | `ManagerApproveAsync` lines 104-113: newest Draft -> Sent, `ApprovedById`, `SentAt` | ManagerReview -> ResponseSent | `ManagerApprove` (ManagerReview -> ResponseSent) |
| 8 | Client | `POST .../close` | Line 23: from ResponseSent, `IsOwner` | `CloseAsync` line 123: `ClosedAt` | ResponseSent -> Closed | `Close` (ResponseSent -> Closed) |

What each person sees along the way:
- After step 1 the leader can see the ticket (its `TeamId` now points at their team). Before that, `VisibleTo` hid it from every leader.
- After step 2 the member can see it (they now have an assignment).
- After step 4 the team still sees the first After snapshot, but the first draft is hidden everywhere (soft-deleted).
- The client, at any time, sees only the Before snapshot, Sent responses and their matching fixes. Before step 7 that means no response at all. After step 7 they see the second summary and the second commit, never the first attempt.
- The client's history (`TicketService.GetHistoryAsync`) shows only `Created`, `Route`, `Reject`, `ManagerApprove`, `Close` and `Reopen`. The internal `TeamAssigned`, `MemberAssigned`, `Assign`, `Submit`, `LeaderSendBack` and `LeaderApprove` rows are filtered out for clients. The team and manager see all 11 rows (Created, TeamAssigned, Route, MemberAssigned, Assign, Submit, LeaderSendBack, Submit, LeaderApprove, ManagerApprove, Close).

What would go wrong at a few points (all with nothing saved):
- In step 2, if a **client** called `assign`: they can see their own ticket, so they pass step 2 of `RunAsync`, then fail `IsLeaderOfTicketTeam` on line 44 of `TicketWorkflow`: **403**.
- In step 2, if the leader of **another** team called `assign`: `VisibleTo` hides the ticket on line 143 of the service: **404**.
- In step 6, if the leader pressed `leader-approve` twice quickly: the first call moves the ticket to ManagerReview. The second either loads the ticket after that and fails the status check on line 47 of `TicketWorkflow` (**409** "Can't LeaderApprove a ticket that is ManagerReview"), or, if both loaded it at the same moment, the second save hits the `RowVersion` check (**409** "Someone else changed this ticket").
- In step 8, if the client had chosen `reopen` instead: Reopen (line 24, ResponseSent -> Reopened, audit `Reopen`). Then the leader calls `assign` with an empty list (line 17 allows Reopened). The loop on line 46 does nothing, line 62 finds the existing active assignee, and the ticket goes Reopened -> InProgress with one `Assign` audit row. The cycle continues from step 3.
