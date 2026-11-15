# Mizan: Study Guide (Nov 16 → Nov 21)

Your map for the last five days. It doesn't repeat the code; it tells you **what to read, in what order, what to understand from it, and how to check yourself.**

The documents you already have:

| Document | Use it for |
|---|---|
| [CODE_GUIDE.md](CODE_GUIDE.md) | One line per file, no code. The big picture in 15 minutes. |
| [code-walkthrough/](code-walkthrough/README.md) | The real code of every file, line by line, with committee Q&A. Chapter 6 is the **frontend**. |
| [PRESENTATION_NOTES.md](PRESENTATION_NOTES.md) | Talking points and likely questions per feature. |
| **This file** | The React basics you need for chapter 6, the study plan, the non-code topics, and a self-quiz. |

---

## Part 1: React in .NET terms (read this before chapter 6)

You know C# and ASP.NET Core. Almost every React idea in Mizan has a .NET cousin. Learn these 12 and you can read every frontend file.

| React / JS idea | What it means | .NET cousin | Where in Mizan |
|---|---|---|---|
| **Component** | A function that returns what the screen should look like *right now*. React calls it again whenever its data changes. | A Razor view + its code-behind, in one function | Every `.jsx` file |
| **JSX** | HTML-like syntax inside JavaScript. `{ }` drops into JS. `className` instead of `class`. | Razor's `@` syntax | `return ( <div>… )` |
| **Props** | The inputs a parent passes to a child component. Read-only. | Method parameters / a view model | `<ActionsPanel ticket={ticket} busy={busy} … />` |
| **`useState`** | A value the component remembers between redraws. Changing it with the setter triggers a redraw. | A field on the page, plus "re-render when it changes" | `const [ticket, setTicket] = useState(null)` |
| **`useEffect`** | "Run this after drawing", e.g. load data. The function it *returns* is the cleanup, run when the page closes or the inputs change. | `OnInitializedAsync` + `Dispose` | Every page that loads data |
| **`AbortController`** | Cancels a `fetch` in flight. | **`CancellationToken`** (exactly the same idea) | `signal: controller.signal` in every load |
| **`useRef`** | A value that survives redraws but does **not** cause one when it changes. | A private field | `running.current` (stops double-clicks), `currentId.current` |
| **Context + custom hook** | Shares one object with every component below it without passing it by hand. | **Dependency injection** of a scoped service | `AuthProvider` + `useAuth()` |
| **React Router** | Maps URLs to components in the browser; no page reloads. `<Navigate>` redirects, `useParams()` reads `:id`, `<Outlet>` is "render the child page here". | `MapControllerRoute`, `RedirectToAction`, route values, `@RenderBody()` | `App.jsx`, `RequireAuth.jsx`, `Layout.jsx` |
| **`async/await` + `fetch`** | Same as C#. `fetch` is the browser's `HttpClient`. | `HttpClient.SendAsync` | `api()` in `client.js` |
| **npm / `package.json`** | Package manager and project file. `node_modules` is the restored packages. | NuGet / `.csproj` / `bin` + `obj` | `mizan-web/package.json` |
| **Vite** | Dev server (hot reload) and build tool. Its **proxy** forwards `/api` to the backend. | Kestrel + `dotnet watch` + `dotnet publish` | `vite.config.js` |

Four things that are **different** from .NET and trip people up:

1. **A redraw re-runs the whole component function.** Local variables are recreated every time. Anything that must survive goes in `useState` (redraw on change) or `useRef` (no redraw).
2. **State updates aren't instant.** After `setTicket(x)`, the variable `ticket` still holds the old value until the next redraw. That's why `TicketDetailsPage` uses a **ref** (`running.current`) to block a double-click: a ref changes immediately.
3. **StrictMode runs effects twice in development** (mount → cleanup → mount) to expose missing cleanups. That's why you see cancelled (`ERR_ABORTED`) requests in the browser's Network tab in dev. It doesn't happen in production.
4. **Hiding a button is not security.** Anyone can open DevTools and change the JS. Every rule is enforced again by the API. You'll say this sentence a lot.

