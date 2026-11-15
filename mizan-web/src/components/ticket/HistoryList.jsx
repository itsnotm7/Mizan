import { actionIcon, actionLabel, formatDate, splitWords, statusColor } from '../../utils/labels.js'

// The audit log, oldest first, drawn as a timeline. Clients get a shorter list: the API
// leaves out the team's internal steps (send-backs, approvals between leader and manager).
export default function HistoryList({ entries }) {
  if (!entries) return <p className="text-muted small my-2">Loading history…</p>
  if (entries.length === 0) return <p className="text-muted small my-2">No history yet.</p>

  return (
    <ol className="timeline">
      {entries.map((entry) => (
        <li key={entry.id} className="timeline-item">
          {/* The dot takes the colour of the status the ticket moved to (same colours as the badges).
              Rows whose new value isn't a status (a team or member name) fall back to grey. */}
          <span className={`timeline-icon text-bg-${statusColor(entry.newValue)}`}>
            <i className={`bi ${actionIcon(entry.action)}`} aria-hidden="true"></i>
          </span>
          {/* Date under the label, not beside it: the side column is narrow and labels would wrap */}
          <div className="fw-medium">{actionLabel(entry.action)}</div>
          <div className="text-muted small">{formatDate(entry.createdAt)}</div>
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
    </ol>
  )
}
