# 1. Domain: what things are

The Domain layer is the center of Mizan. It holds the **nouns** of the system (Ticket, User, Team, Snapshot, Response, AuditLog) and the fixed lists of values they use (roles, statuses, priorities). It contains no business rules, no database code and no HTTP code: it only says *what things are*. The most important rule: **Domain depends on nothing.** It does not reference Application, Infrastructure or API, so every other layer can use it, and it never changes because of a database or web detail. EF Core reads these classes and turns them into tables, but that mapping lives in Infrastructure, not here.

| File | One line |
|---|---|
| [TicketingSystem.Domain.csproj](../../TicketingSystem.Domain/TicketingSystem.Domain.csproj) | Project file: .NET 10, nullable on, one package (EF Core Abstractions). |
| [BaseEntity.cs](../../TicketingSystem.Domain/Entities/BaseEntity.cs) | Parent class: Id, CreatedAt, UpdatedAt, IsDeleted for every entity. |
| [User.cs](../../TicketingSystem.Domain/Entities/User.cs) | A person who logs in, with one role. |
| [Team.cs](../../TicketingSystem.Domain/Entities/Team.cs) | A team with one leader and many members. |
| [TeamMember.cs](../../TicketingSystem.Domain/Entities/TeamMember.cs) | Join row: "this user is in this team". |
| [Ticket.cs](../../TicketingSystem.Domain/Entities/Ticket.cs) | The code-review request itself. The main entity. |
| [TicketAssignment.cs](../../TicketingSystem.Domain/Entities/TicketAssignment.cs) | "This member works on this ticket, assigned by this leader." |
| [CodeSnapshot.cs](../../TicketingSystem.Domain/Entities/CodeSnapshot.cs) | A pointer to the code at a moment: Before or After (repo, branch, commit). |
| [TicketResponse.cs](../../TicketingSystem.Domain/Entities/TicketResponse.cs) | The answer that goes back to the client. |
| [Comment.cs](../../TicketingSystem.Domain/Entities/Comment.cs) | A comment on a ticket (table exists, no UI yet). |
| [AuditLog.cs](../../TicketingSystem.Domain/Entities/AuditLog.cs) | One row of history: who did what, old value, new value. |
| [Enums/](../../TicketingSystem.Domain/Enums/) | `UserRole`, `TicketStatus`, `Priority`, `SnapshotKind`, `ResponseStatus`, `WorkflowAction`. |

---

## [TicketingSystem.Domain.csproj](../../TicketingSystem.Domain/TicketingSystem.Domain.csproj)

**What it is:** The project file for the Domain layer. It sets the .NET version and the compiler options, and lists the one NuGet package.

**The code:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Abstractions" Version="10.0.13" />
  </ItemGroup>

