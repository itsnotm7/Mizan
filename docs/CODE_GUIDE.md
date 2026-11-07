# Mizan: Code Guide

Every part of the code and what it does, in simple words. Written on Nov 7, 2026, while Mon 9 (Tickets) is still in progress.

## The big picture

Mizan is a ticketing system for code review requests. A client asks for a code fix, the fix goes through a team, and every change is approved before it reaches the client.

The code is split into four projects, like the floors of a building. Each one may only use the ones inside it:

| Project | Simple meaning | Can use |
|---|---|---|
| **Domain** | What things *are*: tickets, users, teams | Nothing |
| **Application** | The *rules*: who can do what | Domain |
| **Infrastructure** | The *tools*: database, passwords, tokens | Application |
| **API** | The *front door*: receives web requests | Application, Infrastructure |

**Why this matters:** the rules don't depend on the database or the web. Application only says "I need a way to get tickets" (an interface), and Infrastructure provides it.

## What works today

| Feature | Status |
|---|---|
| Database, tables, demo data | Works |
| Login, tokens, `/me` | Works and tested |
| Checking that the caller's account is still active (piece 1) | Written, not run yet |
| Who can see which ticket (piece 2) | **Stub** |
| Only a client can create a ticket (piece 3) | **Missing** (marked with a comment) |
| `/api/tickets` endpoints | **Not working yet.** List and details hit the piece 2 stub, so they return 500. Create saves a ticket for any role, then returns 500 on the same stub. |
| Workflow, audit logging, React app | Not started |

## Domain: what things are

### Entities (`TicketingSystem.Domain/Entities`)
An entity is a C# class that becomes a database table.

| File | What it is |
|---|---|
| `BaseEntity.cs` | Fields most tables share: `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`. `IsDeleted` means "hidden" without really deleting the row (a soft delete). |
| `User.cs` | Anyone who logs in: name, email, password hash, role, and `IsActive`. |
| `Team.cs` | A team with a name and one leader. |
| `TeamMember.cs` | Links a user to a team. One row means "this user is in this team". |
| `Ticket.cs` | The main thing: title, description, repo link, priority, status, client, manager and team. `RowVersion` stops two people from overwriting each other's save. |
| `TicketAssignment.cs` | "This member works on this ticket, and this leader assigned them." |
| `CodeSnapshot.cs` | A pointer to the code at one moment: repo, branch and commit SHA. `Before` is the code as submitted, `After` is the fix. |
| `TicketResponse.cs` | The answer that goes back to the client. Moves from Draft to Approved to Sent. |
| `Comment.cs` | A comment on a ticket. `IsInternal` hides it from the client. The comments screen is cut from the MVP, but the table exists. |
| `AuditLog.cs` | The history: who did what, and the old and new values. It has no foreign keys on purpose, so history survives even if the ticket or user is removed. |

### Enums (`TicketingSystem.Domain/Enums`)
An enum is a fixed list of allowed values.

| File | Values |
|---|---|
| `UserRole.cs` | Client, TeamsManager, TeamLeader, TeamMember, Admin |
| `TicketStatus.cs` | Submitted, Routed, InProgress, LeaderReview, ManagerReview, ResponseSent, Closed, Rejected, Reopened |
| `Priority.cs` | Low, Medium, High, Critical |
| `SnapshotKind.cs` | Before, After |
| `ResponseStatus.cs` | Draft, Approved, Sent |

## Application: the rules

### DTOs (`TicketingSystem.Application/DTOs`)
A DTO is the shape of data going in or out of the API. We never send entities directly, so private fields like `PasswordHash` can't leak by accident.

| File | What it is |
|---|---|
| `Auth/LoginRequest.cs` | What login receives: email and password, with validation rules. |
| `Auth/LoginResponse.cs` | What login returns: token, expiry time and user. |
| `Auth/UserDto.cs` | What the API shows about a user. No password hash. |
| `Tickets/CreateTicketRequest.cs` | What creating a ticket receives. Checks lengths, that the URL looks like `https://github.com/owner/repo`, that the SHA is 7-64 hex characters, and that the priority really exists. |
| `Tickets/TicketListItemDto.cs` | One row in the ticket list, with short info only. |
| `Tickets/TicketDetailsDto.cs` | The full ticket: client, manager, team, assignees and snapshots. Also holds the small `TicketUserDto` and `CodeSnapshotDto`. |

