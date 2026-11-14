import { useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api/client.js'
import { useAuth } from '../auth/AuthContext.jsx'
import { fieldError, summarizeError } from '../utils/fieldErrors.js'

const priorities = ['Low', 'Medium', 'High', 'Critical']

const emptyForm = {
  title: '',
  description: '',
  repositoryUrl: '',
  branch: 'main',
  commitSha: '',
  priority: 'Medium',
}

export default function CreateTicketPage() {
  const { user } = useAuth()
  const navigate = useNavigate()

  const [form, setForm] = useState(emptyForm)
  const [error, setError] = useState(null)
  const [fieldErrors, setFieldErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const submitting = useRef(false)

  // Only clients create tickets. The API refuses anyone else with 403; this just explains it.
  if (user.role !== 'Client') {
    return (
      <div className="card card-body text-center">
        <p className="mb-2">Only clients can create tickets.</p>
        <Link to="/tickets">Back to tickets</Link>
      </div>
    )
  }

  // One change handler for every field: the input's name says which value to update
  function handleChange(event) {
    const { name, value } = event.target
    setForm((current) => ({ ...current, [name]: value }))
  }

  async function handleSubmit(event) {
    event.preventDefault()
    // A second click before React redraws the disabled button would send a second ticket
    if (submitting.current) return
    submitting.current = true

    setError(null)
    setFieldErrors({})
    setSaving(true)
    try {
      // Pasted values often carry a stray space or newline, which would fail the SHA/URL checks
      const body = Object.fromEntries(Object.entries(form).map(([k, v]) => [k, v.trim()]))
      const ticket = await api('/tickets', { method: 'POST', body })
      // replace: pressing Back afterwards shouldn't land on an empty form and invite a duplicate
      navigate(`/tickets/${ticket.id}`, { replace: true })
    } catch (err) {
      // A 400 lists what's wrong per field: those show under each input, the rest in the alert
      setFieldErrors(err.fieldErrors ?? {})
      setError(summarizeError(err, Object.keys(emptyForm)))
      setSaving(false)
      submitting.current = false
    }
  }

  // Bootstrap marks a field red with "is-invalid"; the message goes in "invalid-feedback" below it
  const invalid = (name) => (fieldError(fieldErrors, name) ? ' is-invalid' : '')
  const feedback = (name) =>
    fieldError(fieldErrors, name) && <div className="invalid-feedback">{fieldError(fieldErrors, name)}</div>

  return (
    <div className="row justify-content-center">
      <div className="col-lg-8">
        <Link to="/tickets" className="small text-decoration-none">← Back to tickets</Link>
        <h1 className="h4 mt-2 mb-3">New ticket</h1>

        <div className="card shadow-sm">
          <div className="card-body">
            {error && <div className="alert alert-danger py-2">{error}</div>}

            {/* The browser checks only required + length. The real rules (URL format, hex SHA)
                live on the server, and its messages appear under each field. */}
            <form onSubmit={handleSubmit}>
              <div className="mb-3">
                <label htmlFor="title" className="form-label">Title</label>
                <input id="title" name="title" className={`form-control${invalid('Title')}`}
                  value={form.title} onChange={handleChange} required maxLength={200} />
                {feedback('Title')}
              </div>

              <div className="mb-3">
                <label htmlFor="description" className="form-label">What needs fixing?</label>
                <textarea id="description" name="description" rows={5}
                  className={`form-control${invalid('Description')}`}
                  value={form.description} onChange={handleChange} required maxLength={4000} />
                {feedback('Description')}
              </div>

              <div className="mb-3">
                <label htmlFor="repositoryUrl" className="form-label">GitHub repository</label>
                <input id="repositoryUrl" name="repositoryUrl" placeholder="https://github.com/owner/repo"
                  className={`form-control${invalid('RepositoryUrl')}`}
                  value={form.repositoryUrl} onChange={handleChange} required maxLength={500} />
                {feedback('RepositoryUrl')}
              </div>

              <div className="row">
                <div className="col-md-4 mb-3">
                  <label htmlFor="branch" className="form-label">Branch</label>
                  <input id="branch" name="branch" className={`form-control${invalid('Branch')}`}
                    value={form.branch} onChange={handleChange} required maxLength={200} />
                  {feedback('Branch')}
                </div>
                <div className="col-md-5 mb-3">
                  <label htmlFor="commitSha" className="form-label">Commit SHA (the "before" code)</label>
                  <input id="commitSha" name="commitSha" placeholder="e.g. a1b2c3d"
                    className={`form-control font-monospace${invalid('CommitSha')}`}
                    value={form.commitSha} onChange={handleChange} required maxLength={64} />
                  {feedback('CommitSha')}
                </div>
                <div className="col-md-3 mb-3">
                  <label htmlFor="priority" className="form-label">Priority</label>
                  <select id="priority" name="priority" className={`form-select${invalid('Priority')}`}
                    value={form.priority} onChange={handleChange}>
                    {priorities.map((p) => <option key={p}>{p}</option>)}
                  </select>
                  {feedback('Priority')}
                </div>
              </div>

              <div className="d-flex justify-content-end gap-2">
                <Link to="/tickets" className="btn btn-outline-secondary">Cancel</Link>
                <button type="submit" className="btn btn-primary" disabled={saving}>
                  {saving ? 'Submitting…' : 'Submit ticket'}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
