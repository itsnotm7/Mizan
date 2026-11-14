import { actionLabel, formatDate, splitWords } from '../../utils/labels.js'

// The audit log, oldest first. Clients get a shorter list: the API leaves out the
// team's internal steps (send-backs, approvals between leader and manager).
export default function HistoryList({ entries }) {
  if (!entries) return <p className="text-muted small mb-0">Loading history…</p>
  if (entries.length === 0) return <p className="text-muted small mb-0">No history yet.</p>

  return (
    <ul className="list-group list-group-flush">
      {entries.map((entry) => (
        <li key={entry.id} className="list-group-item px-0">
          <div className="d-flex justify-content-between gap-2">
            <span className="fw-medium">{actionLabel(entry.action)}</span>
            <span className="text-muted small text-nowrap">{formatDate(entry.createdAt)}</span>
          </div>
          <div className="small text-muted">
            by {entry.actorName}
            {(entry.oldValue || entry.newValue) && (
              <>
                {' · '}
                {entry.oldValue && <>{splitWords(entry.oldValue)} → </>}
                <span className="text-body">{entry.newValue && splitWords(entry.newValue)}</span>
              </>
            )}
          </div>
        </li>
      ))}
    </ul>
  )
}