### Interfaces (`TicketingSystem.Application/Interfaces`)
An interface is a promise: "something will do this job", without saying how.

| File | The promise | Who keeps it |
|---|---|---|
| `IAuthService.cs` | Log in, get the current user | `AuthService` |
| `ITokenService.cs` | Create a token for a user | `JwtTokenService` |
| `ITicketService.cs` | Create a ticket, list visible tickets, get one ticket | `TicketService` |
| `ITicketRepository.cs` | Add, save, list and read tickets from storage | `TicketRepository` |
| `IUserRepository.cs` | Read a user by id | `UserRepository` |

### Services (`TicketingSystem.Application/Services`)

**`TicketService.cs`** is the logic for tickets:
- `CreateAsync`: loads the caller, builds the ticket and its `Before` snapshot, then saves both together. Either both are saved or neither is.
- `GetVisibleTicketsAsync`: loads the caller and returns only the tickets they may see.
- `GetByIdAsync`: same, for one ticket. Returns 404 if it doesn't exist or the caller may not see it.
- `GetActingUserAsync`: **piece 1.** Reads the caller from the database on every request. If the account is missing, deleted or inactive, it refuses with 401. A valid token alone isn't enough.
- The `To...Dto` methods copy entity data into DTOs.

**`TicketVisibility.cs`** is the single place that will decide who sees which ticket. **It's a stub (piece 2).**

### Exceptions (`TicketingSystem.Application/Exceptions`)
Application doesn't know about HTTP, so it throws one of these and the API turns it into a status code.

| File | Means | Becomes |
|---|---|---|
| `UnauthorizedException.cs` | This account is no longer valid | 401 |
| `ForbiddenException.cs` | We know you, but you may not do this | 403 |
| `NotFoundException.cs` | It doesn't exist, or you may not see it | 404 |

### `DependencyInjection.cs`
`AddApplication()` tells .NET: "when someone asks for `ITicketService`, give them `TicketService`."

## Infrastructure: the tools

### Database (`TicketingSystem.Infrastructure/Persistence`)

| File | What it does |
|---|---|
| `AppDbContext.cs` | The connection to the database, with one `DbSet` per table. Sets `UpdatedAt` automatically on every save. |
| `DbSeeder.cs` | Fills an empty database with demo data in Development: 6 users and "Backend Team". |
| `Repositories/TicketRepository.cs` | Keeps the `ITicketRepository` promise. Runs the real EF queries, skips soft-deleted rows and loads related data. |
| `Repositories/UserRepository.cs` | Keeps the `IUserRepository` promise. Reads one user by id. |
| `Migrations/` | Generated scripts that create the tables. `InitialCreate` is the only one so far. |

### Table rules (`Persistence/Configurations`)
There is one file per entity. They set column sizes, relationships and indexes. Rules that apply everywhere:
- Enums are stored as text, like `TeamLeader`, so they're readable in the database.
- Deleting a row that others depend on is blocked (`DeleteBehavior.Restrict`).

| File | Special rules |
|---|---|
| `UserConfiguration.cs` | Email is unique, because it's the login. |
| `TeamConfiguration.cs` | Team name is unique. |
| `TeamMemberConfiguration.cs` | A user can be in a given team only once. |
| `TicketConfiguration.cs` | Column sizes, `RowVersion`, and indexes on the columns we filter by. |
| `TicketAssignmentConfiguration.cs` | A member can be assigned to a ticket only once. |
| `CodeSnapshotConfiguration.cs` | Column sizes. `CommitSha` is at most 64 characters. |
| `TicketResponseConfiguration.cs` | Summary is at most 4000 characters. |
| `CommentConfiguration.cs` | Body is at most 4000 characters. |
| `AuditLogConfiguration.cs` | No relationships, and indexes so a ticket's history loads quickly. |

### Login tools (`TicketingSystem.Infrastructure/Auth`)

| File | What it does |
|---|---|
| `JwtOptions.cs` | Reads the token settings. The app refuses to start if the signing key is missing or shorter than 32 bytes. |
| `JwtTokenService.cs` | Creates the signed token, holding the user's id (`sub`), email and role. |
| `AuthService.cs` | Checks the email and password. Takes about the same time for unknown emails, so timing doesn't reveal which emails exist. |

