import { workflowActions } from '../../workflow/actions.js'
import AssignForm from './AssignForm.jsx'
import RouteForm from './RouteForm.jsx'
import SubmitFixForm from './SubmitFixForm.jsx'

// One control per action the SERVER says this user can take right now (allowedActions).
// If the list is empty, the user has nothing to do on this ticket at the moment.
export default function ActionsPanel({ ticket, busy, error, fieldErrors, onAction }) {
  const actions = ticket.allowedActions.filter((name) => workflowActions[name])

  // Still show the error: after a 409 the refreshed ticket may leave this user with no actions
  if (actions.length === 0) {
    return (
      <>
        {error && <div className="alert alert-warning py-2">{error}</div>}
        <p className="text-muted small mb-0">Nothing for you to do on this ticket right now.</p>
      </>
    )
  }

  const simple = actions.filter((name) => !workflowActions[name].form)
  const withForm = actions.filter((name) => workflowActions[name].form)

  function runSimple(name) {
    const action = workflowActions[name]
    if (action.confirm && !window.confirm(action.confirm)) return
    onAction(name)
  }

  return (
    <>
      {error && <div className="alert alert-danger py-2">{error}</div>}

      {withForm.map((name) => (
        <div key={name} className="mb-3">
          <div className="fw-medium mb-2">
            <i className={`bi ${workflowActions[name].icon} me-2`} aria-hidden="true"></i>
            {workflowActions[name].label}
          </div>
          {workflowActions[name].form === 'route' && <RouteForm busy={busy} onRun={(body) => onAction(name, body)} />}
          {workflowActions[name].form === 'assign' && (
            <AssignForm ticket={ticket} busy={busy} onRun={(body) => onAction(name, body)} />
          )}
          {workflowActions[name].form === 'submit' && (
            <SubmitFixForm busy={busy} fieldErrors={fieldErrors} onRun={(body) => onAction(name, body)} />
          )}
        </div>
      ))}

      {simple.length > 0 && (
        <div className="d-flex flex-wrap gap-2">
          {simple.map((name) => (
            <button
              key={name}
              type="button"
              className={`btn btn-${workflowActions[name].variant}`}
              disabled={busy}
              onClick={() => runSimple(name)}
            >
              <i className={`bi ${workflowActions[name].icon} me-1`} aria-hidden="true"></i>
              {workflowActions[name].label}
            </button>
          ))}
        </div>
      )}
    </>
  )
}
