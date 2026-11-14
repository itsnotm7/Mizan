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
- **What happens when the token expires?** The next API call gets a 401, `client.js` clears the session, and the user lands on the login page.
- **What if someone deletes the `if (role === 'Client')` check in the browser?** They'd see the button, but `POST /api/tickets` would still refuse them with a 403, because `TicketService` checks the role from the database.

### Future work

An httpOnly cookie instead of sessionStorage, refresh tokens, TypeScript, and paging and filters on the list.
