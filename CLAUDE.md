# Mizan (ميزان) — CLAUDE.md

Read this whole file before doing anything. It is the project brief, the plan, and the rules.

## Who you're working with
- **Mahmoud**, junior .NET developer (CoderZ ASP.NET Core Full-Stack graduate). This is his **CoderZ final project**, and he may sell it to a real company afterwards.
- **Presentation: Saturday, Nov 21, 2026** (pushed back from Nov 14). He must be able to explain and defend every file in front of a committee.
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
  - `TicketingSystem.Tests`: xUnit, references Application + Domain. Unit tests for `TicketWorkflow` and `TicketVisibility` (no database). `TicketWorkflowLaterStepsTests.cs` is Mahmoud's file.
- **Dependency rule: arrows point inward only.** Never make Domain or Application reference Infrastructure/API. Application defines interfaces; Infrastructure implements them.

## Current state (backend frozen, Nov 15)
DONE:
- Domain entities: `BaseEntity` (Guid Id, CreatedAt, UpdatedAt, IsDeleted), `User`, `Team`, `TeamMember`, `Ticket` (with RowVersion), `TicketAssignment`, `CodeSnapshot`, `TicketResponse`, `Comment`, `AuditLog` (not a BaseEntity, no FKs on purpose).
- Enums: `UserRole` (Client, TeamsManager, TeamLeader, TeamMember, Admin), `TicketStatus`, `Priority`, `SnapshotKind`, `ResponseStatus` (Draft, Approved, Sent).
- Infrastructure: `Persistence/AppDbContext.cs` (stamps UpdatedAt in SaveChangesAsync), `Persistence/Configurations/*` (one per entity, all FKs `Restrict`, enums stored as strings), `Persistence/DbSeeder.cs` (6 demo users, one per role + extra member, and "Backend Team"; password hashed with `PasswordHasher<User>`), `DependencyInjection.cs` (`AddInfrastructure`).
- Migration `InitialCreate` applied to LocalDB. The seeder runs on startup in Development.
- Auth: `POST /api/auth/login`, `GET /api/auth/me`, JwtBearer validation, `MapControllers().RequireAuthorization()` (every controller needs a login unless `[AllowAnonymous]`), Swagger UI at `/swagger`, global `ProblemDetails` errors. Claude's API test script passed; see open items below.
- Git: repo on `main`, remote `origin` = https://github.com/itsnotm7/Mizan (private). Ask before pushing. `git` isn't on PATH, so use Visual Studio's copy: `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe`. Commit identity is set for this repo only.

- Tickets: `POST /api/tickets` (Client only, saves the Before snapshot), `GET /api/tickets` (filtered by role), `GET /api/tickets/{id}` (404 if not visible). Application holds `TicketService`, `TicketVisibility` (the one visibility rule) and the repository interfaces. Infrastructure implements `TicketRepository` and `UserRepository`. Every ticket call reloads the user from the database (inactive gets 401, role from the database). `UnauthorizedException`, `ForbiddenException` and `NotFoundException` map to 401, 403 and 404 in `GlobalExceptionHandler`. 31 scenarios tested.

- Workflow + audit: `TicketWorkflow` (the one rules table), `TicketWorkflowService` (`RunAsync` pipeline), 10 `POST /api/tickets/{id}/<step>` endpoints, `GET /api/tickets/{id}/history`, `GET /api/teams`, and `allowedActions` plus responses in ticket details. 67 end-to-end checks pass, code review done. Mahmoud walked through it line by line on Nov 14–15. Details and points to rehearse are in `docs/SESSION_NOTES_2026-11-08.md`.

**Backend is frozen.** Only bug fixes from here.

