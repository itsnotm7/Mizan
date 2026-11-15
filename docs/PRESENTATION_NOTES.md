# Mizan: Presentation Notes

## Auth (login, JWT, roles)

- **What it does:** `POST /api/auth/login` checks the email and password and returns a signed JWT holding the user's id (`sub`), `email` and `role`. The client sends that token with each request. `GET /api/auth/me` returns the logged-in user.
- **Where it sits:** Application defines `IAuthService` and `ITokenService`. Infrastructure implements them because they need the database and crypto libraries. The controller only calls the interface, so the dependency arrows still point inward.
- **Secure by default:** `MapControllers().RequireAuthorization()` protects every controller. Login is the only endpoint marked `[AllowAnonymous]`, so forgetting `[Authorize]` on a new endpoint can't make it public.
- **Signing key:** it lives in user-secrets, outside the repo. The app refuses to start if the key is missing or shorter than 32 bytes. A new machine needs `dotnet user-secrets set "Jwt:SigningKey" "<key>" --project TicketingSystem.API`.
- **Errors:** JSON clients get errors in the standard `ProblemDetails` format (validation errors add an `errors` field), and a global handler logs exceptions without sending stack traces to the client.

### Likely committee questions

- **Why JWT instead of sessions?** The API stays stateless, so it doesn't store login state, and a React client can send the token in a header. The tradeoff is that a token can't easily be cancelled before it expires, so tokens last only 2 hours.
- **Can a user edit their token to become Admin?** No. The signature covers the contents, so any change makes it invalid and the API returns 401. We tested this, along with `alg:none` tokens.
- **Is the data in the token secret?** No. A JWT is signed, not encrypted, and anyone can read it. That's why it holds no password or other sensitive data.
- **How are passwords stored?** As salted hashes from `PasswordHasher<User>`, never as plain text. Login hashes the submitted password and compares the result.
- **Why the same error for a wrong password and an unknown email?** To make it hard to use the login form to find out which emails exist. Unknown emails also go through a password hash check, so both cases take about the same time (191 ms vs 173 ms in our test).
- **What's the difference between 401 and 403?** 401 means we don't know who you are (no valid token). 403 means we know who you are, but your role isn't allowed.
- **Why does `/me` read the database instead of the token?** The token could be up to 2 hours old. Reading the database means a deactivated user is refused right away on this endpoint.

### Future work

Refresh tokens, logout and token revocation, and rate limiting on login.

## Tickets (create, list, details)

- **What it does:** a client creates a ticket with `POST /api/tickets`, which also saves the "Before" snapshot (repo, branch, commit SHA). `GET /api/tickets` lists the tickets the caller may see, and `GET /api/tickets/{id}` shows one.
- **Where the logic lives:** `TicketService` in Application holds the rules. It gets data through `ITicketRepository` and `IUserRepository`, which Application defines and Infrastructure implements with EF Core. Application has no EF dependency, and the controller only calls the service.
- **The database decides, not the token:** every request reloads the user. A deactivated account gets 401 and the role comes from the database, so a change applies right away instead of after the 2-hour token expires.
- **One visibility rule:** `TicketVisibility.VisibleTo` is used by both list and details. Client sees their own, TeamsManager and Admin see all, a leader sees their team's tickets, and a member sees only tickets they're assigned to. It's an `Expression`, so SQL does the filtering.
- **Safe saving:** the ticket and its snapshot are saved in one `SaveChanges`, so either both are written or neither is. Input limits match the column sizes, so bad input gets a 400 and never reaches the database as a 500.
- **Tested:** 31 scenarios passed, including an unassigned member opening a ticket by ID (404), a deactivated user (401) and a changed role (403).

### Likely committee questions

- **Why 404 and not 403 when a user opens someone else's ticket?** A 403 would confirm the ticket exists. A 404 gives the same answer for "doesn't exist" and "not yours." Creating a ticket returns 403, because that endpoint has nothing to hide.
- **Why not `[Authorize(Roles = "Client")]` on create?** That reads the role from the token, which can be up to 2 hours old. The service checks the current role in the database, so there's one source of truth.
- **Why repositories instead of using the DbContext in the service?** It keeps the business rules free of EF Core, as Clean Architecture requires. Swapping the database or faking the repository in a test doesn't touch the rules.
- **Can a team member see all their team's tickets?** No, only the ones they're assigned to. Being on the team isn't enough. Only the leader sees the whole team's tickets.
- **What if a new role is added later?** The visibility rule's default case returns no tickets, so a forgotten rule fails safe.

### Future work

Pagination and filters on the list, and fetching the commit SHA from GitHub automatically.

