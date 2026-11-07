# Session Notes: Nov 7, 2026

## Where we stopped
Mon 9 (Tickets) is **in progress**. The boilerplate is written, and we're walking through the 3 core pieces together.

| Piece | File | Status |
|---|---|---|
| 1. Active user check | `Services/TicketService.cs` (`GetActingUserAsync`) | Done and in the code |
| 2. Who sees which ticket | `Services/TicketVisibility.cs` | **Next.** Explained, waiting on Mahmoud's answer below |
| 3. Only a client can create | `Services/TicketService.cs` (`CreateAsync`) | Not started |

**The ticket endpoints don't work yet.** Nothing has been tested. Don't demo them until all 3 pieces are in.

## Pick up here: the pending question
> Member Two isn't assigned to a ticket, but gets its ID and opens `GET /api/tickets/{id}` directly. What response do they get, and why that response and not 403?

After Mahmoud answers, add this code to `TicketVisibility.cs` (it also needs `using TicketingSystem.Domain.Enums;`):

```csharp
public static Expression<Func<Ticket, bool>> VisibleTo(User user)
{
    var userId = user.Id;

    return user.Role switch
    {
        UserRole.Client => t => t.ClientId == userId,
        UserRole.TeamsManager or UserRole.Admin => t => true,
        UserRole.TeamLeader => t => t.Team != null && t.Team.LeaderId == userId,
        UserRole.TeamMember => t => t.Assignments.Any(a => a.UserId == userId && !a.IsDeleted),
        _ => t => false
    };
}
```

This code hasn't been compiled yet.

## Remaining steps for Mon 9
1. Finish piece 2, then walk through and add piece 3.
2. Build with 0 errors and 0 warnings.
3. Run the API and test the scenarios listed below.
4. Run the `code-reviewer` agent and fix what it finds.
5. Add Tickets to `docs/PRESENTATION_NOTES.md`, and update `CLAUDE.md` and `docs/CODE_GUIDE.md`.
6. Commit, then ask Mahmoud one quick question about the feature.

### Test scenarios
1. Client creates a ticket: 201, status `Submitted`, one `Before` snapshot.
2. Non-client creates a ticket: 403.
3. Bad URL, bad SHA or priority `99`: 400.
4. List as each role: client and manager see the ticket; leader and members don't until it's routed and assigned. To test those two rules, temporarily set `TeamId` and add an assignment in LocalDB, then remove them.
5. Details as a user who can't see the ticket: 404. Unknown ID: 404. No token: 401.
6. Log in, set `IsActive = 0` in LocalDB, call `/api/tickets`: 401, then restore the user. This also tests the `/me` open item in `CLAUDE.md`.
7. Log in as a client, change their role in LocalDB, try to create a ticket: 403, then restore the role.

## Decisions made
- **Data access:** Application defines `ITicketRepository` and `IUserRepository`, and Infrastructure implements them. Application stays free of EF Core.
- **Stale tokens:** the token only supplies the user's id. The active status and role come from the database on every call. No `[Authorize(Roles=...)]` on ticket endpoints, because it reads the token's role.
- **Visibility:** one rule for both list and details. Client sees their own, TeamsManager and Admin see all, TeamLeader sees their teams' tickets, TeamMember sees tickets they're assigned to.
- **Hidden tickets:** return 404, not 403, so nobody can find out which ticket IDs exist.
- **Errors:** three small exceptions in Application, mapped to 401, 403 and 404 in `GlobalExceptionHandler`.
- **Before snapshot:** the client types the branch and commit SHA. Fetching them from GitHub is future work.
- **Core logic:** Mahmoud chose to walk through it together rather than write it himself.
- **Left out for now:** pagination and filters, `DueDate` on create, responses in the details DTO, and the audit row for ticket creation (Tuesday).

## What we discussed

**Pending auth question.** "If a client changes `role` to `Admin` in their token, why does the API reject it?"
- Mahmoud's first answer was that the API compares the token's info with the admin's info. That's not how it works: apart from `/me`, the API doesn't check the database for this.
- The real answer is the signature. It's made from the token's contents and a secret key only the server has. Any change makes the signature stop matching, so the API returns 401.
- Follow-up: "What if the attacker calculates a new signature?" Mahmoud answered that the server didn't produce it, so it's rejected. Correct, because the attacker doesn't have the key.

**Piece 1 question.** "What happens if a deactivated user calls the API without this check?"
- Mahmoud answered that the account is deactivated but the token isn't, and the token lasts the whole session. Correct: JwtBearer only checks the token and never looks at the database, so they'd still get in for up to 2 hours.

## Saved
- `docs/CODE_GUIDE.md`: everything in the code and what it does.
- This file.
- A local git commit on `main`. **Not pushed.** There's still no off-machine backup, because the GitHub repo hasn't been set up yet.