- React day 1 (Nov 15): `mizan-web/` (Vite 6, React 19, React Router 7, Bootstrap 5, JavaScript). The Vite proxy sends `/api` to `http://localhost:5199`, so no CORS. `UseHttpsRedirection` runs only outside Development. `src/api/client.js` (one `api()` wrapper, `ApiError`, logout on 401), `src/auth/AuthContext.jsx` (token in sessionStorage, `/me` check after a refresh), `RequireAuth`, `Layout`, `LoginPage` (demo-account buttons), `TicketsPage` (list). Tested in the browser. Run it with `.claude/launch.json` (`api` + `web`) or `npm run dev` in `mizan-web`.

- React days 2+3 (done early, Nov 15): `CreateTicketPage` (`/tickets/new`, field errors from the server, trimmed input) and `TicketDetailsPage` (`/tickets/:id`): info, snapshots, responses, history, and `ActionsPanel`, which draws one control per name in `allowedActions`. `workflow/actions.js` holds display only (path, label, colour). Route, assign and submit have small forms. The full chain was run in the browser.

- Unit tests (Nov 15): `TicketingSystem.Tests`, 34 tests pass (Route/Reject/Assign, AllowedActions, Visibility), mutation check done. Mahmoud is writing the later-step tests in `TicketWorkflowLaterStepsTests.cs`; review them when he's done. Walkthrough chapter 7.

- Visual polish (Nov 16): navy + gold theme in `index.css` (Bootstrap CSS variables, no Sass), `bootstrap-icons` + `@fontsource/inter`/`cairo` (bundled, works offline), `Logo`, split login page, display-only `WorkflowStepper` on ticket details, `HistoryList` as a timeline, icons on actions. Checked in the browser (full chain, Rejected, Reopened, Closed, 375px). Code review done, walkthrough chapter 6 updated.

- Later-step tests (Nov 16): Mahmoud asked Claude to write them. 59 tests pass (all 10 actions + a full-chain test), second mutation check done. Walkthrough 7.5 explains one test of each kind; Mahmoud still needs to walk through it.
- Logout fix (Nov 16): after a logout, the next account no longer lands on the previous user's page. An expired session shows a message.

- Small open items (Nov 16): Swagger Authorize tested in the browser (200 on `/me`). `/me` and `/tickets` return 401 for a user deactivated after login (tested with Member Two, reactivated after). Three stale comments fixed (comments only), walkthrough README updated.

- Final UI run (Nov 19, a day late): full chain by clicking as every role, including a leader send-back and resubmit, Reject, the leader's view of a Reopened ticket, Member Two blocked (404) on a ticket they aren't assigned to, form validation, 375px and 1366px widths. No functional bugs. Two cosmetic fixes: the repo URL error message is worded for people, and the route dropdown is stacked above its button so the team name fits. API logs had no errors. **Frontend frozen.**

NOT DONE: Mahmoud walking through the tests (7.5), rehearsing with the slides. Optional later: a styled confirm dialog instead of `window.confirm`.

## Next up
1. **Rehearsal** (Fri 20). Slides: first draft done Nov 19, a 14-slide Claude Slides deck (navy + gold, speaker notes on every slide) at https://claude.ai/artifact/WnUWW87EhgJdAppxpNHhg5. Private until Mahmoud shares it; it exports to PPTX/PDF. Open items: his full name on the cover, the backup screenshots of the demo flow.

- Demo data (Nov 19): `scripts/demo-data.ps1` clears the tickets and creates 9 through the real API, one per status (the VAT ticket has a manager send-back). Run it with the API running to reset before a rehearsal. For the live demo: "Password reset email never arrives" is Submitted (manager routes), "Orders API returns 500…" is Routed (leader assigns), "Search returns duplicate products" is Response Sent (client decides). Repo links point to a made-up `github.com/demo-client/...`, so clicking them gives a GitHub 404. Rehearsal Fri 20. Note: the demo DB ticket "Search returns duplicate products" is now Reopened (used for testing the stepper).

