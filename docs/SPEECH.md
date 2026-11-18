# Mizan: presentation speech

About **13 minutes**: 8 minutes of talking and 5 minutes of demo. Then questions.

**How to use it.** Every part has two cues:
- 🖥️ **Slides:** the slide to show.
- 💻 **No slides:** what to open instead (Visual Studio, the browser). The words stay the same.

Don't learn it word for word. Learn the **bold line** of each part. The rest is there so you know where you're going. Speak slowly. Pausing is fine.

**Before you start:** API and web app running (`.claude/launch.json` or Visual Studio + `npm run dev`). Run `scripts\demo-data.ps1` the same morning. Open the browser on the login page. In Visual Studio, have `TicketWorkflow.cs` and `TicketWorkflowService.cs` open in tabs.

---

## 1. Opening (1 min)

🖥️ Slides 1 → 3 · 💻 the Mizan login page in the browser

> Good morning. My name is Mahmoud Abdelrahman, and this is my CoderZ final project: **Mizan**.
>
> Mizan, ميزان, means "balance" or "scale" in Arabic. **The idea is simple: every code change is weighed and approved before it reaches the client.**
>
> Here is the problem. In many teams, a client asks for a fix in a chat message or an email. A developer changes the code and sends it back. Nobody reviews it, and the client is the first person to test it. And later, when something breaks, nobody can say who changed what, or from which commit.
>
> Mizan puts that whole process in one system. It makes three promises. One: every fix is reviewed twice, by the team leader and then by the manager, before the client sees it. Two: we save the commit the ticket started from, and the commit of every fix, so you can compare before and after. Three: every single step is written to an audit log: who did it, when, and what changed.

---

## 2. How it works (1.5 min)

🖥️ Slides 4 → 5 · 💻 stay on the login page and point at the five demo-account buttons

> There are four roles. The **client** opens a ticket with a GitHub repository and a commit. The **teams manager** sends it to a team, or rejects it. The **team leader** assigns people from their own team. And the **team members** do the fix.
>
> **A ticket moves through seven statuses: Submitted, Routed, In Progress, Leader Review, Manager Review, Response Sent, and Closed.**
>
> But it's not a straight line. The leader or the manager can send a fix back for more work. The manager can reject a ticket at the start, if it's not a real code fix. And at the end, the client can reopen it if they're not happy.
>
> Each role only sees its own tickets, and only the buttons it's allowed to press.

---

## 3. How it's built (3 min)

### Stack and architecture

🖥️ Slides 6 → 7 · 💻 Visual Studio, Solution Explorer: expand the four projects

> The backend is an ASP.NET Core Web API on .NET 10, with Entity Framework Core and SQL Server. Login uses JWT tokens. The frontend is React with Vite and Bootstrap. The tests use xUnit.
>
> I used **Clean Architecture**, so there are four projects. **The rule is: the arrows only point inward.**
>
> In the middle is the **Domain**: the entities and enums. It depends on nothing. Around it is **Application**: the business rules and the interfaces, like `ITicketRepository`. **Infrastructure** implements those interfaces with EF Core. And the **API** has the controllers and wires everything together.
>
> Why does this matter? Because my business rules don't know anything about the database or the web. That's what made it possible to test them without a database, which I'll show in a minute.

### The rules table (the most important part)

🖥️ Slide 8 · 💻 open `TicketWorkflow.cs`, show lines 13–25

> This is the heart of the project. **All the workflow rules live in one table, in one class: `TicketWorkflow`.**
>
> Each line is one action. For example, *Route*: it's allowed from Submitted, it goes to Routed, and only a teams manager can do it.
>
> And the checks go beyond the role. It's not "any team leader can assign". It's **the leader of this ticket's team**. Not "any member can submit". It's **a member assigned to this ticket**. And the final approval can only come from **the manager who routed it**.
>
> No controller decides a status change by itself. If I want to change a rule, I change one line here.
>
> One more thing: the frontend doesn't decide which buttons to show. The server sends a list called `allowedActions`, built from this same table. So the app can never show a button that the API would refuse.

### One request, step by step

🖥️ Slide 9 · 💻 open `TicketWorkflowService.cs`, go to `RunAsync` (line 134)