---

## Part 2: The frontend in 5 ideas

If you remember nothing else, remember these. Each one answers several committee questions.

1. **One door to the API.** Every request goes through `api()` in `client.js`: it adds the token, parses JSON, turns errors into an `ApiError` with the server's message, and logs out on a 401.
2. **Login state is shared, not passed around.** `AuthContext` holds `user`. The token lives in **sessionStorage**, which survives a refresh but not closing the tab. After a refresh, `/api/auth/me` checks that the token is still good.
3. **The server decides the buttons.** Ticket details include `allowedActions`, computed from the same rules table that enforces them. `ActionsPanel` draws one control per name, and `workflow/actions.js` only says how each one *looks*.
4. **Every action returns the updated ticket.** `handleAction` POSTs the step and swaps in the answer, so the status, stepper, buttons and history update without a reload. A 409 or 403 refreshes the ticket and explains why.
5. **Display-only helpers stay display-only.** The stepper, colours, labels and icons read data and draw it. None of them decides anything.

---

## Part 3: Study plan

About 2–3 hours a day. Tick the boxes as you go.

### Mon 16 (tonight, ~1 h): the big picture
- [ ] Read Part 1 and Part 2 of this file.
- [ ] Read the **Frontend** section of [CODE_GUIDE.md](CODE_GUIDE.md).
- [ ] Open the app (`api` + `web`) and click through the full chain once with the demo accounts, while watching the **Network** tab in the browser's DevTools (F12). Every click is one `POST`, and the response is the updated ticket.

### Tue 17 (~3 h): frontend, the plumbing
Read [06-frontend.md](code-walkthrough/06-frontend.md) in this order, and **answer the section's "committee might ask" questions out loud** before reading the answers:
- [ ] `package.json`, `vite.config.js`, `index.html`, `main.jsx` (how the app starts)
- [ ] `App.jsx` (routes)
- [ ] `client.js` (**the most important frontend file**)
- [ ] `AuthContext.jsx`, `RequireAuth.jsx` (login state, the guard, today's logout fix)
- [ ] `Layout.jsx`, `LoginPage.jsx`
- [ ] Self-quiz A below

### Wed 18 (~3 h): frontend, the pages + freeze run
- [ ] `TicketsPage.jsx`, `CreateTicketPage.jsx`, `fieldErrors.js`
- [ ] `TicketDetailsPage.jsx` (**the second most important**: read `handleAction` twice)
- [ ] `ActionsPanel.jsx`, `actions.js`, the three forms, `WorkflowStepper.jsx`, `HistoryList.jsx`, `labels.js`
- [ ] The **"One click, end to end"** section at the end of chapter 6
- [ ] Final run through the UI with every role (the freeze run, with Claude)
- [ ] Self-quiz B below

### Thu 19 (~2 h + demo data/slides): backend refresh + tests
- [ ] [03-workflow.md](code-walkthrough/03-workflow.md) again (the heart: `TicketWorkflow` + `RunAsync`)
- [ ] [07-tests.md](code-walkthrough/07-tests.md), section **7.5** (one test of each kind) and **7.6** (mutation testing)
- [ ] The "Small things the walkthrough noticed" list in the [walkthrough README](code-walkthrough/README.md)
- [ ] Self-quiz C below

### Fri 20: rehearsal
- [ ] Run the demo script (Part 5) twice, timed.
- [ ] Have someone read random questions from [PRESENTATION_NOTES.md](PRESENTATION_NOTES.md) and from the quizzes here. Answer in **one or two sentences**.

---

## Part 4: Beyond the code: what to be ready for

The committee will judge the *decisions*, not just the code. Be able to explain each of these in your own words.

### Architecture
- **Clean Architecture:** four layers, arrows point inward. Domain depends on nothing. Application defines interfaces (`ITicketRepository`), Infrastructure implements them with EF Core, and the API wires it together.
  *Why:* the business rules don't depend on the database or the web framework, so you can test them without either (that's exactly what the 59 unit tests do).