`docs/STUDY_GUIDE.md` is Mahmoud's study plan until Nov 21 (React in .NET terms, non-code topics, demo script, self-quiz); when he says "quiz me on part X", ask fresh questions, not the ones in the file. `docs/CODE_GUIDE.md` explains every file in simple words. `docs/code-walkthrough/` (6 chapters + README) shows the real code of every file with line-by-line explanations and committee Q&A. Keep both updated after each feature: when code changes, update the copied code and the "Line N" references in the matching chapter.

Open items:
- **Not tested:** `Add-Migration` with the startup key check (the `dotnet ef` CLI isn't installed, so confirm in Package Manager Console at the next migration; none planned before the presentation).
- **Decided (Nov 16):** `System.Security.Cryptography.Pkcs` stays. It's a security pin: without it the SQL Server driver brings in the vulnerable 9.0.11 (NU1903). Mahmoud chose "remove", the build showed why it's needed, and it was put back with a comment.
- **Permissions:** `.claude/settings.json` pre-approves `Bash(...)` commands, so PowerShell sessions may still prompt.

## The plan (presentation Sat Nov 21)
| Date | Goal |
|---|---|
| Sat 7 | ✅ Entities, DbContext, migration, seed |
| Sun 8 | ✅ **Auth**: login → JWT, JwtBearer, roles, `/me`, Swagger with bearer token. Also ✅ **Tickets** (create, list by role, details). |
| Nov 8–15 | ✅ **Workflow** endpoints + audit logging, walkthrough done. **Backend freeze (Nov 15).** |
| Sun 15 | ✅ React setup, login, list. ✅ Also (ahead of plan): create form, details page with all workflow actions, history, full chain run in the browser. |
| Mon 16–Tue 17 | Visual polish, bug fixes (buffer gained by finishing early) |
| Wed 18 | Final full run through the UI. **Frontend freeze.** |
| Thu 19 | Demo data, slides |
| Fri 20 | Rehearsal + buffer |
| Sat 21 | **Presentation** |

If a day slips, **cut scope, don't move the freeze.**

## MVP scope
KEEP: login + roles (seeded users, no registration screen), create ticket, the full workflow chain, audit log, before/after snapshots, React: login, list, details + actions, create form.
CUT (mention as "future work" in the presentation): comments UI, attachments, notifications/email/SignalR, admin screens (seed teams instead), refresh tokens, integration tests (unit tests for the rules exist), rate limiting, soft-delete query filters.

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
- Config/secrets: JWT signing key in user-secrets only, because `appsettings.Development.json` is tracked by git. Never hardcode it in code. The app won't start without it, so a new machine needs: `dotnet user-secrets set "Jwt:SigningKey" "<at least 32 characters>" --project TicketingSystem.API`.
- New EF changes go through a migration. **Visual Studio Package Manager Console** (Mahmoud's preferred way), with Default project = Infrastructure and Startup = API: `Add-Migration <Name> -OutputDir Persistence/Migrations`, then `Update-Database`.
  CLI equivalent from `F:\Ticketing`: `dotnet ef migrations add <Name> -p TicketingSystem.Infrastructure -s TicketingSystem.API -o Persistence/Migrations`.
- Package versions: match the existing 10.0.x versions in the csproj files. Exceptions: `Microsoft.IdentityModel.JsonWebTokens` 8.19.2 (the version `JwtBearer` 10.0.13 uses) and `Swashbuckle.AspNetCore.SwaggerUI` 10.3.1, which version separately.

## How to work (be efficient, we're short on time)
1. **Start each day's feature in plan mode.** Propose a short plan (files to add/change), get Mahmoud's OK, then build it.
2. **Use subagents in parallel** where it saves time: an Explore agent to sweep the codebase, the `code-reviewer` agent (in `.claude/agents/`) after each feature, and the `explainer` agent when Mahmoud asks "explain this".
3. **Run `dotnet build` from `F:\Ticketing` after every change** and fix errors yourself before reporting back. Target: 0 errors, 0 warnings. Then `dotnet test` (stop the running API first, or the build can't overwrite its DLLs).
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