> All ten actions go through the same method, `RunAsync`, in the same order.
>
> First, it loads the user **from the database**, not just from the token. So if an account was deactivated five minutes ago, it's refused right away.
> Second, it loads the ticket. If you're not allowed to see it, you get a 404, as if it doesn't exist.
> Third, it checks the rules table: wrong person is 403, wrong status is 409.
> Then it makes the change, writes the audit row, and **saves everything in one transaction**. So there is never a status change without its audit record.

---

## 4. Security and testing (1.5 min)

🖥️ Slides 10 → 11 · 💻 Visual Studio Test Explorer: press **Run All** while you talk (it takes under a second)

> On security: the user interface hides buttons, but that's only for comfort. **The server checks every request.**
>
> The status codes each mean something different. 401: we don't know who you are. 403: we know you, but you're not allowed. 404: you can't see this ticket, and we don't even confirm it exists. 409: the action is OK, but not in the ticket's current status.
>
> Passwords are stored as salted hashes, never plain text. The JWT signing key is in user-secrets, not in the code. And every endpoint requires a login by default.
>
> For testing: **59 unit tests** on the rules table and the visibility rule. They need no database, and they run in under a second. *(point at Test Explorer: all green)* For each action, I test three things: the right person succeeds, the wrong person gets 403, and the wrong status gets 409.
>
> And to prove the tests actually work, I broke a rule on purpose, twice. Both times, the tests failed. Then I put the rule back.

---

## 5. Live demo (5 min)

🖥️ Slide 12, then switch to the browser · 💻 the browser

> Now let me show you the full chain live.

Use the demo-account buttons. The password is the seeded one.

| Who | What you do | What you say |
|---|---|---|
| **Client** | New ticket: title, description, repo link, commit SHA. Submit. | "The ticket is created, and the **Before snapshot** is saved. The stepper shows step one." |
| **Teams Manager** | Open it, pick Backend Team, **Route**. | "Only the manager sees this button. It comes from `allowedActions`." |
| **Team Leader** | Tick Member One, **Assign**. | "Now it's In Progress." |
| **Member One** | Branch, commit SHA, summary. **Submit fix**. | "Here is the **After snapshot**, and a **Draft** response. The client can't see this draft." |
| **Team Leader** | **Send back**. | "The workflow isn't a straight line. The leader isn't happy, so it goes back." |
| **Member One** | Submit again (a new SHA). | "A second try. Both commits stay in the history." |
| **Team Leader** | **Approve**. | |
| **Teams Manager** | **Approve and send to client**. | "Now the response is **Sent**." |
| **Client** | Read the response. **Accept and close**. | "And here is the **history**: every step, who did it, and the status before and after." |

**Optional, if you have time:** log in as **Member Two** and paste the ticket's URL.
> "Member Two isn't assigned to this ticket, so they get *Ticket not found*. For them it doesn't exist."

**If you're short on time:** don't create a new ticket. Use the ready-made ones from the demo data:
- "Password reset email never arrives" is Submitted (the manager routes it).
- "Orders API returns 500…" is Routed (the leader assigns it).
- "Search returns duplicate products" is Response Sent (the client closes it).

**Don't click the GitHub links** in the demo tickets: the repos are made up.

---

## 6. Closing (1 min)

🖥️ Slides 13 → 14 · 💻 back to the browser, or just look at the committee

> To finish: I chose to build **one complete chain**, with an audit log and tests, instead of many half-finished features.
>
> Some things I cut on purpose, and they're the natural next steps: comments and attachments, email notifications, admin screens for users and teams, refresh tokens, and integration tests that run the real API in memory.
>
> **Mizan makes sure every code change is weighed before it ships.** Thank you. I'm happy to take your questions.

---

## If something goes wrong

- **The API isn't answering** (login says "Cannot reach the server"): "One moment, the server is starting." Start the API again and wait for Swagger to load.
- **The demo can't run at all:** "Here are screenshots of the same flow, taken from the real app." Open `docs/demo-screenshots/` and go through them in order (the README there says what each one shows).
- **A button gives an error:** read the message out loud. It's probably a 409 because the status is wrong, which shows the rules working: "See, the server refuses it, because the ticket isn't in that status."
- **You lose your place:** go back to the **bold line** of the part you're in.
- **A question you don't know:** "I didn't build that part yet. Here's how I would approach it…" It's fine to say what's future work.
