# Session Notes: Nov 8, 2026

## Done today
- **Tickets finished and committed** (`6e03719`). 31 test scenarios pass.
- **Workflow and audit logging built** while Mahmoud was at dinner. 64 end-to-end checks pass. **Not committed yet:** we walk through the core logic together first.

## Walkthrough for Mahmoud (before committing)
Mahmoud owns the workflow rules and permission checks, and he chose to walk through them line by line. They were written while he was away so we'd stay ahead, so this walkthrough replaces the usual "explain before writing" step. Go in this order, and answer one question per file before moving on.

1. **`Application/Services/TicketWorkflow.cs`: the rules table, the heart of Mizan.**
   - Each line is one arrow from CLAUDE.md: action, from-status, to-status, who.
   - `Apply` checks **who** first (403), then **status** (409), then changes the status.
   - `AllowedActions` uses the same table to tell the frontend which buttons to show.
   - The `Is...` checks go beyond the role: the leader of **this** ticket's team, a member assigned to **this** ticket, the manager who routed **this** ticket, the client who owns **this** ticket.
   - *Question:* the leader of another team calls `leader-approve` on our ticket. Which response do they get, and why? (404: they can't see it, so they never reach the rules.)
2. **`Application/Services/TicketWorkflowService.cs`: `RunAsync`, the 6 steps every action follows.**
   - Load the user (fresh from the database).
   - Load the ticket (404 if not visible).
   - `TicketWorkflow.Apply`.
   - Make the step-specific changes.
   - Write the audit row.
   - Save once (all or nothing).
   - *Question:* if writing the audit row failed, would the status change still be saved? (No, it's one `SaveChanges`, so one transaction.)
3. **The step-specific parts of `TicketWorkflowService.cs`:**
   - **Route:** sets the team and records which manager routed it.
   - **Assign:** only active members of that team. An already assigned member is skipped, not duplicated. At least one member is required.
   - **Submit:** adds the After snapshot and a Draft response.
   - **Send back:** discards the draft.
   - **Manager approve:** the Draft becomes Sent, with `ApprovedById` and `SentAt` set.
   - **Close:** sets `ClosedAt`.
   - *Question:* why can the client see the response only after manager approval?
4. **`Application/Services/ActingUserExtensions.cs`:** piece 1 from yesterday, moved here so all three services share it.

## Endpoints added
| Method | Route | Who |
|---|---|---|
| POST | `/api/tickets/{id}/route` `{ teamId }` | TeamsManager, while Submitted |
| POST | `/api/tickets/{id}/reject` | TeamsManager, while Submitted |
| POST | `/api/tickets/{id}/assign` `{ memberIds }` | Leader of the ticket's team, while Routed or Reopened |
| POST | `/api/tickets/{id}/submit` `{ branch, commitSha, summary }` | Assigned member, while InProgress |
| POST | `/api/tickets/{id}/leader-approve` and `/leader-send-back` | Leader of the team, while LeaderReview |
| POST | `/api/tickets/{id}/manager-approve` and `/manager-send-back` | The manager who routed it, while ManagerReview |
| POST | `/api/tickets/{id}/close` and `/reopen` | The client who owns it, while ResponseSent |
| GET | `/api/tickets/{id}/history` | Anyone who can see the ticket (clients get a filtered list) |
| GET | `/api/teams` | Manager and Admin (all teams), Leader (own team) |

Ticket details now also include `responses` (clients see only Sent ones) and `allowedActions`.

## Decisions Claude made while Mahmoud was away (all easy to change)
- **Re-assigning:** assignments are never removed. Assigning a member who's already on the ticket is skipped, so the unique index is never hit and no migration is needed. This answers yesterday's open question.
- **No reason text on reject or send-back:** MVP, listed as future work.
- **Send-back hides the draft:** it's soft-deleted, so the next submit starts a fresh one. The audit log still records the send-back.
- **Manager approval sends the response directly:** Draft becomes Sent, and the `Approved` status isn't used, as CLAUDE.md describes.
- **Only the manager who routed the ticket** can do the final review. Admin has no workflow powers.
- **Client history:** clients see Created, Route, Reject, ManagerApprove, Close and Reopen, not the internal back-and-forth.
- **Audit rows:** one per status change (old → new status), plus `Created`, `TeamAssigned` (team name) and `MemberAssigned` (member name).

## Bug found by the tests (and fixed)
- **What happened:** new assignments, snapshots and responses were added through the ticket's collections (`ticket.Assignments.Add(...)`).
- **Why it failed:** our ids are set in C# (`Guid.NewGuid()`), so EF thought the rows already existed and sent an UPDATE. That updated nothing, and EF reported a concurrency conflict (409).
- **The fix:** new rows are added explicitly through the repository (`AddAssignment`, `AddSnapshot`, `AddResponse`).
- **Good committee story:** the end-to-end test caught this.

## Code review (done, fixes in)
- **Fixed, a leak:** clients could see the commit SHAs of fixes still in review or sent back. They now see the Before snapshot and only the fixes that were sent to them. Submit gives the snapshot and the response the same timestamp, which is how they're matched up.
- **Fixed:** routing to a team without an active leader now gets 400, because nobody could assign members and the ticket would be stuck.
- **Fixed:** continuing a reopened ticket needs at least one assignee who is still active and on the team.
- **Not done on purpose:** the reviewer suggested turning every database error into a 409. That would hide real bugs behind a "someone else saved" message. The only case it covers is two leaders assigning the same member in the same millisecond.

After the fixes, 67 workflow checks and the 31 ticket checks pass.

## Walkthrough done (Nov 14–15), backend frozen
The presentation moved to Sat Nov 21. Mahmoud walked through all four files above. Rehearse these points, which took a second pass:
- **Both checks in a rule must pass:** right person **and** right status. A leader assigning a Closed ticket fails the status check → 409.
- **Who before status:** a client pressing `assign` gets 403, not 409. "Never you" is permanent; "not now" would suggest trying again later.
- **Where drafts are hidden:** ManagerApprove *sets* Sent; `TicketService` (details mapping) *filters* so clients only see Sent responses and their fixes.
- **Async ≠ faster:** async frees the thread so the server handles more requests at once; the per-request user lookup is cheap because it's a ~1 ms primary-key lookup, and it makes deactivation instant.

Next: React, starting Sun 15.
