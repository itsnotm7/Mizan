import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, isAbort } from '../api/client.js'
import ActionsPanel from '../components/ticket/ActionsPanel.jsx'
import HistoryList from '../components/ticket/HistoryList.jsx'
import { workflowActions } from '../workflow/actions.js'
import { summarizeError } from '../utils/fieldErrors.js'
import { formatDate, priorityColor, splitWords, statusColor } from '../utils/labels.js'

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// The fields the action forms show; any other error key goes in the alert above them
const actionFormFields = ['TeamId', 'MemberIds', 'Branch', 'CommitSha', 'Summary']

// "https://github.com/owner/repo/" + sha → a link to that exact commit on GitHub
const commitUrl = (repo, sha) => `${repo.replace(/\/$/, '')}/commit/${sha}`

// The server already only accepts https://github.com/... URLs. Checking again before
// making it a link means a bad value could never become a clickable script link.
const isSafeLink = (url) => url.startsWith('https://')

export default function TicketDetailsPage() {
  const { id } = useParams()
  const validId = guidPattern.test(id)

  const [ticket, setTicket] = useState(null)
  const [history, setHistory] = useState(null)
  const [historyError, setHistoryError] = useState(null)
  const [loadError, setLoadError] = useState(null)

  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState(null)
  const [fieldErrors, setFieldErrors] = useState({})

  // Which ticket this page shows right now. An answer for an older id is ignored,
  // so a slow response can never put ticket A's data on ticket B's page.
  const currentId = useRef(id)
  // Cancels this page's loads when the ticket changes or the page closes
  const pageLoads = useRef(null)
  // Blocks a second click before React has redrawn the buttons as disabled
  const running = useRef(false)

  const loadTicket = useCallback((ticketId, signal) =>
    api(`/tickets/${ticketId}`, { signal })
      .then((data) => {
        if (currentId.current === ticketId) setTicket(data)
      })
      .catch((err) => {
        if (!isAbort(err) && currentId.current === ticketId) setLoadError(err)
      }), [])

  const loadHistory = useCallback((ticketId, signal) =>
    api(`/tickets/${ticketId}/history`, { signal })
      .then((data) => {
        if (currentId.current === ticketId) {
          setHistory(data)
          setHistoryError(null)
        }
      })
      .catch((err) => {
        if (!isAbort(err) && currentId.current === ticketId) setHistoryError(err.message)
      }), [])

  useEffect(() => {
    currentId.current = id
    setTicket(null)
    setHistory(null)
    setHistoryError(null)
    setLoadError(null)
    setActionError(null)
    setFieldErrors({})
    if (!validId) return

    const controller = new AbortController()
    pageLoads.current = controller
    loadTicket(id, controller.signal)
    loadHistory(id, controller.signal)
    return () => controller.abort()
  }, [id, validId, loadTicket, loadHistory])

  // Every button ends up here: POST the step, and the API answers with the updated ticket.
  // Swapping it in redraws the buttons from the new allowedActions.
  async function handleAction(name, body) {
    if (running.current) return
    running.current = true
    const ticketId = id

    setBusy(true)
    setActionError(null)
    setFieldErrors({})
    try {
      const updated = await api(`/tickets/${ticketId}/${workflowActions[name].path}`, { method: 'POST', body })
      if (currentId.current !== ticketId) return
      setTicket(updated)
      loadHistory(ticketId, pageLoads.current?.signal)
    } catch (err) {
      if (currentId.current !== ticketId) return
      setFieldErrors(err.fieldErrors ?? {})
      if (err.status === 409 || err.status === 403) {
        // Someone else moved the ticket first: show the latest version instead of stale buttons
        setActionError(`${err.message} The ticket has been refreshed to its latest version.`)
        loadTicket(ticketId, pageLoads.current?.signal)
        loadHistory(ticketId, pageLoads.current?.signal)
      } else {
        setActionError(summarizeError(err, actionFormFields))
      }
    } finally {
      running.current = false
      setBusy(false)
    }
  }

  if (!validId || loadError) {
    return (
      <div className="card card-body text-center">
        {/* 404 also covers tickets this user isn't allowed to see: the API doesn't say which */}
        <p className="mb-2">{!validId || loadError.status === 404 ? 'Ticket not found.' : loadError.message}</p>
        <Link to="/tickets">Back to tickets</Link>
      </div>
    )
  }

  if (!ticket) return <p className="text-muted">Loading ticket…</p>

  return (
    <>
      <Link to="/tickets" className="small text-decoration-none">← Back to tickets</Link>

      <div className="d-flex flex-wrap align-items-center gap-2 mt-2 mb-3">
        <h1 className="h4 mb-0 me-2">{ticket.title}</h1>
        <span className={`badge text-bg-${statusColor(ticket.status)}`}>{splitWords(ticket.status)}</span>
        <span className={`badge text-bg-${priorityColor(ticket.priority)}`}>{ticket.priority}</span>
      </div>

      <div className="row g-3">
        <div className="col-lg-8 d-flex flex-column gap-3">
          <div className="card shadow-sm">
            <div className="card-body">
              <p style={{ whiteSpace: 'pre-wrap' }}>{ticket.description}</p>
              <dl className="row small mb-0">
                <dt className="col-sm-3">Repository</dt>
                <dd className="col-sm-9">
                  {isSafeLink(ticket.repositoryUrl)
                    ? <a href={ticket.repositoryUrl} target="_blank" rel="noreferrer">{ticket.repositoryUrl}</a>
                    : ticket.repositoryUrl}
                </dd>
                <dt className="col-sm-3">Client</dt>
                <dd className="col-sm-9">{ticket.client.name}</dd>
                <dt className="col-sm-3">Manager</dt>
                <dd className="col-sm-9">{ticket.manager?.name ?? <span className="text-muted">Not routed yet</span>}</dd>
                <dt className="col-sm-3">Team</dt>
                <dd className="col-sm-9">{ticket.teamName ?? <span className="text-muted">Not routed yet</span>}</dd>
                <dt className="col-sm-3">Assigned</dt>
                <dd className="col-sm-9">
                  {ticket.assignees.length > 0
                    ? ticket.assignees.map((a) => a.name).join(', ')
                    : <span className="text-muted">Nobody yet</span>}
                </dd>
                <dt className="col-sm-3">Created</dt>
                <dd className="col-sm-9">{formatDate(ticket.createdAt)}</dd>
                {ticket.closedAt && (
                  <>
                    <dt className="col-sm-3">Closed</dt>
                    <dd className="col-sm-9">{formatDate(ticket.closedAt)}</dd>
                  </>
                )}
              </dl>
            </div>
          </div>

          <div className="card shadow-sm">
            <div className="card-header bg-white fw-medium">Code snapshots</div>
            <ul className="list-group list-group-flush">
              {ticket.snapshots.map((s) => (
                <li key={s.id} className="list-group-item d-flex flex-wrap gap-2 align-items-center">
                  <span className={`badge text-bg-${s.kind === 'Before' ? 'secondary' : 'success'}`}>{s.kind}</span>
                  <span className="small">{s.branch}</span>
                  {isSafeLink(s.repositoryUrl)
                    ? (
                      <a className="font-monospace small" href={commitUrl(s.repositoryUrl, s.commitSha)}
                        target="_blank" rel="noreferrer">
                        {s.commitSha.slice(0, 7)}
                      </a>
                    )
                    : <span className="font-monospace small">{s.commitSha.slice(0, 7)}</span>}
                  <span className="text-muted small ms-auto">{formatDate(s.createdAt)}</span>
                </li>
              ))}
            </ul>
          </div>

          <div className="card shadow-sm">
            <div className="card-header bg-white fw-medium">Responses</div>
            <div className="card-body">
              {ticket.responses.length === 0 && <p className="text-muted small mb-0">No response yet.</p>}
              {ticket.responses.map((r) => (
                <div key={r.id} className="border-start border-3 ps-3 mb-3">
                  <div className="d-flex flex-wrap gap-2 align-items-center mb-1">
                    <span className={`badge text-bg-${r.status === 'Sent' ? 'success' : 'warning'}`}>{r.status}</span>
                    <span className="small">by {r.author.name}</span>
                    <span className="text-muted small ms-auto">{formatDate(r.sentAt ?? r.createdAt)}</span>
                  </div>
                  <p className="mb-0" style={{ whiteSpace: 'pre-wrap' }}>{r.summary}</p>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="col-lg-4 d-flex flex-column gap-3">
          <div className="card shadow-sm border-primary">
            <div className="card-header bg-white fw-medium">Your actions</div>
            <div className="card-body">
              <ActionsPanel ticket={ticket} busy={busy} error={actionError} fieldErrors={fieldErrors}
                onAction={handleAction} />
            </div>
          </div>

          <div className="card shadow-sm">
            <div className="card-header bg-white fw-medium">History</div>
            <div className="card-body py-1">
              {historyError
                ? <p className="text-danger small my-2">Couldn't load the history: {historyError}</p>
                : <HistoryList entries={history} />}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}
