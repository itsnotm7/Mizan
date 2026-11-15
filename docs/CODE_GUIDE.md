# Mizan: Code Guide

Every part of the code and what it does, in simple words. For the **actual code explained line by line**, see the [Code Walkthrough](code-walkthrough/README.md). Last updated Nov 15, 2026, after React day 1 (login and ticket list).

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
| Tickets: create, list by role, details | Works, 31 test scenarios passed |
| Workflow (10 steps), audit log, history, teams list | Works, 67 end-to-end checks passed, walked through. Backend frozen. |
| React app | Login, list, create form, and the details page with every workflow button and the history. The full chain was run in the browser. Visual polish still to do. |

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
| `WorkflowAction.cs` | Route, Reject, Assign, Submit, LeaderApprove, LeaderSendBack, ManagerApprove, ManagerSendBack, Close, Reopen |

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
| `Tickets/TicketDetailsDto.cs` | The full ticket: client, manager, team, assignees, snapshots, responses, and `AllowedActions` (the buttons this user may press). Also holds the small `TicketUserDto`, `CodeSnapshotDto` and `TicketResponseDto`. |
| `Tickets/WorkflowRequests.cs` | What the workflow steps receive: a team to route to, members to assign, and the fix to submit (branch, commit SHA, summary). |
| `Tickets/AuditEntryDto.cs` | One line of a ticket's history: when, who, what, and the old and new values. |
| `Teams/TeamDto.cs` | A team with its leader and members, used to pick a team or members. |

### Interfaces (`TicketingSystem.Application/Interfaces`)
An interface is a promise: "something will do this job", without saying how.

| File | The promise | Who keeps it |
|---|---|---|
| `IAuthService.cs` | Log in, get the current user | `AuthService` |
| `ITokenService.cs` | Create a token for a user | `JwtTokenService` |
| `ITicketService.cs` | Create a ticket, list visible tickets, get one ticket | `TicketService` |
| `ITicketWorkflowService.cs` | The 10 workflow steps | `TicketWorkflowService` |
| `ITeamService.cs` | List teams | `TeamService` |
| `ITicketRepository.cs` | Add, save, list and read tickets, add related rows and audit rows, read history | `TicketRepository` |
| `IUserRepository.cs` | Read a user by id, look up names | `UserRepository` |
| `ITeamRepository.cs` | Read teams with their members | `TeamRepository` |

### Services (`TicketingSystem.Application/Services`)

**`TicketService.cs`** is the logic for tickets:
- `CreateAsync`: loads the caller and refuses with 403 unless their role in the database is `Client`. Then it builds the ticket and its `Before` snapshot and saves both together. Either both are saved or neither is.
- `GetVisibleTicketsAsync`: loads the caller and returns only the tickets they may see.
- `GetByIdAsync`: same, for one ticket. Returns 404 if it doesn't exist or the caller may not see it.
- `CreateAsync` also writes a `Created` audit row.
- `GetHistoryAsync`: the ticket's audit log, with names filled in. Clients see only the public steps.
- The `To...Dto` methods copy entity data into DTOs. Clients only get responses that were sent.

**`ActingUserExtensions.cs`:** `GetActingUserAsync` reads the caller from the database on every request. If the account is missing, deleted or inactive, it refuses with 401, so a valid token alone isn't enough. Every service starts with it.

**`TicketWorkflow.cs` is the heart of Mizan.** It's one table of rules. Each row says: this action, from these statuses, to this status, by this person.
- `Apply` checks who first (else 403), then the current status (else 409), then changes the status.
- `AllowedActions` uses the same table to tell the frontend which buttons to show.
- The "who" checks are specific: the leader of *this* ticket's team, a member assigned to *this* ticket, the manager who routed it, the client who owns it.