## Workflow and audit log

- **What it does:** ten steps take a ticket from Submitted to Closed: route, reject, assign, submit, leader approve or send back, manager approve or send back, close, reopen. Each step is a `POST /api/tickets/{id}/<step>`. `GET /api/tickets/{id}/history` shows the audit log.
- **One place for the rules:** `TicketWorkflow` is a single table. Each row lists an action, the statuses it's allowed from, the status it moves to, and who may do it. No controller or service decides transitions on its own.
- **Permissions go beyond the role:** only the leader of **this** ticket's team can assign or review, only a member assigned to **this** ticket can submit, only the manager who routed it gives final approval, and only the owning client can close or reopen.
- **Every step works the same way (`RunAsync`):** load the user fresh, load the ticket (404 if hidden), check the rule, make the changes, write the audit row, then save everything in one transaction.
- **Buttons from the same rules:** ticket details include `allowedActions`, computed from the same table, so the React app shows only buttons that will work.
- **Tested end to end:** 64 checks passed, covering the full chain with both send-backs, a reopen, close and reject, plus wrong role (403), wrong status (409), hidden ticket (404) and bad input (400). The test caught a real EF bug (new rows saved as UPDATEs), which is now fixed.

### Likely committee questions

- **What stops someone skipping a step, like approving straight from InProgress?** `TicketWorkflow.Apply` checks the current status against the table. A wrong status gets 409 Conflict and nothing changes.
- **Why is the audit log in the same transaction?** If the status change and its audit row were saved separately, one could succeed without the other. Saving them together means there's never a change without a record.
- **What if two people act on the same ticket at the same moment?** `Ticket.RowVersion` is a concurrency token. The second save fails, and the repository turns that into a 409 asking the user to reload.
- **Why 403 for some refusals and 404 for others?** A user who can't see the ticket gets 404, so its existence stays hidden. A user who can see it but may not do this step gets 403. A step that's wrong for the ticket's current status gets 409.
- **Can the client see the team's internal work?** No. Clients see only the response the manager sent, and a history without internal steps like send-backs.
- **Why doesn't the frontend decide which buttons to show?** The server already knows the rules. Sending `allowedActions` means the rules exist once, on the server, and the UI can never offer a button the API would refuse.

### Future work

A reason text on reject and send-back, removing a member from a ticket, notifications when a ticket changes status, and a WWW-Authenticate header on 401 responses.

## React app: login and ticket list

- **What it does:** a React (Vite) app in `mizan-web/`, styled with Bootstrap. You log in as any demo user and see exactly the tickets the API lets that user see.
- **One API client:** every call goes through `api()` in `src/api/client.js`. It adds the token, shows the API's own error message (the ProblemDetails `title`), and logs the user out on a 401.
- **Proxy instead of CORS:** in development, Vite forwards `/api` to the backend, so the browser only ever talks to one address. In production you'd serve both from one domain or add a CORS policy.
- **The token lives in sessionStorage:** it survives a refresh but is gone when the tab closes, and each tab can be logged in as a different demo user.
- **The frontend hides, the backend decides:** the "New ticket" button only shows for clients, and protected pages redirect to login. That's for convenience. The API still checks every request.

### Likely committee questions

- **Why not CORS?** The Vite proxy makes the browser see one origin, so there's nothing for CORS to allow. It's less configuration and no preflight requests. HTTPS redirection is now on outside Development only, because the proxy talks plain http locally.
- **Isn't sessionStorage open to XSS?** Any script on the page could read it, yes. React escapes everything it renders, and we never insert raw HTML. An httpOnly cookie would be safer and is listed as future work.
- **What happens when the token expires?** The next API call gets a 401, `client.js` clears the session, and the user lands on the login page with "Your session has expired".
- **If A logs out and B logs in on the same browser, where does B land?** On the ticket list, always. "Take me back to the page I wanted" only applies to someone who opened a link while logged out. That was a real bug we found and fixed: B used to land on A's last ticket. Even then the API returned 404 if the ticket wasn't B's, so no data leaked.
- **What if someone deletes the `if (role === 'Client')` check in the browser?** They'd see the button, but `POST /api/tickets` would still refuse them with a 403, because `TicketService` checks the role from the database.

### Future work

An httpOnly cookie instead of sessionStorage, refresh tokens, TypeScript, and paging and filters on the list.

## React app: create form and ticket details