</Project>
```

**Line by line:**
- Line 1: `Microsoft.NET.Sdk` is the plain class-library SDK. Not the Web SDK, because Domain has nothing to do with HTTP.
- Line 4: `net10.0` targets .NET 10, the same as every other project in the solution.
- Line 5: `ImplicitUsings` adds common `using` lines automatically (`System`, `System.Collections.Generic`, `System.Linq`, ...). That is why most entity files have no `using System;` at the top and can still use `Guid` and `DateTime`.
- Line 6: `Nullable` turns on **nullable reference types**. With it on, a `string` is "never null" and a `string?` is "may be null", and the compiler warns when you mix them up. This explains all the `= string.Empty` and `= null!` you will see below.
- Line 10: `Microsoft.EntityFrameworkCore.Abstractions` is a small package with EF attributes (for example `[Index]`, `[Precision]`) and a few interfaces. It does **not** contain `DbContext` or the SQL Server provider.
- There is **no** `<ProjectReference>`. That is the "depends on nothing" rule, visible in the file.

Note: No code in Domain uses this package yet (all mapping is done in Infrastructure's configuration classes). It is kept on purpose: Mahmoud wants it available for data annotations later.

**Committee might ask:**
- *Q: Doesn't an EF package in Domain break Clean Architecture?* A: It's only the Abstractions package (attributes, no database code), and nothing uses it yet. Domain still has no project references, so it doesn't depend on Infrastructure. It's there so annotations can be added later without changing the dependency direction.
- *Q: How do you prove Domain depends on nothing?* A: Open the csproj: there is no `ProjectReference`. The compiler would refuse any `using TicketingSystem.Infrastructure` inside Domain.

---

## [BaseEntity.cs](../../TicketingSystem.Domain/Entities/BaseEntity.cs)

**What it is:** The parent class of every entity except `AuditLog`. It gives each table the same four columns: `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`.

**The code:**

```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
```

**Line by line:**
- Lines 1-3: `using` lines added by the Visual Studio template. With `ImplicitUsings` on they are not needed, and `System.Text` is unused. They are harmless.
- Line 5: the **namespace** groups the classes; every entity lives in `TicketingSystem.Domain.Entities`.
- Line 7: `abstract class` means "you can't create a `BaseEntity` by itself, only classes that inherit from it". There is no `BaseEntities` table; its four properties become columns in each child table (Users, Tickets, ...).
- Line 9: `Guid` is a 128-bit random ID (like `3f2b...-...`). `= Guid.NewGuid()` gives every new object an ID **in C#, before it is saved**. Why: the code can link rows together before calling `SaveChanges`. For example, `TicketService` writes `TicketId = ticket.Id` into the audit row right after creating the ticket, in the same transaction. With a database-generated `int` ID, that value would still be 0 at that point. Guids also can't be guessed (`/api/tickets/5` would invite trying `/6`).
- Line 10: `CreatedAt` defaults to the moment the object is created. `DateTime.UtcNow` (not `Now`) so times don't depend on the server's time zone; the frontend converts to local time for display.
- Line 11: `DateTime?` — the `?` on a value type means "nullable": it can hold a date **or** null. `UpdatedAt` is null until the row is first changed. It is not set by hand: `AppDbContext.SaveChangesAsync` loops over every modified `BaseEntity` and stamps `UpdatedAt = DateTime.UtcNow`, so no service can forget it.
- Line 12: `IsDeleted` is for **soft delete**: a row is marked deleted instead of removed, so history is kept. `bool` defaults to `false`. The code filters it out by hand (for example `.Where(t => !t.IsDeleted)` in `TicketRepository`), because global soft-delete query filters were cut from the MVP. One real use today: when a leader or manager sends a fix back, the draft response gets `IsDeleted = true`.
- Every property is `{ get; set; }`, an **auto-property**: C# creates the hidden field for you. EF needs public setters to fill objects when it reads rows.

**Committee might ask:**
- *Q: Why Guid instead of int identity?* A: The ID exists before saving, so related rows (snapshot, audit log) can point to the ticket in the same transaction. And IDs can't be guessed from the URL.
- *Q: Who sets UpdatedAt?* A: `AppDbContext.SaveChangesAsync`, automatically, for every modified entity. One place instead of every service.
- *Q: Why soft delete?* A: Mizan is about traceability. Deleting rows would break the history. Soft-deleted rows stay in the database and the audit log still makes sense.

---

## [User.cs](../../TicketingSystem.Domain/Entities/User.cs)

**What it is:** Anyone who can log in: a client, a teams manager, a team leader, a team member or an admin. The role decides what they can do.

**The code:**

```csharp
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
```

**Line by line:**
- Line 1: imports the enums (here `UserRole`).
- Line 5: `: BaseEntity` means **inheritance**: `User` gets `Id`, `CreatedAt`, `UpdatedAt` and `IsDeleted` for free.
- Lines 7-9: `= string.Empty` gives each string a starting value of `""`. With nullable checks on, a non-nullable `string` must never be null, and this satisfies the compiler without a constructor.
- Line 8: `Email` is the login name. The configuration adds a **unique index** on it, so the database itself refuses two users with the same email.
- Line 9: `PasswordHash`, never the password. The seeder and `AuthService` use ASP.NET Core's `PasswordHasher<User>`, which stores a salted, slow hash. If the database leaks, the real passwords are not in it.
- Line 10: `UserRole Role` is an **enum** (see the Enums section). One user has exactly one role. In the database it's stored as text (`"TeamLeader"`), not a number.
- Line 11: `IsActive` defaults to `true`. It lets an admin block a login without deleting the user. It's checked on every request: login and `/me` require `u.IsActive && !u.IsDeleted`, and every ticket call reloads the user from the database, so a deactivated user gets 401 right away.
- There are **no navigation collections** here (no `Tickets` list on the user). Many tables point to Users (tickets, assignments, snapshots, responses...). Adding a list for each would clutter the class, and nothing needs to go "from user to all their tickets" through EF. The configurations use `.WithMany()` with no argument for that reason.

**Committee might ask:**
- *Q: Why one role per user and not a roles table?* A: The workflow has five clear roles and each person plays one. An enum is simpler, type-safe, and enough for the MVP. Many-to-many roles would be future work.
- *Q: Why is the role re-read from the database on each request, if it's already in the JWT?* A: The token can be old. Reading the user fresh means a role change or deactivation applies immediately, not when the token expires.
- *Q: How are passwords stored?* A: Hashed with `PasswordHasher<User>` (salted PBKDF2), never in plain text.

---

## [Team.cs](../../TicketingSystem.Domain/Entities/Team.cs)

**What it is:** A group that works on tickets. It has exactly one leader and a list of members.

**The code:**

```csharp
namespace TicketingSystem.Domain.Entities
{
    public class Team : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        // The team leader (a User with the TeamLeader role)
        public Guid LeaderId { get; set; }
        public User Leader { get; set; } = null!; // null! = "EF will fill this in when loading"

        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
    }
}
```

**Line by line:**
- Line 5: `Name` has a unique index in the configuration, so two teams can't share a name.
- Line 8: `LeaderId` is the **foreign key** column: the Id of the leader in the Users table. It's a plain `Guid`, not `Guid?`, so a team **must** have a leader.
- Line 9: `User Leader` is a **navigation property**: a C# reference to the related object, so code can write `team.Leader.Name` instead of looking the user up by Id. EF fills it when you ask for it with `.Include(t => t.Leader)`. The two together (`LeaderId` + `Leader`) is the usual EF pattern: the Id is what's stored, the object is for convenience.
- Line 9: `= null!` — the `!` is the "null-forgiving" operator. It tells the compiler "I know this looks null, trust me". The navigation is not set in C#; EF sets it when loading. Without `null!` the compiler would warn that a non-nullable property is never initialized.
- Line 11: `ICollection<TeamMember>` is an interface for "a group of items you can add to and remove from". EF uses it for the "many" side of a relationship. `= new List<TeamMember>()` starts it empty, so `team.Members.Add(...)` never crashes with a null reference.
- The leader is **not** in `Members`. Members are the people who do the work; the leader is a separate link.

Note: Nothing in the Domain enforces that `Leader` actually has the `TeamLeader` role; the comment on line 7 states it. The check is done in the workflow code (`TicketWorkflowService.RouteAsync` refuses a team whose leader is inactive, deleted, or not a `TeamLeader`).

---

## [TeamMember.cs](../../TicketingSystem.Domain/Entities/TeamMember.cs)

**What it is:** A **join table** between `Team` and `User`. One row means "this user belongs to this team".

**The code:**

```csharp
namespace TicketingSystem.Domain.Entities
{
    // Join table between Team and User (many-to-many)
    public class TeamMember : BaseEntity
    {
        public Guid TeamId { get; set; }
        public Team Team { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
```

**Line by line:**
- Line 3: a team has many users, and (in theory) a user can be in many teams. That is **many-to-many**, and in a relational database it always needs a middle table.
- Lines 6-7: foreign key + navigation to the team. In the configuration, `.WithMany(t => t.Members)` connects this to `Team.Members`.
- Lines 9-10: foreign key + navigation to the user.
- It's a full entity (it inherits `BaseEntity`), not just two columns. So it has its own `Id`, a `CreatedAt` (when the user joined) and `IsDeleted` (removing someone from a team keeps the history). The workflow and `TeamService` filter `!m.IsDeleted` when listing members.
- The configuration adds a unique index on `(TeamId, UserId)`, so the same user can't be added to the same team twice.

---

## [Ticket.cs](../../TicketingSystem.Domain/Entities/Ticket.cs)

**What it is:** The code-review request a client submits. Everything else (assignments, snapshots, responses, comments, audit rows) hangs off a ticket.

**The code:**

```csharp
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Domain.Entities
{
    public class Ticket : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RepositoryUrl { get; set; } = string.Empty;
        public Priority Priority { get; set; } = Priority.Medium;
        public TicketStatus Status { get; set; } = TicketStatus.Submitted;
        public DateTime? DueDate { get; set; }
        public DateTime? ClosedAt { get; set; }

        // Who submitted it (always set)
        public Guid ClientId { get; set; }
        public User Client { get; set; } = null!;

        // Set when a teams manager picks it up
        public Guid? ManagerId { get; set; }
        public User? Manager { get; set; }

        // Set when the manager routes it to a team (the leader is Team.Leader)
        public Guid? TeamId { get; set; }
        public Team? Team { get; set; }

        // Concurrency token: SQL Server changes it on every update, so two people
        // saving the same ticket at once get an error instead of overwriting each other
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
        public ICollection<CodeSnapshot> Snapshots { get; set; } = new List<CodeSnapshot>();
        public ICollection<TicketResponse> Responses { get; set; } = new List<TicketResponse>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
```

**Line by line:**
- Lines 7-9: what the client types in the create form. The configuration limits them (Title 200, Description 4000, RepositoryUrl 500 characters) and marks them required.
- Line 10: `Priority Priority` — the type and the property have the same name. C# allows this and knows which is which from context. `= Priority.Medium` is the default if the client doesn't choose.
- Line 11: `Status` starts as `Submitted`. This one property is what the whole workflow moves forward. Important: **the entity doesn't decide which changes are allowed.** That is done in one place, the `TicketWorkflow` rules table in Application (chapter 3). The entity just holds the current value.
- Line 12: `DueDate` is nullable and optional.
- Line 13: `ClosedAt` stays null until the client closes the ticket; `TicketWorkflowService.CloseAsync` sets it.
- Lines 16-17: `ClientId` is a plain `Guid` (required): every ticket has an owner from the start.
- Lines 20-21: `Guid? ManagerId` and `User? Manager` are **nullable** because a fresh ticket has no manager yet. The `?` on `User?` means "this reference may be null", so the compiler forces you to check before using it. It is set when a teams manager routes the ticket, and later only *that* manager can approve or send back (`IsRoutingManager` rule).
- Lines 24-25: `TeamId` / `Team`, also nullable for the same reason. There is no `LeaderId` on the ticket: the leader is found through `Team.Leader` (comment on line 23). So if a team changes its leader, the new leader sees the team's tickets with no data to update.
- Line 29: `byte[] RowVersion` is a **concurrency token**. The configuration maps it with `.IsRowVersion()` to SQL Server's `rowversion` type, which the database changes automatically on every update. When EF saves, it adds `WHERE RowVersion = <the value I loaded>`. If someone else saved in between, no row matches, EF throws `DbUpdateConcurrencyException`, and `TicketRepository.SaveChangesAsync` turns that into a `ConflictException` (HTTP 409, "Someone else changed this ticket..."). Example: two managers click "Route" at the same moment; one wins, the other gets 409 instead of silently overwriting. `= Array.Empty<byte>()` just avoids a null; the database provides the real value.
- Lines 31-34: the four "child" collections. `Assignments` (who works on it), `Snapshots` (Before / After code), `Responses` (answers to the client), `Comments`. Each is the "many" side of a one-to-many, connected in the configurations with `.WithMany(t => t.Assignments)` etc. Starting them as empty lists lets `TicketService.CreateAsync` do `ticket.Snapshots.Add(...)` on a brand-new ticket, so the ticket and its Before snapshot are saved together.

Note: `DueDate` is never set or read by any code in the solution yet. `Comments` has a table but no endpoint or UI (comments UI is in the "cut" list).

**Committee might ask:**
- *Q: Where are the status transition rules? Why not in this class?* A: In one place, the `TicketWorkflow` rules table in Application. The rules need the acting user and other data (the team, the assignments), so they sit in the Application layer. The entity just holds state. Either choice satisfies "one place"; the important thing is it's never scattered across controllers.
- *Q: What happens if two people act on the same ticket at the same time?* A: `RowVersion`. The second save fails with a concurrency error and the API returns 409 Conflict. No lost update.
- *Q: Why are ManagerId and TeamId nullable?* A: They don't exist when the ticket is created. They are filled in by the Route step.

---

## [TicketAssignment.cs](../../TicketingSystem.Domain/Entities/TicketAssignment.cs)

**What it is:** One row per member assigned to a ticket. It records who works on it, who assigned them, and when.

**The code:**

```csharp
namespace TicketingSystem.Domain.Entities
{
    // A team member assigned to work on a ticket
    public class TicketAssignment : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        // The member doing the work
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        // The leader who assigned them
        public Guid AssignedById { get; set; }
        public User AssignedBy { get; set; } = null!;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
```

**Line by line:**
- Lines 6-7: which ticket. Connected to `Ticket.Assignments` in the configuration.
- Lines 10-11: the member doing the work. This is what the rules check: only a user with an assignment row can submit a fix (`IsAssignedMember`), and a team member only **sees** tickets they're assigned to (`TicketVisibility`).
- Lines 14-15: who made the assignment (the team leader). This table has **two** foreign keys to Users (`UserId` and `AssignedById`), so EF can't guess which navigation goes with which key; the configuration spells out each one.
- Line 17: `AssignedAt` defaults to now.
- The configuration adds a unique index on `(TicketId, UserId)`: the same member can't be assigned twice to the same ticket.

Note: `AssignedAt` holds the same moment as `CreatedAt` from `BaseEntity`, and no code reads `AssignedAt` yet.

---

## [CodeSnapshot.cs](../../TicketingSystem.Domain/Entities/CodeSnapshot.cs)

**What it is:** A reference to the code at one moment: the **Before** version (when the client submitted) or an **After** version (the member's fix). It stores the repo, branch and commit SHA, not the code itself.

**The code:**

```csharp
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Domain.Entities
{
    // A point-in-time reference to the code: "Before" (as submitted) or "After" (the fix)
    public class CodeSnapshot : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public SnapshotKind Kind { get; set; }
        public string RepositoryUrl { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string CommitSha { get; set; } = string.Empty;
        public string? ArchivePath { get; set; }

        public Guid SubmittedById { get; set; }
        public User SubmittedBy { get; set; } = null!;
    }
}
```

**Line by line:**
- Lines 8-9: which ticket. Connected to `Ticket.Snapshots`.
- Line 11: `Kind` is `Before` or `After` (enum `SnapshotKind`). Stored as text.
- Lines 12-14: the code location. A **commit SHA** is Git's ID for one exact version of the code, so "Before" and "After" can be compared on GitHub at any time, and the code can't silently change underneath. The configuration caps `CommitSha` at 64 characters (a full SHA-1 is 40, SHA-256 is 64).
- Line 15: `string?` — a nullable string. `ArchivePath` would be a path to an uploaded zip of the code. It's optional.
- Lines 17-18: who submitted it: the client for Before, the member for After.
- When they are created: the **Before** snapshot in `TicketService.CreateAsync`, added through `ticket.Snapshots`, so the ticket can never exist without it. Each **After** snapshot in `TicketWorkflowService.SubmitAsync`, with the same `CreatedAt` as the draft response so a response can be matched to its fix. A ticket can have several After snapshots if it was sent back and fixed again.

Note: `ArchivePath` is never set by any code yet (attachments were cut from the MVP).

---

## [TicketResponse.cs](../../TicketingSystem.Domain/Entities/TicketResponse.cs)

**What it is:** The written answer that goes back to the client with the fix. It starts as a draft, and the client only sees it once the manager approves it.

**The code:**

```csharp
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Domain.Entities
{
    // The answer that goes back to the client: Draft -> Approved -> Sent
    public class TicketResponse : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public Guid AuthorId { get; set; }
        public User Author { get; set; } = null!;

        public string Summary { get; set; } = string.Empty;
        public ResponseStatus Status { get; set; } = ResponseStatus.Draft;

        // Set when the teams manager approves it
        public Guid? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        // Null until it is actually sent to the client
        public DateTime? SentAt { get; set; }
    }
}
```

**Line by line:**
- Lines 8-9: which ticket. Connected to `Ticket.Responses`.
- Lines 11-12: the author, the member who submitted the fix.
- Line 14: `Summary` is the text of the answer (max 4000 characters in the configuration).
- Line 15: `Status` starts as `Draft`. A draft is internal: the client can't see it.
- Lines 18-19: nullable, because nobody has approved it yet when it's created. `ManagerApproveAsync` sets `ApprovedById` to the manager.
- Line 22: `SentAt` stays null until the response is really delivered.
- What the code actually does: `SubmitAsync` creates the draft. `ManagerApproveAsync` sets `Status = Sent`, `ApprovedById` and `SentAt` together. If the leader or manager sends the fix back, the draft is soft-deleted (`IsDeleted = true`), so the next submit starts a fresh one.

Note: The comment on line 5 says `Draft -> Approved -> Sent`, but the code goes straight from `Draft` to `Sent` when the manager approves ("approving is what sends it"). The `Approved` value of `ResponseStatus` is never assigned anywhere.

---

## [Comment.cs](../../TicketingSystem.Domain/Entities/Comment.cs)

**What it is:** A comment on a ticket. It can be internal (team only) or visible to the client.

**The code:**

```csharp
namespace TicketingSystem.Domain.Entities
{
    public class Comment : BaseEntity
    {
        public Guid TicketId { get; set; }
        public Ticket Ticket { get; set; } = null!;

        public Guid AuthorId { get; set; }
        public User Author { get; set; } = null!;

        public string Body { get; set; } = string.Empty;

        // true = team-only discussion, hidden from the client
        public bool IsInternal { get; set; }
    }
}
```

**Line by line:**
- Lines 5-6: which ticket. Connected to `Ticket.Comments`.
- Lines 8-9: who wrote it.
- Line 11: the text (max 4000 characters).
- Line 14: `IsInternal` defaults to `false` (visible). `true` would hide it from the client.

Note: The `Comments` table exists in the database, but no service, endpoint or page uses it. The comments UI is in the MVP "cut" list (future work). The entity was designed up front so adding it later needs no schema redesign.

---

## [AuditLog.cs](../../TicketingSystem.Domain/Entities/AuditLog.cs)

**What it is:** One line of history. Every ticket creation and every workflow step writes one row: who did it, which action, the status before and after.

**The code:**

```csharp
namespace TicketingSystem.Domain.Entities
{
    // Append-only history. Not a BaseEntity: audit rows are never updated or soft-deleted.
    // No navigation properties on purpose: the log must survive even if the ticket/user it
    // mentions is removed, so we store plain IDs without database foreign keys.
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? TicketId { get; set; }   // null for non-ticket events (e.g. login)
        public Guid ActorId { get; set; }
        public string Action { get; set; } = string.Empty; // e.g. "StatusChanged", "Assigned"
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

**Line by line:**
- Line 3: **append-only** means rows are only ever added, never changed or deleted. That is why it does **not** inherit `BaseEntity`: it doesn't need `UpdatedAt` (never updated) or `IsDeleted` (never deleted). A side effect: `AppDbContext` only stamps `UpdatedAt` on `BaseEntity` objects, so audit rows are skipped.
- Lines 4-5: the key design decision. There are **no navigation properties and no foreign keys**. A foreign key would tie each audit row to a live Ticket or User row. Then deleting that ticket or user would either be blocked by the log or would force the log rows to be deleted too. Either way the history would control the data, or be lost. With plain Ids, the log keeps working no matter what happens to the rows it mentions. The configuration confirms: "No relationships configured on purpose".
- Line 8: its own `Id`, defined here because there is no base class.
- Line 9: `Guid? TicketId` is nullable so the same table could also log events that aren't about a ticket (like a login). Today every row written has a ticket Id. The configuration adds an index on `TicketId` so "history of ticket X" is fast.
- Line 10: `ActorId`, the user who did it.
- Line 11: `Action` is a `string`, not the `WorkflowAction` enum. Why: not every action is a workflow step. Ticket creation writes `"Created"` (a constant in `TicketService`), and workflow steps write `action.ToString()`, for example `"Route"` or `"ManagerApprove"`. A string can hold both.
- Lines 12-13: `OldValue` and `NewValue` are nullable strings. In the current code they hold the status before and after (`"Submitted"` -> `"Routed"`). For `Created`, `OldValue` is null because there was no status before.
- Line 14: `IpAddress` (max 45 characters, enough for IPv6).
- Line 15: `CreatedAt`, when it happened. Indexed, so history sorts quickly.
- The audit row is added in the **same `SaveChanges`** as the status change, so both are saved together or not at all. You can't have a change without its audit row.

Note: The comment on line 11 gives `"StatusChanged"` and `"Assigned"` as examples, but the code actually writes `"Created"` and the `WorkflowAction` names (`"Route"`, `"Assign"`, ...). `IpAddress` is never filled in yet.

**Committee might ask:**
- *Q: Why no foreign keys on the audit log? Isn't that bad design?* A: It's deliberate. An audit log must outlive the data it describes. With foreign keys, deleting a user would be blocked by the log or would remove the log. Plain Ids keep history independent. We trade database-enforced integrity for durability, and the Ids are always written by our own code in the same transaction.
- *Q: Why isn't AuditLog a BaseEntity?* A: It's append-only. `UpdatedAt` and `IsDeleted` don't make sense for a row that is never changed or deleted.
- *Q: Can a status change happen without an audit row?* A: No. Both are added before one `SaveChangesAsync`, which is one database transaction.

---

## [Enums](../../TicketingSystem.Domain/Enums/): UserRole, TicketStatus, Priority, SnapshotKind, ResponseStatus, WorkflowAction

**What they are:** Fixed lists of named values. An **enum** is a type with a closed set of names (under the hood they are numbers 0, 1, 2...). Using them instead of strings means a typo like `"Routd"` is a compile error, not a bug found at runtime.

**The code:**

[UserRole.cs](../../TicketingSystem.Domain/Enums/UserRole.cs)
```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Enums
{
    public enum UserRole {
        Client, TeamsManager,
        TeamLeader, TeamMember, Admin 
    }
}
```

[TicketStatus.cs](../../TicketingSystem.Domain/Enums/TicketStatus.cs)
```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Enums
{
    public enum TicketStatus
    {
        Submitted, Routed, InProgress,
        LeaderReview, ManagerReview,
        ResponseSent, Closed, Rejected, Reopened
    }
}
```

[Priority.cs](../../TicketingSystem.Domain/Enums/Priority.cs)
```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Enums
{
    public enum Priority { 
        Low, Medium,
        High, Critical 
    }
}
```

[SnapshotKind.cs](../../TicketingSystem.Domain/Enums/SnapshotKind.cs)
```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Enums
{
    public enum SnapshotKind { Before, After }
}
```

[ResponseStatus.cs](../../TicketingSystem.Domain/Enums/ResponseStatus.cs)
```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TicketingSystem.Domain.Enums
{
    public enum ResponseStatus
    {
        Draft, Approved, Sent
    }
}
```

[WorkflowAction.cs](../../TicketingSystem.Domain/Enums/WorkflowAction.cs)
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

**Line by line:**
- Lines 1-3 in the first five files: template `using` lines, not needed (same as in `BaseEntity`).
- **`UserRole`**: the five roles from the brief. Each `User` has exactly one. `Admin` exists in the list and the seeder creates an admin user. Today an admin can see every ticket (like a teams manager, in `TicketVisibility`), but admin screens are cut from the MVP.
- **`TicketStatus`**: every state a ticket can be in. The normal path is `Submitted -> Routed -> InProgress -> LeaderReview -> ManagerReview -> ResponseSent -> Closed`. Side paths: `Submitted -> Rejected` (manager refuses it), `LeaderReview` or `ManagerReview -> InProgress` (sent back), `ResponseSent -> Reopened -> InProgress` (client not happy, leader assigns again). `Closed` and `Rejected` are the end states. The enum only lists the states; which moves are allowed is in `TicketWorkflow` (chapter 3).
- **`Priority`**: four levels. `Medium` is the default on `Ticket`.
- **`SnapshotKind`**: `Before` (the client's code when submitting) or `After` (a member's fix).
- **`ResponseStatus`**: `Draft` when a member submits, `Sent` when the manager approves. `Approved` is in the list but not used by the current code (see the note under `TicketResponse`).
- **`WorkflowAction`**: one name per button in the workflow, ten in total. Each is a key in the `TicketWorkflow` rules table, and each has its own endpoint (`POST /api/tickets/{id}/<step>`). Its `ToString()` is what goes in `AuditLog.Action` and in the `allowedActions` list the frontend uses to decide which buttons to show. It lives in Domain (not Application) because it is a pure name list with no logic, used by the rules, the audit log and the DTOs.
- **How they are stored:** every enum column is configured with `.HasConversion<string>()`, so SQL Server stores `"TeamLeader"` and `"InProgress"`, not `2` and `2`. This makes the data readable in SSMS, and inserting a new value in the middle of an enum can't silently change the meaning of old rows (with numbers, adding a value before `TeamLeader` would turn every stored `2` into a different role).

Note: The formatting differs slightly between files (`{` on the same line in `UserRole` and `Priority`, trailing spaces after `Admin` and `Critical`). It has no effect on the code.

**Committee might ask:**
- *Q: Why store enums as strings in the database?* A: Readable data, and safe if the enum order changes later. The cost (a few bytes per row) doesn't matter at this size.
- *Q: Where is it decided that Routed can go to InProgress but not to Closed?* A: Not in the enum. In the `TicketWorkflow` rules table in Application: one entry per `WorkflowAction`, with the allowed "from" statuses, the "to" status, and who is allowed.
- *Q: Why is there both `ResponseSent` and `Closed`?* A: `ResponseSent` means the team has answered and is waiting for the client. `Closed` means the client accepted it. Between them the client can still reopen.

---

## How the pieces connect

```
User (one Role)
 |
 |-- leads ----------> Team (LeaderId, required)
 |                       |
 |-- member of ------> TeamMember (TeamId + UserId, unique pair) <-- Team.Members
 |
 |-- owns (Client) --> Ticket (ClientId, required)
 |-- routes (Manager)> Ticket (ManagerId, null until routed)
                         |-- TeamId --------> Team (null until routed; leader = Team.Leader)
                         |-- Assignments ---> TicketAssignment (UserId = member, AssignedById = leader)
                         |-- Snapshots -----> CodeSnapshot (1 Before at creation, 0..n After)
                         |-- Responses -----> TicketResponse (Draft -> Sent, ApprovedById = manager)
                         |-- Comments ------> Comment (table only, no UI yet)

AuditLog: TicketId and ActorId are plain Guids. No foreign keys, no navigation.
```

- **User 1 - n Ticket** twice: as the client who owns it (`ClientId`) and as the manager who routed it (`ManagerId`). Users are also referenced by `Team.LeaderId`, `TeamMember.UserId`, `TicketAssignment.UserId` / `AssignedById`, `CodeSnapshot.SubmittedById`, `TicketResponse.AuthorId` / `ApprovedById` and `Comment.AuthorId`. `User` has no collections back to any of these; the configurations use `.WithMany()` with no argument.
- **Team** has one leader (a navigation to `User`) and many members through the `TeamMember` join table. The leader is not a `TeamMember` row.
- **Ticket** is the parent of four collections: `Assignments`, `Snapshots`, `Responses`, `Comments`. Each child has a required `TicketId`.
- **Every foreign key is `DeleteBehavior.Restrict`** in the configurations: the database refuses to delete a row that others still point to. Two reasons, both written in `TeamConfiguration`: SQL Server rejects "multiple cascade paths" (many foreign keys point to Users), and rows are soft-deleted anyway.
- **Enums are stored as text** in every table (`Role`, `Status`, `Priority`, `Kind`).
- **AuditLog has no foreign keys on purpose.** The history must survive whatever happens to the ticket or user it mentions, and it must never block a delete. It is written in the same transaction as the change it records, which is what keeps its Ids correct.
