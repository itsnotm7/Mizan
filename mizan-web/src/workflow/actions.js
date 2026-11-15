// How each workflow action LOOKS: its URL, button text and colour.
// WHICH actions a user gets is not decided here: the server sends them in allowedActions,
// worked out from the same rules table (TicketWorkflow.cs) that enforces them.
//
// form: the action needs input first (a team, members, or the fix), so it opens a small form.
// confirm: the action can't be undone, so ask before sending it.
// icon: a Bootstrap Icons name shown on the button.
export const workflowActions = {
  Route:           { path: 'route',             label: 'Route to team',  variant: 'primary', icon: 'bi-signpost-split', form: 'route' },
  Reject:          { path: 'reject',            label: 'Reject',         variant: 'outline-danger', icon: 'bi-x-lg', confirm: 'Reject this ticket? This cannot be undone.' },
  Assign:          { path: 'assign',            label: 'Assign members', variant: 'primary', icon: 'bi-person-plus', form: 'assign' },
  Submit:          { path: 'submit',            label: 'Submit fix',     variant: 'primary', icon: 'bi-upload', form: 'submit' },
  LeaderApprove:   { path: 'leader-approve',    label: 'Approve',        variant: 'success', icon: 'bi-check-lg' },
  LeaderSendBack:  { path: 'leader-send-back',  label: 'Send back',      variant: 'outline-warning', icon: 'bi-arrow-counterclockwise' },
  ManagerApprove:  { path: 'manager-approve',   label: 'Approve and send to client', variant: 'success', icon: 'bi-send', confirm: 'Send this response to the client?' },
  ManagerSendBack: { path: 'manager-send-back', label: 'Send back',      variant: 'outline-warning', icon: 'bi-arrow-counterclockwise' },
  Close:           { path: 'close',             label: 'Accept and close', variant: 'success', icon: 'bi-lock', confirm: 'Accept the fix and close this ticket?' },
  Reopen:          { path: 'reopen',            label: 'Reopen',         variant: 'outline-danger', icon: 'bi-unlock' },
}