- **What it does:** clients create tickets from a form. Everyone opens a ticket and sees its info, the Before/After code snapshots (linked to the commits on GitHub), the responses, the history, and only the buttons they may use right now. The whole chain, from create to close, including send-backs and reopen, runs in the browser.
- **Buttons come from the server:** the page draws one control per name in `allowedActions`. `workflow/actions.js` only says how each one *looks* (text, colour, URL). Who gets which button is decided by the same rules table that enforces it.
- **Validation lives on the server:** the form only checks "required" and length. The real rules (GitHub URL format, hex SHA) are in `CreateTicketRequest`, and their messages appear under the matching field. There's one set of rules, not two copies that can drift apart.
- **Every action returns the updated ticket:** after a click, the page swaps in the API's answer, so the status, the buttons and the history update without a page reload.
- **Small safety touches:** pasted values are trimmed, Reject and Close ask "are you sure?", buttons are disabled while a request runs, and Back after creating a ticket doesn't reopen an empty form.

### Likely committee questions

- **What if two people click at the same time?** The second save hits the ticket's `RowVersion` check and gets a 409. The page shows the message and asks the user to reload.
- **Why doesn't the client see the team's first attempt that was sent back?** The API only gives clients `Sent` responses, and only the snapshots that belong to them. Drafts and internal steps stay inside the team.
- **Why is Member Two's view a 404 and not a 403?** Member Two isn't assigned, so the ticket isn't visible to them at all. A 404 doesn't even reveal that it exists.
- **Why `window.confirm` and not a nice dialog?** It's the simplest thing that prevents an accidental reject or close. A styled dialog is part of the visual polish.

### Future work

A styled confirm dialog, a reason text when rejecting or sending back, live updates when someone else changes the ticket (SignalR), and comments.

## React app: visual polish

- **What it does:** a navy + gold theme (the gold balance scale is the meaning of "Mizan"), a progress stepper at the top of every ticket, the history as a timeline with an icon per step, icons on every button, and a login page with a branded side panel.
- **Theme without Sass:** Bootstrap 5 reads its colours from CSS variables. `index.css` overrides `--bs-primary` and the button variables, so every existing `btn-primary`, link and badge turned navy without touching the JSX or adding a Sass build step.
- **Works offline:** icons (`bootstrap-icons`) and fonts (`@fontsource/inter`, `@fontsource/cairo` for the Arabic) come from npm and are bundled, not loaded from Google. The demo looks the same with no internet.
- **The stepper is display-only:** it reads `ticket.status` and draws it. Which buttons a user gets still comes from `allowedActions`, computed by `TicketWorkflow` on the server.

### Likely committee questions

- **Doesn't the stepper repeat the workflow rules in the frontend?** No. It only knows the order of the happy path, to draw it. It never decides what's allowed. If the stepper were wrong, the server would still refuse a wrong step with a 409.
- **How does it show the odd cases?** A rejected ticket shows Submitted, then a red "Rejected" step. A reopened ticket goes back to the team, so it sits on the In Progress step, labelled "Reopened" in red.
- **Does it work on a phone?** Yes. The login side panel hides on small screens and the stepper scrolls sideways inside its card. We checked at 375 px wide: the page doesn't scroll sideways.

## Automated tests (xUnit)

- **What it does:** `TicketingSystem.Tests` checks the rules table (`TicketWorkflow`) and the visibility rule (`TicketVisibility`). For every action there are three kinds of test: the right person at the right status succeeds, the wrong person gets Forbidden (403), and the right person at the wrong status gets Conflict (409). Run with `dotnet test` or Visual Studio's Test Explorer. All of them run in under a second.
- **Why these classes:** they hold the core business logic and they're pure: a user and a ticket in, an answer out. No database, no mocks, so the tests are short and never flaky.
- **"Beyond the role" is tested:** the test data has an *Other* user per role (another team's leader, a manager who didn't route the ticket). Those users must be refused, which proves the checks look at *which* person, not just the role.
- **The tests were proven to work:** one rule was broken on purpose (Route allowed from Routed) and two tests failed. Then it was reverted. A second round (final approval allowed for *any* manager) failed three tests.
- **All 10 actions are covered:** 59 tests, including a full-chain test that walks one ticket through 16 steps (both send-backs and a reopen) to Closed.

### Likely committee questions

- **Why not test the controllers and database too?** Those were covered by the end-to-end runs (67 API checks and the full chain in the browser). The next step would be integration tests with `WebApplicationFactory`, which runs the real API in memory.
- **What's the difference between `[Fact]` and `[Theory]`?** A Fact is one test. A Theory is one test run once per `[InlineData]` row, e.g. "Route fails from Routed, InProgress and Closed".
- **How do you test an Expression without a database?** `.Compile()` turns it into a normal function, and you call it on in-memory tickets. It's the same logic EF Core turns into SQL.
