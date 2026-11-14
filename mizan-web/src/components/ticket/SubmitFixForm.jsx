import { useState } from 'react'
import { fieldError } from '../../utils/fieldErrors.js'

// An assigned member hands in the fix: where the fixed code is, and what was changed.
// The summary becomes the response the client reads once the manager approves it.
export default function SubmitFixForm({ busy, fieldErrors, onRun }) {
  const [form, setForm] = useState({ branch: '', commitSha: '', summary: '' })

  const handleChange = (e) => setForm((current) => ({ ...current, [e.target.name]: e.target.value }))
  const invalid = (name) => (fieldError(fieldErrors, name) ? ' is-invalid' : '')
  const feedback = (name) =>
    fieldError(fieldErrors, name) && <div className="invalid-feedback">{fieldError(fieldErrors, name)}</div>

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        // Trim pasted values: a trailing space would fail the SHA check
        onRun({ branch: form.branch.trim(), commitSha: form.commitSha.trim(), summary: form.summary.trim() })
      }}
    >
      <div className="row g-2">
        <div className="col-md-5">
          <label htmlFor="fix-branch" className="form-label small mb-1">Branch with the fix</label>
          <input id="fix-branch" name="branch" className={`form-control${invalid('Branch')}`}
            value={form.branch} onChange={handleChange} required maxLength={200} />
          {feedback('Branch')}
        </div>
        <div className="col-md-7">
          <label htmlFor="fix-sha" className="form-label small mb-1">Commit SHA (the "after" code)</label>
          <input id="fix-sha" name="commitSha" className={`form-control font-monospace${invalid('CommitSha')}`}
            value={form.commitSha} onChange={handleChange} required maxLength={64} />
          {feedback('CommitSha')}
        </div>
      </div>
      <label htmlFor="fix-summary" className="form-label small mt-2 mb-1">What did you change?</label>
      <textarea id="fix-summary" name="summary" rows={3} className={`form-control${invalid('Summary')}`}
        value={form.summary} onChange={handleChange} required maxLength={4000} />
      {feedback('Summary')}
      <button type="submit" className="btn btn-primary mt-2" disabled={busy}>Submit fix</button>
    </form>
  )
}