- **Why one rules table (`TicketWorkflow`)?** All status changes and permission checks live in one place, so they can't drift apart, the UI's buttons come from the same table, and the tests cover it directly.
- **Thin controllers:** validate, call a service, return a DTO. **Never return entities.** That hides internal fields and prevents accidental over-sharing (like password hashes).

### Security
- **JWT:** signed, not encrypted. The signature stops tampering, the key is in user-secrets, and tokens expire after 2 hours.
- **Passwords:** salted hashes (`PasswordHasher<User>`), never plain text.
- **401 vs 403 vs 404 vs 409:** who are you? / not allowed / not visible to you (hides that it exists) / not now, because of the status.
- **Defence in depth:** the UI hides buttons, but the API checks every request and reloads the user from the database each time (a deactivated user gets 401 immediately, tested today).
- **Known limits, said honestly:** sessionStorage can be read by an XSS script (mitigated because React escapes output), there are no refresh tokens, and there is no rate limiting on login. They're all on the "future work" list.
- **The Pkcs package:** a security pin that overrides a vulnerable version brought in by the SQL Server driver.

### Data
- **EF Core + migrations:** the schema is code. `Add-Migration` and `Update-Database`.
- **One transaction per action:** status change + audit row + snapshot saved together, or none of them.
- **Concurrency:** `Ticket.RowVersion`. If two people act at the same moment, the second gets 409.
- **Soft delete:** `IsDeleted` instead of removing rows. The audit log has no foreign keys, so history survives.

### Testing
- **What's tested and how:** 59 xUnit unit tests on the rules (no database), 67 end-to-end API checks, and the full chain in the browser.
- **Mutation testing:** we broke a rule on purpose and watched the tests fail. That proves the tests can catch a real bug.
- **What's not tested:** integration tests with `WebApplicationFactory` (future work).

### Scope
- **What you cut and why:** comments UI, attachments, notifications, admin screens, refresh tokens, integration tests. The reason: a working full chain with an audit log mattered more than breadth in the time available. Say it with confidence; cutting scope to protect quality is a skill.

---

## Part 5: Demo script (a draft to rehearse)

About 5 minutes. Use the demo-account buttons on the login page.

1. **Client:** log in, create a ticket (title, description, GitHub repo, commit SHA). Point out the **Before snapshot** and the stepper at step 1.
2. **Teams Manager:** route it to Backend Team. *"Only managers see this button, because the server sends `allowedActions`."*
3. **Team Leader:** assign Member One. The stepper moves to In Progress.
4. **Member One:** submit the fix (branch, SHA, summary). The After snapshot and a Draft response appear.
5. **Team Leader:** **send it back** once. *"The workflow isn't a straight line."* Member One submits again, and the Leader approves.
6. **Teams Manager:** approve and send. The response becomes **Sent**.
7. **Client:** see the response, then **Close**. Show the **history timeline**: every step, who did it, old → new status.
8. Optional: log in as **Member Two** and open the ticket URL. *"404: not assigned, so it doesn't even exist for them."*

Have one backup line ready: *"If the demo fails, here are screenshots of the same flow"* (take them on Thursday).

---

## Part 6: Self-quiz

Answer out loud first, then open the answer.

### A: plumbing (after Tuesday)

<details><summary>1. Why does every page call <code>api()</code> instead of <code>fetch</code> directly?</summary>
One place adds the token, parses JSON, turns errors into <code>ApiError</code> with the server's message, and handles 401 (logout). Without it, every page would repeat that and some would get it wrong.
</details>

<details><summary>2. Where is the token stored, and why there?</summary>
sessionStorage. It survives a refresh and disappears when the tab closes, and each tab can be a different demo user. The tradeoff: a malicious script could read it. An httpOnly cookie is the safer future option.
</details>

<details><summary>3. What does the Vite proxy do, and why don't we need CORS?</summary>
It forwards <code>/api/...</code> from port 5173 to the API on 5199. The browser only ever talks to one origin, so there's no cross-origin request for CORS to allow.
</details>

<details><summary>4. After a page refresh, how does the app know who is logged in?</summary>
The token is still in sessionStorage. <code>AuthContext</code> calls <code>/api/auth/me</code>, which reads the user from the database. While it waits, <code>checking</code> is true, so the guard shows "Loading…" instead of flashing the login page.
</details>

