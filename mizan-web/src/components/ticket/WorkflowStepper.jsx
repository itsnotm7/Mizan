import { splitWords } from '../../utils/labels.js'

// The normal path a ticket takes, in order. DISPLAY ONLY: it shows where the ticket is.
// Which moves are allowed is still decided by the server (TicketWorkflow.cs → allowedActions).
const steps = ['Submitted', 'Routed', 'InProgress', 'LeaderReview', 'ManagerReview', 'ResponseSent', 'Closed']

const stateText = { done: 'completed', current: 'current step', failed: 'stopped here', todo: 'not reached yet' }

export default function WorkflowStepper({ status }) {
  let items

  if (status === 'Rejected') {
    // A rejected ticket never left step one: show Submitted, then a red end step
    items = [
      { key: 'Submitted', label: 'Submitted', state: 'done' },
      { key: 'Rejected', label: 'Rejected', state: 'failed' },
    ]
  } else {
    // Reopened goes back to the team, so it sits on the "In Progress" step, labelled in red
    const current = steps.indexOf(status === 'Reopened' ? 'InProgress' : status)
    items = steps.map((step, i) => ({
      key: step,
      label: status === 'Reopened' && i === current ? 'Reopened' : splitWords(step),
      // Closed is the last step: nothing left to do, so it shows as done, not "current"
      state: i < current || status === 'Closed' ? 'done'
        : i === current ? (status === 'Reopened' ? 'failed' : 'current')
        : 'todo',
    }))
  }

  // Where the ticket is now, for screen readers: the gold step, the red step, or the last step once closed
  const nowIndex = items.findIndex((item) => item.state === 'current' || item.state === 'failed')
  const currentIndex = nowIndex === -1 && status === 'Closed' ? items.length - 1 : nowIndex

  return (
    <ol className="stepper" aria-label="Ticket progress">
      {items.map((item, i) => (
        <li key={item.key} className={`stepper-step ${item.state}`}
          aria-current={i === currentIndex ? 'step' : undefined}>
          <div className="stepper-dot">
            {item.state === 'done' && <i className="bi bi-check-lg" aria-hidden="true"></i>}
            {item.state === 'failed' && <i className="bi bi-exclamation-lg" aria-hidden="true"></i>}
            {(item.state === 'current' || item.state === 'todo') && i + 1}
          </div>
          {item.label}
          {/* The icons are hidden from screen readers, so say the state in words */}
          <span className="visually-hidden"> ({stateText[item.state]})</span>
        </li>
      ))}
    </ol>
  )
}
