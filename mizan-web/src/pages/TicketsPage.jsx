import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, isAbort } from '../api/client.js'
import { useAuth } from '../auth/AuthContext.jsx'
import { formatDate, priorityColor, splitWords, statusColor } from '../utils/labels.js'

export default function TicketsPage() {
  const { user } = useAuth()
  const navigate = useNavigate()

  const [tickets, setTickets] = useState(null) // null = still loading
  const [error, setError] = useState(null)

  useEffect(() => {
    // The API already filters by role (TicketVisibility), so we show whatever comes back
    const controller = new AbortController()
    api('/tickets', { signal: controller.signal })
      .then(setTickets)
      .catch((err) => {
        if (!isAbort(err)) setError(err.message)
      })

    // Leaving the page cancels the request, so a late answer can't update a closed page
    return () => controller.abort()
  }, [])

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h1 className="h4 page-title mb-0">Tickets</h1>
          {tickets?.length > 0 && <span className="small text-muted">{tickets.length} shown for your role</span>}
        </div>
        {/* Only clients create tickets. Hiding the button is for convenience; the API enforces it. */}
        {user.role === 'Client' && (
          <button className="btn btn-primary" onClick={() => navigate('/tickets/new')}>
            <i className="bi bi-plus-lg me-1" aria-hidden="true"></i>
            New ticket
          </button>
        )}
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {!error && tickets === null && <p className="text-muted">Loading tickets…</p>}

      {tickets?.length === 0 && (
        <div className="card text-center empty-state">
          <i className="bi bi-inbox d-block mb-2" aria-hidden="true"></i>
          No tickets to show yet.
        </div>
      )}

      {tickets?.length > 0 && (
        <div className="card overflow-hidden">
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th>Title</th>
                  <th>Status</th>
                  <th>Priority</th>
                  <th>Client</th>
                  <th>Team</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {tickets.map((ticket) => (
                  <tr key={ticket.id} className="row-link" onClick={() => navigate(`/tickets/${ticket.id}`)}>
                    {/* A real link too, so keyboard users and middle-click work */}
                    <td className="fw-medium">
                      <Link to={`/tickets/${ticket.id}`} className="text-reset text-decoration-none">
                        {ticket.title}
                      </Link>
                    </td>
                    <td>
                      <span className={`badge text-bg-${statusColor(ticket.status)}`}>
                        {splitWords(ticket.status)}
                      </span>
                    </td>
                    <td>
                      <span className={`badge text-bg-${priorityColor(ticket.priority)}`}>{ticket.priority}</span>
                    </td>
                    <td>{ticket.clientName}</td>
                    <td>{ticket.teamName ?? <span className="text-muted">Not routed</span>}</td>
                    <td className="text-nowrap">{formatDate(ticket.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </>
  )
}
