import { useEffect, useState } from 'react'
import { api, isAbort } from '../../api/client.js'

// Leader picks members of their team. People already on the ticket stay on it
// (assignments are never removed), so they show ticked and can't be unticked.
export default function AssignForm({ ticket, busy, onRun }) {
  // undefined = still loading, null = the ticket's team isn't in the list
  const [team, setTeam] = useState(undefined)
  const [selected, setSelected] = useState([])
  const [error, setError] = useState(null)

  const assignedIds = new Set(ticket.assignees.map((a) => a.id))
  const isReopened = ticket.status === 'Reopened'

  useEffect(() => {
    const controller = new AbortController()
    // A leader only gets their own team back from this endpoint
    api('/teams', { signal: controller.signal })
      .then((list) => setTeam(list.find((t) => t.id === ticket.teamId) ?? null))
      .catch((err) => {
        if (!isAbort(err)) setError(err.message)
      })
    return () => controller.abort()
  }, [ticket.teamId])

  function toggle(id) {
    setSelected((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]))
  }

  if (error) return <div className="text-danger small">{error}</div>
  if (team === undefined) return <div className="text-muted small">Loading team…</div>
  if (team === null) return <div className="text-danger small">The team for this ticket wasn't found.</div>

  // On a reopened ticket the leader may carry on with the people already assigned,
  // as long as one of them is still an active member (the same rule the server checks)
  const canContinue = isReopened && team.members.some((m) => assignedIds.has(m.id))
  const canSubmit = selected.length > 0 || canContinue

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        onRun({ memberIds: selected })
      }}
    >
      {team.members.length === 0 && <div className="text-muted small mb-2">This team has no active members.</div>}
      {team.members.map((member) => {
        const already = assignedIds.has(member.id)
        return (
          <div className="form-check" key={member.id}>
            <input
              id={`member-${member.id}`}
              type="checkbox"
              className="form-check-input"
              checked={already || selected.includes(member.id)}
              disabled={already}
              onChange={() => toggle(member.id)}
            />
            <label htmlFor={`member-${member.id}`} className="form-check-label">
              {member.name} {already && <span className="text-muted small">(already assigned)</span>}
            </label>
          </div>
        )
      })}
      <button type="submit" className="btn btn-primary mt-2" disabled={busy || !canSubmit}>
        {selected.length === 0 && isReopened ? 'Continue with current team' : 'Assign'}
      </button>
    </form>
  )
}
