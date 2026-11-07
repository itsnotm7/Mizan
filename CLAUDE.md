# Mizan (ميزان) — CLAUDE.md

Read this whole file before doing anything. It is the project brief, the plan, and the rules.

## Who you're working with
- **Mahmoud**, junior .NET developer (CoderZ ASP.NET Core Full-Stack graduate). This is his **CoderZ final project**, and he may sell it to a real company afterwards.
- **Presentation: Saturday, Nov 14, 2026.** He must be able to explain and defend every file in front of a committee.
- Communication: casual, direct, concise. English or Arabic (Levantine) are both fine.
- Machine: Windows, Visual Studio + VS Code, SQL Server **LocalDB** `(localdb)\MSSQLLocalDB`, database `MizanDb`.

## What Mizan is
A ticketing system for **code review / correction requests**.
A **Client** submits a ticket (description + GitHub repo link). The **Teams Manager** routes it to a team. The **Team Leader** assigns **Team Members**. Members fix the code and submit the result. The result goes back up for approval (Leader, then Manager), then the response is sent to the Client, who closes or reopens the ticket. Everything is recorded in an **audit log**, with "before" and "after" code snapshots (commit SHAs).
The name means "balance/scale": every code change is weighed and approved before it reaches the client.

## Stack and structure
- ASP.NET Core Web API (.NET 10), Clean Architecture, EF Core 10 + SQL Server, JWT auth. Frontend: React (Vite), to be created at `F:\Ticketing\mizan-web\`.
- Solution: `F:\Ticketing\TicketingSystem.slnx` (root). Projects:
  - `TicketingSystem.Domain`: entities + enums. **No dependencies on other projects.** (It has the EF Abstractions package. Mahmoud wants it for annotations later; leave it.)
  - `TicketingSystem.Application`: references Domain. DTOs, service interfaces, business/workflow logic go here.
  - `TicketingSystem.Infrastructure`: references Application. EF `AppDbContext`, configurations, seeder, implementations of Application interfaces. Has `FrameworkReference Microsoft.AspNetCore.App`.
  - `TicketingSystem.API`: references Application + Infrastructure. Controllers, auth setup, `Program.cs`.
- **Dependency rule: arrows point inward only.** Never make Domain or Application reference Infrastructure/API. Application defines interfaces; Infrastructure implements them.

## Current state (as of Nov 7)
DONE:
- Domain entities: `BaseEntity` (Guid Id, CreatedAt, UpdatedAt, IsDeleted), `User`, `Team`, `TeamMember`, `Ticket` (with RowVersion), `TicketAssignment`, `CodeSnapshot`, `TicketResponse`, `Comment`, `AuditLog` (not a BaseEntity, no FKs on purpose).
- Enums: `UserRole` (Client, TeamsManager, TeamLeader, TeamMember, Admin), `TicketStatus`, `Priority`, `SnapshotKind`, `ResponseStatus` (Draft, Approved, Sent).
- Infrastructure: `Persistence/AppDbContext.cs` (stamps UpdatedAt in SaveChangesAsync), `Persistence/Configurations/*` (one per entity, all FKs `Restrict`, enums stored as strings), `Persistence/DbSeeder.cs` (6 demo users, one per role + extra member, and "Backend Team"; password hashed with `PasswordHasher<User>`), `DependencyInjection.cs` (`AddInfrastructure`).
- Migration `InitialCreate` applied to LocalDB. The seeder runs on startup in Development.

NOT DONE: auth, any controllers/endpoints, Application layer content, React app, git status unknown (check it; if no repo exists, help Mahmoud create a **private** GitHub repo early).

## The plan (backend frozen by end of Tue Nov 10)
| Date | Goal |
|---|---|
| Sat 7 | ✅ Entities, DbContext, migration, seed |
| Sun 8 | **Auth**: `POST /api/auth/login` → JWT (claims: sub=UserId, email, role). JwtBearer setup, role-based `[Authorize(Roles=...)]`, `GET /api/auth/me`. Swagger/OpenAPI usable with a bearer token. |
| Mon 9 | **Tickets**: create (Client), list filtered by role, details. DTOs, validation. |
| Tue 10 | **Workflow** endpoints + audit logging. Run the full flow end-to-end. **Backend freeze.** |
| Wed 11 | React (Vite) setup, CORS, login page, ticket list |
| Thu 12 | Ticket details page with role-based action buttons, create-ticket form |
| Fri 13 | Demo data, bug fixes, slides, rehearsal |

If a day slips, **cut scope, don't move the freeze.**

## MVP scope
KEEP: login + roles (seeded users, no registration screen), create ticket, the full workflow chain, audit log, before/after snapshots, React: login, list, details + actions, create form.
CUT (mention as "future work" in the presentation): comments UI, attachments, notifications/email/SignalR, admin screens (seed teams instead), refresh tokens, automated tests, rate limiting, soft-delete query filters.

## Workflow (the core business logic)
Status transitions. Enforce them in ONE place (a domain method or a single workflow service), never scattered in controllers:
- `Submitted → Routed`: TeamsManager routes to a team (sets ManagerId, TeamId). Or `Submitted → Rejected` by TeamsManager.
- `Routed → InProgress`: the leader of that team assigns ≥1 members (TicketAssignment rows).
- `InProgress → LeaderReview`: an assigned member submits the fix (an "After" CodeSnapshot + a Draft TicketResponse).
- `LeaderReview → ManagerReview` (leader approves) or `→ InProgress` (leader sends back).
- `ManagerReview → ResponseSent` (manager approves: response becomes Sent, SentAt set, ApprovedById set) or `→ InProgress` (sent back).
- `ResponseSent → Closed` (client accepts; set ClosedAt) or `→ Reopened` (client).
- `Reopened → InProgress` (leader re-assigns / continues).
- The "Before" snapshot is captured when the ticket is created.
- Every transition writes an `AuditLog` row (ActorId, Action, OldValue → NewValue).
- Permission checks go beyond the role: e.g. only the leader **of that ticket's team** can assign, and only an **assigned** member can submit.

## Conventions
- Controllers stay thin: validate, call an Application service, return a DTO. **Never return entities** from the API.
- Application: `Interfaces/`, `DTOs/`, `Services/` (or feature folders). Infrastructure implements the interfaces and is registered in `DependencyInjection.cs`. Add an `AddApplication()` extension the same way.
- Async everywhere, `CancellationToken` passed through.
- Errors: consistent `ProblemDetails`. A simple global exception handler is enough.
- Config/secrets: JWT signing key in `appsettings.Development.json` (or user-secrets). Never hardcode it in code.
- New EF changes go through a migration. **Visual Studio Package Manager Console** (Mahmoud's preferred way), with Default project = Infrastructure and Startup = API: `Add-Migration <Name> -OutputDir Persistence/Migrations`, then `Update-Database`.
  CLI equivalent from `F:\Ticketing`: `dotnet ef migrations add <Name> -p TicketingSystem.Infrastructure -s TicketingSystem.API -o Persistence/Migrations`.
- Package versions: match the existing 10.0.x versions in the csproj files.

## How to work (be efficient, we're short on time)
1. **Start each day's feature in plan mode.** Propose a short plan (files to add/change), get Mahmoud's OK, then build it.
2. **Use subagents in parallel** where it saves time: an Explore agent to sweep the codebase, the `code-reviewer` agent (in `.claude/agents/`) after each feature, and the `explainer` agent when Mahmoud asks "explain this".
3. **Run `dotnet build` from `F:\Ticketing` after every change** and fix errors yourself before reporting back. Target: 0 errors, 0 warnings.
4. Test endpoints yourself where possible (run the API, call it with curl/`.http` file) before saying a feature works.
5. Use a task list for each day so progress is visible.
6. Commit after each working feature, with a clear message. Ask before pushing.
7. Keep replies short. Show what changed and what Mahmoud should check, not long recaps.

## Mentoring rules (non-negotiable)
- **You write the boilerplate** (auth setup, DI wiring, DTO mapping, React setup, CSS). **Mahmoud owns the core logic** (workflow transitions, permission checks). For those, propose the structure and let him write it or walk through it line by line with him.
- After each feature, add 3–6 bullet points to `docs/PRESENTATION_NOTES.md`: what it does, why it was designed that way, and likely committee questions with short answers.
- Before moving to the next feature, make sure Mahmoud can explain what was just built. Ask him one quick question about it.
- Add short comments on non-obvious lines (the *why*, not the *what*), like the existing code does.
- If he wants to skip understanding something, remind him once that the committee will ask, then respect his call.