### `DependencyInjection.cs`
`AddInfrastructure()` registers the database, auth services and repositories.

## API: the front door

| File | What it does |
|---|---|
| `Program.cs` | The app's starting point. Registers everything and sets the order each request goes through. Every endpoint needs a login unless marked `[AllowAnonymous]`. |
| `Controllers/AuthController.cs` | `POST /api/auth/login` and `GET /api/auth/me`. |
| `Controllers/TicketsController.cs` | `POST /api/tickets`, `GET /api/tickets` and `GET /api/tickets/{id}`. Thin on purpose: it takes the caller's id from the token and calls the service. |
| `Extensions/AuthExtensions.cs` | Sets how tokens are checked: signature, issuer, audience, expiry and allowed algorithm. |
| `Extensions/ClaimsPrincipalExtensions.cs` | `User.GetUserId()` reads the user's id from the token. |
| `Middleware/GlobalExceptionHandler.cs` | Catches errors and returns a standard error response. Turns our 3 exceptions into 401, 403 and 404. Anything else becomes a 500, logged without showing details to the client. |
| `OpenApi/BearerSecurityTransformer.cs` | Makes Swagger UI show its Authorize button. |
| `TicketingSystem.API.http` | Ready-made test requests for login and tickets. |
| `appsettings.json` | Database connection and token settings, but never the signing key. |
| `appsettings.Development.json` | Logging settings for Development. |
| `Properties/launchSettings.json` | Local addresses, like `http://localhost:5199`. |

## Other files

| File | What it is |
|---|---|
| `TicketingSystem.slnx` | The solution file that groups the 4 projects. |
| `*.csproj` | One per project: its packages and which projects it can use. |
| `CLAUDE.md` | The project brief, plan and rules. |
| `docs/PRESENTATION_NOTES.md` | Talking points and likely committee questions. |
| `docs/CODE_GUIDE.md` | This file. |
| `docs/SESSION_NOTES_2026-11-07.md` | What we did on Nov 7 and exactly where we stopped. |
| `.claude/agents/code-reviewer.md` | Claude helper that reviews finished features. |
| `.claude/agents/explainer.md` | Claude helper that explains code in simple terms. |
| `.claude/settings.json` | Pre-approved commands for Claude. |

The signing key is **not** in the project. It's in user-secrets on this machine.

## How a request travels

### Logging in
1. React or Swagger sends the email and password to `POST /api/auth/login`.
2. `AuthController` checks the input and calls `AuthService`.
3. `AuthService` finds the user and checks the password hash.
4. `JwtTokenService` creates a signed token.
5. The client sends that token with every later request.

### Listing tickets (once piece 2 is in)
1. The client sends `GET /api/tickets` with the token.
2. JwtBearer checks the token's signature and expiry. A bad token gets 401 here.
3. `TicketsController` takes the user's id from the token and calls `TicketService`.
4. `GetActingUserAsync` reads the user from the database. An inactive account gets 401.
5. `TicketVisibility` builds the rule for that user's role.
6. `TicketRepository` runs the query, and SQL returns only the allowed tickets.
7. `TicketService` copies them into DTOs, and the controller returns them.

## Words you'll be asked about

| Word | Simple meaning |
|---|---|
| **JWT** | A signed token that proves who you are. Anyone can read it, but only the server can create a valid one. |
| **Signature** | A value made from the token's contents and the secret key. Change anything and it no longer matches. |
| **Hash** | A one-way scramble. Passwords are stored this way and can't be turned back into plain text. |
| **Entity** | A class that maps to a database table. |
| **DTO** | The shape of data that goes in or out of the API. |
| **Interface** | A promise of what something can do, without saying how. |
| **Repository** | A class whose only job is reading and saving data. |
| **Dependency injection** | .NET creates the objects a class needs and passes them in. |
| **Migration** | A generated script that updates the database structure. |
| **Soft delete** | Marking a row as deleted (`IsDeleted`) without removing it. |
| **Expression** | Code stored as a description, so EF can turn it into SQL. |
| **401 / 403 / 404** | Don't know who you are / not allowed / not found. |