**`TicketWorkflowService.cs`** has one method per step. They all go through `RunAsync`:
1. Load the user.
2. Load the ticket (404 if they can't see it).
3. Check the rule.
4. Make the step's own changes.
5. Write the audit row.
6. Save it all at once.

What each step changes:
- **Route:** sets the team and the manager.
- **Assign:** adds members from that team only. A member who's already assigned is skipped.
- **Submit:** adds the After snapshot and a Draft response.
- **Send back:** hides the draft.
- **Manager approve:** sends the response to the client.
- **Close:** sets the closing time.

**`TeamService.cs`:** lists teams. The manager and admin see all teams, a leader sees their own, and anyone else gets 403.

**`TicketVisibility.cs`** is the single place that decides who sees which ticket:
- **Client:** their own tickets.
- **TeamsManager and Admin:** all tickets.
- **TeamLeader:** tickets of the teams they lead. A new ticket has no team until it's routed, so no leader sees it yet.
- **TeamMember:** only tickets they're assigned to. Being on the team isn't enough.
- **Any other role:** nothing, so a role added later fails safe.

It returns an `Expression`, which EF turns into SQL, so the database does the filtering.

### Exceptions (`TicketingSystem.Application/Exceptions`)
Application doesn't know about HTTP, so it throws one of these and the API turns it into a status code.

| File | Means | Becomes |
|---|---|---|
| `UnauthorizedException.cs` | This account is no longer valid | 401 |
| `ForbiddenException.cs` | We know you, but you may not do this | 403 |
| `NotFoundException.cs` | It doesn't exist, or you may not see it | 404 |
| `ConflictException.cs` | Not allowed in the ticket's current status, or someone else saved first | 409 |
| `BadRequestException.cs` | Input that is wrong once checked against the database (e.g. a member from another team) | 400 |

### `DependencyInjection.cs`
`AddApplication()` tells .NET: "when someone asks for `ITicketService`, give them `TicketService`."

## Infrastructure: the tools

### Database (`TicketingSystem.Infrastructure/Persistence`)

| File | What it does |
|---|---|
| `AppDbContext.cs` | The connection to the database, with one `DbSet` per table. Sets `UpdatedAt` automatically on every save. |
| `DbSeeder.cs` | Fills an empty database with demo data in Development: 6 users and "Backend Team". |
| `Repositories/TicketRepository.cs` | Keeps the `ITicketRepository` promise. Runs the real EF queries, skips soft-deleted rows and loads related data. `GetForUpdateAsync` loads a ticket that can be changed. New rows for an existing ticket are added explicitly (see the comment in the interface). Turns "someone else saved first" into a 409. |
| `Repositories/UserRepository.cs` | Keeps the `IUserRepository` promise. Reads one user by id, and looks up names for the history. |
| `Repositories/TeamRepository.cs` | Keeps the `ITeamRepository` promise. Reads teams with their leader and members. |
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
| `Controllers/TicketsController.cs` | `POST /api/tickets`, `GET /api/tickets`, `GET /api/tickets/{id}` and `GET /api/tickets/{id}/history`. Thin on purpose: it takes the caller's id from the token and calls the service. |
| `Controllers/TicketWorkflowController.cs` | One `POST /api/tickets/{id}/<step>` per workflow step. It decides nothing: the rules are in `TicketWorkflow`. |
| `Controllers/TeamsController.cs` | `GET /api/teams`. |
| `Extensions/AuthExtensions.cs` | Sets how tokens are checked: signature, issuer, audience, expiry and allowed algorithm. |
| `Extensions/ClaimsPrincipalExtensions.cs` | `User.GetUserId()` reads the user's id from the token. |
| `Middleware/GlobalExceptionHandler.cs` | Catches errors and returns a standard error response. Turns our 5 exceptions into 400, 401, 403, 404 and 409. Anything else becomes a 500, logged without showing details to the client. |
| `OpenApi/BearerSecurityTransformer.cs` | Makes Swagger UI show its Authorize button. |
| `TicketingSystem.API.http` | Ready-made test requests for login and tickets. |
| `appsettings.json` | Database connection and token settings, but never the signing key. |
| `appsettings.Development.json` | Logging settings for Development. |
| `Properties/launchSettings.json` | Local addresses, like `http://localhost:5199`. |

## Frontend: the React app (`mizan-web/`)

The screens people use. It runs on `http://localhost:5173` and only ever talks to the API through `/api/...`.

| File | What it does |
|---|---|
| `vite.config.js` | Vite settings. The **proxy** forwards every `/api` call to the API on `localhost:5199`. The browser sees one address, so CORS never comes up. |
| `index.html` | The one HTML page. React draws everything inside `<div id="root">`. |
| `src/main.jsx` | The starting point: loads Bootstrap, the icons and the fonts, then our theme (`index.css`), then wraps the app in the router and the login state (`AuthProvider`). |
| `src/index.css` | The navy + gold theme. Bootstrap reads its colours from CSS variables, so setting `--bs-primary` and the button variables here re-colours every `btn-primary`, link and badge without Sass. Also the styles Bootstrap doesn't have: the stepper, the history timeline and the login side panel. |
| `src/App.jsx` | The list of pages (routes): `/login` is public, `/tickets` needs a login, anything else goes to `/tickets`. |
| `src/api/client.js` | The **one** place that calls the API. It adds the token, reads the JSON, turns error responses into an `ApiError` with the API's message, and logs you out on a 401. |
| `src/auth/AuthContext.jsx` | Remembers who is logged in and shares it with every page. `login()` calls the API and saves the token in **sessionStorage**. After a refresh it asks `/api/auth/me` whether the saved token is still good. |
| `src/auth/RequireAuth.jsx` | A guard around pages that need a login: no user, go to `/login`. If you opened a link while logged out, it remembers that page so login takes you there. After a logout or an expired session it does **not**, because the next person may be a different account. It only decides what to *show*. The API still checks every request. |
| `src/components/Layout.jsx` | The navy top bar (logo, name, role, Log out) around every logged-in page. |
| `src/components/Logo.jsx` | The Mizan mark: a gold balance scale drawn in SVG, plus "Mizan ميزان". Used in the top bar and on the login page. |
| `src/pages/LoginPage.jsx` | The login form, with buttons that fill in each demo account's email. On wide screens a navy side panel shows the logo and three selling points. |
| `src/pages/TicketsPage.jsx` | The ticket table. The API already filters by role, so it shows whatever comes back. "New ticket" shows for clients only. |
| `src/pages/CreateTicketPage.jsx` | The new-ticket form (clients only). It trims what you typed, sends it, and shows the server's message under each field it rejects. |
| `src/pages/TicketDetailsPage.jsx` | One ticket: its info, code snapshots (linked to the commit on GitHub), responses, your action buttons, and the history. Every button ends up in `handleAction`, which POSTs the step and swaps in the updated ticket the API sends back. |
| `src/workflow/actions.js` | How each workflow action *looks*: its URL, button text, colour and icon, and whether it needs a form or a "are you sure?". It does **not** decide who gets which button: that's `allowedActions` from the server. |
| `src/components/ticket/ActionsPanel.jsx` | Draws one button or small form for each action in `allowedActions`. Reject and Close ask for confirmation first. |
| `src/components/ticket/RouteForm.jsx` | Manager: choose the team (from `GET /api/teams`). |
| `src/components/ticket/AssignForm.jsx` | Leader: tick members of their team. People already on the ticket stay ticked. On a reopened ticket, "Continue with current team" needs no new ticks. |
| `src/components/ticket/SubmitFixForm.jsx` | Member: branch, commit SHA and summary of the fix. The summary becomes the response the client reads. |
| `src/components/ticket/WorkflowStepper.jsx` | The progress bar at the top of a ticket: Submitted → … → Closed, with done steps ticked and the current one in gold. Rejected and Reopened show in red. **Display only**: it reads `ticket.status`, it doesn't decide anything. |
| `src/components/ticket/HistoryList.jsx` | The audit log as a timeline: an icon per action, what happened, when, who did it, old status → new status. The dot takes the colour of the new status. |
| `src/utils/fieldErrors.js` | Finds the server's message for one form field in a 400 response. |
| `src/utils/labels.js` | Colours for status and priority badges, readable names ("LeaderReview" → "Leader Review", audit actions → "Leader sent back"), an icon per audit action, and showing UTC times in local time. |
| `package.json` | The frontend's packages (React, React Router, Bootstrap, Bootstrap Icons, the Inter and Cairo fonts, Vite) and commands (`npm run dev`, `npm run build`). |

**How to run it:** start the API (Visual Studio, `http` profile, or `dotnet run --project TicketingSystem.API --launch-profile http`), then in `mizan-web` run `npm run dev` and open `http://localhost:5173`.

## Other files

| File | What it is |
|---|---|
| `TicketingSystem.slnx` | The solution file that groups the 4 projects. |
| `*.csproj` | One per project: its packages and which projects it can use. |
| `CLAUDE.md` | The project brief, plan and rules. |
| `docs/PRESENTATION_NOTES.md` | Talking points and likely committee questions. |
| `docs/CODE_GUIDE.md` | This file. |
| `docs/SESSION_NOTES_2026-11-07.md` | What we did on Nov 7 and exactly where we stopped. |
| `docs/SESSION_NOTES_2026-11-08.md` | Nov 8: Tickets finished, the workflow built, and the walkthrough order for the workflow. |
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

### Listing tickets
1. The client sends `GET /api/tickets` with the token.
2. JwtBearer checks the token's signature and expiry. A bad token gets 401 here.
3. `TicketsController` takes the user's id from the token and calls `TicketService`.
4. `GetActingUserAsync` reads the user from the database. An inactive account gets 401.
5. `TicketVisibility` builds the rule for that user's role.
6. `TicketRepository` runs the query, and SQL returns only the allowed tickets.
7. `TicketService` copies them into DTOs, and the controller returns them.

### A workflow step (e.g. the leader approves)
1. The client sends `POST /api/tickets/{id}/leader-approve` with the token.
2. `TicketWorkflowController` calls `TicketWorkflowService.LeaderApproveAsync`.
3. `RunAsync` loads the user, then the ticket. A user who can't see the ticket gets 404.
4. `TicketWorkflow.Apply` checks that this is the leader of the ticket's team (else 403) and that the status is LeaderReview (else 409). Then it sets the status to ManagerReview.
5. An audit row records "LeaderApprove: LeaderReview → ManagerReview".
6. One save writes both. The updated ticket comes back with its new `allowedActions`.

### A button click in the React app (the same step, seen from the browser)
1. The details page loads `GET /api/tickets/{id}`. The answer includes `allowedActions`, e.g. `["LeaderApprove", "LeaderSendBack"]`.
2. `ActionsPanel` draws one button per name, using `workflow/actions.js` for the text and colour.
3. The leader clicks **Approve**. `handleAction` sends `POST /api/tickets/{id}/leader-approve` through `api()`.
4. The backend runs the 6 steps above and returns the updated ticket.
5. The page swaps in the new ticket. Its `allowedActions` is now empty for the leader, so the buttons disappear, and the history reloads with the new row.
6. If the API refuses (403/409/400), its message shows in a red alert above the buttons, and nothing changes.

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
| **409 Conflict** | The request is fine, but the ticket's current state doesn't allow it. |
| **Transaction** | Several changes saved together: either all of them happen or none do. |
| **Concurrency token** | A value (`RowVersion`) that changes on every save, so two people can't silently overwrite each other. |
| **Audit log** | A history table that is only added to, never edited. |