<details><summary>5. User A logs out, user B logs in. Where does B land, and why was that a bug before?</summary>
On the ticket list. Before the fix, the guard mistook the logout moment for a stranger opening A's ticket link and remembered it, so B landed on A's ticket. Now <code>signedOut</code> tells the guard not to remember anything after a logout. Even before, no data leaked: the API returned 404.
</details>

<details><summary>6. What is <code>AbortController</code> for?</summary>
It cancels a request when the page closes or the ticket changes, so a late answer can't update a page that's gone or show ticket A's data on ticket B. It's the frontend's <code>CancellationToken</code>.
</details>

### B: pages (after Wednesday)

<details><summary>1. How does the details page decide which buttons to show?</summary>
It doesn't. The server sends <code>allowedActions</code>, computed from <code>TicketWorkflow</code>. <code>ActionsPanel</code> draws one control per name, and <code>actions.js</code> only provides label, colour, icon, URL and whether it needs a form or a confirm.
</details>

<details><summary>2. Why does <code>handleAction</code> use <code>running.current</code> (a ref) and not just the <code>busy</code> state?</summary>
State updates only apply on the next redraw, so a fast double-click could send two requests before the button looks disabled. A ref changes immediately, so the second click is blocked.
</details>

<details><summary>3. What happens if two people act on the same ticket at once?</summary>
The second save fails the <code>RowVersion</code> check, and the API returns 409. The page shows the message, reloads the ticket and history, and redraws the buttons for the new status.
</details>

<details><summary>4. Where are the validation rules for a new ticket?</summary>
On the server (<code>CreateTicketRequest</code>). The form only checks required fields and lengths. A 400 response lists errors per field, and <code>fieldErrors.js</code> puts each message under its input. One set of rules, so the two copies can't drift apart.
</details>

<details><summary>5. Can the stepper break the workflow?</summary>
No. It only reads <code>ticket.status</code> and draws it. The buttons come from <code>allowedActions</code>, and the real enforcement is <code>TicketWorkflow.Apply</code> on every POST (403/409).
</details>

<details><summary>6. Why is a reopened ticket shown on step 3?</summary>
Reopen sends the ticket back to the team, and the next move is the leader assigning work again (Reopened → InProgress). The red "Reopened" label shows it came back from the client.
</details>

### C: backend + tests (after Thursday)

<details><summary>1. Walk through what happens on <code>POST /api/tickets/{id}/leader-approve</code>.</summary>
The controller calls the workflow service. <code>RunAsync</code> loads the user fresh from the database (401 if inactive), loads the ticket (404 if not visible), and calls <code>TicketWorkflow.Apply</code>: wrong person → 403, wrong status → 409, otherwise the status changes. Then it writes the audit row and saves everything in one transaction. The response is the updated ticket details with new <code>allowedActions</code>.
</details>

<details><summary>2. Why is "who" checked before "status"?</summary>
"You may never do this" (403) is the permanent answer. "Not now" (409) would suggest trying again later, which is misleading for someone who will never be allowed.
</details>

<details><summary>3. Why test with <code>OtherManager</code> instead of the Client?</summary>
The Client is refused by the role check alone, so that test would still pass if the "manager who routed it" check were deleted. <code>OtherManager</code> has the right role, so only the deeper check refuses them. That's the test that caught the mutation.
</details>

<details><summary>4. What does <code>Apply</code> change, and what does it leave to the service?</summary>
Only <code>ticket.Status</code> (and it returns the old status for the audit log). Setting the team and manager, creating assignments, snapshots and responses, and writing the audit row are <code>TicketWorkflowService</code>'s job.
</details>

<details><summary>5. Why does the audit log have no foreign keys?</summary>
History must survive even if the ticket or user it mentions is removed later. With a foreign key you either couldn't remove the row (Restrict) or you'd lose the history (Cascade).
</details>

---

When you finish a part, tell Claude "quiz me on part X" and you'll get fresh questions, not these ones.
