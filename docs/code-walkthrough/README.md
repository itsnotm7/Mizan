# Mizan: Code Walkthrough

This walkthrough explains the **actual code** of Mizan, file by file. For each file you get the real code, a line-by-line explanation of what it does and **why** it's written that way, and the questions a committee is likely to ask.

It sits next to two other documents:
- [CODE_GUIDE.md](../CODE_GUIDE.md): what every file is for, in plain words, with no code. Start there if you want the big picture fast.
- [PRESENTATION_NOTES.md](../PRESENTATION_NOTES.md): talking points per feature for the slides.

## Chapters

| # | Chapter | What it covers |
|---|---|---|
| 1 | [Domain: what things are](01-domain.md) | Entities (Ticket, User, Team, ...) and enums. The center of the onion: depends on nothing. |
| 2 | [Application: the rules](02-application.md) | DTOs, interfaces, exceptions, the visibility rule, ticket and team services. |
| 3 | [The workflow: the heart of Mizan](03-workflow.md) | The rules table (`TicketWorkflow`), the 6-step pipeline (`RunAsync`), and every workflow step. **Read this one twice.** |
| 4 | [Infrastructure: the tools](04-infrastructure.md) | EF Core (DbContext, table configurations, repositories), the seeder, password hashing and JWT creation. |
| 5 | [API: the front door](05-api.md) | `Program.cs` and the middleware order, JWT validation, the exception handler, controllers. |
| 6 | [Frontend: the React app](06-frontend.md) | The API client, login state, pages, the action buttons driven by `allowedActions`. |
| 7 | [Tests: proving the rules hold](07-tests.md) | xUnit tests for the rules table and the visibility rule. No database needed. Run with `dotnet test`. |

## Suggested reading order

1. **Chapter 1** (short) so you know the nouns: Ticket, User, Team, Snapshot, Response, AuditLog.
2. **Chapter 3** next, because it's what the committee will dig into. It refers back to chapter 2 where needed.
3. **Chapters 2, 4 and 5** to see how a request gets in and how data is saved.
4. **Chapter 6** last, to show how the UI uses all of it.

## The one-paragraph story

A **client** creates a ticket with a GitHub repo and commit (the "Before" snapshot). A **teams manager** routes it to a team. That team's **leader** assigns **members**. A member submits a fix (an "After" snapshot plus a draft response). The leader, then the manager, approve it or send it back. When the manager approves, the response is sent, and the client closes the ticket or reopens it. Every step is checked in **one rules table**, saved in **one transaction** together with an **audit log** row, and the frontend only shows the buttons the server says are allowed.

## Small things the walkthrough noticed (your call)

While writing these chapters, the writers marked a few oddities with "Note:" lines. None of them breaks anything. Know them before the committee does.

**Fixed (Nov 16):** three stale comments now match the code: the response path is Draft → Sent (`TicketResponse`, `ResponseStatus`), the real `AuditLog.Action` names, and "in time order" in `AuditLogConfiguration`. Comments only, no behaviour change.

**Unused on purpose (say "designed ahead, cut from the MVP")**
- `ResponseStatus.Approved`: kept for a later "approve now, send later" step. Removing an enum value is riskier than keeping one, because enums are stored as strings. *(chapter 1)*
- `Ticket.DueDate`, `ArchivePath`, `AuditLog.IpAddress` and `TicketAssignment.AssignedAt` are never set. `Comment` has a table but no endpoint (comments are cut scope). The schema was designed up front so these features need no redesign. *(chapter 1)*
- `System.Security.Cryptography.Pkcs` in the Infrastructure csproj is **not** unused by accident. It's a security pin that overrides a vulnerable version (9.0.11) pulled in by the SQL Server driver. Removing it brings back 8 vulnerability warnings, so it stays. *(chapter 4)*

**Design details worth being able to explain**
- `JwtOptions` is read by hand (not `IOptions<T>`), and its startup check runs twice. Harmless. *(chapters 4, 5)*
- `AuthService` uses `AppDbContext` directly rather than `IUserRepository`. *(chapter 2)*
- The seeder creates its own `PasswordHasher` instead of the one registered in DI. *(chapter 4)*
- A login whose hash says "rehash needed" doesn't save a new hash. *(chapter 4)*
- `RunAsync` loads the user and ticket again to build its response: one extra query per action, for simple code. *(chapter 3)*
- A fix that was sent back keeps its After snapshot. The team still sees it; clients never do. *(chapter 3)*
- Clients see a fix's snapshot because Submit gives the snapshot and the response the same timestamp. *(chapters 2, 3)*

## How the layers depend on each other

```
   mizan-web (React)  --HTTP /api-->  API
                                       |
                                       v
                 Infrastructure --> Application --> Domain
```

The arrows point **inward**. Domain knows nothing. Application knows Domain and defines interfaces such as `ITicketRepository`. Infrastructure implements those interfaces with EF Core. The API wires everything together in `Program.cs`.
