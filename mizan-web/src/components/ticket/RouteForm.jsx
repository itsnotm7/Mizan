import { useEffect, useState } from 'react'
import { api, isAbort } from '../../api/client.js'

// Manager picks the team that will work on the ticket
export default function RouteForm({ busy, onRun }) {
  const [teams, setTeams] = useState(null)
  const [teamId, setTeamId] = useState('')
  const [error, setError] = useState(null)

  useEffect(() => {
    const controller = new AbortController()
    api('/teams', { signal: controller.signal })
      .then((list) => {
        setTeams(list)
        if (list.length > 0) setTeamId(list[0].id)
      })
      .catch((err) => {
        if (!isAbort(err)) setError(err.message)
      })
    return () => controller.abort()
  }, [])

  if (error) return <div className="text-danger small">{error}</div>
  if (!teams) return <div className="text-muted small">Loading teams…</div>
  if (teams.length === 0) return <div className="text-muted small">There are no teams to route to.</div>

  return (
    <form
      className="d-flex flex-column align-items-start gap-2"
      onSubmit={(e) => {
        e.preventDefault()
        onRun({ teamId })
      }}
    >
      <select className="form-select" value={teamId} onChange={(e) => setTeamId(e.target.value)} aria-label="Team">
        {teams.map((team) => (
          <option key={team.id} value={team.id}>
            {team.name} — {team.leader.name}
          </option>
        ))}
      </select>
      <button type="submit" className="btn btn-primary text-nowrap" disabled={busy}>Route</button>
    </form>
  )
}
