// How each workflow action LOOKS: its URL, button text and colour.
// WHICH actions a user gets is not decided here: the server sends them in allowedActions,
// worked out from the same rules table (TicketWorkflow.cs) that enforces them.
//
// form: the action needs input first (a team, members, or the fix), so it opens a small form.
// confirm: the action can't be undone, so ask before sending it.
export const workflowActions = {
  Route:           { path: 'route',             label: 'Route to team',  variant: 'primary', form: 'route' },
  Reject:          { path: 'reject',            label: 'Reject',         variant: 'outline-danger', confirm: 'Reject this ticket? This cannot be undone.' },
  Assign:          { path: 'assign',            label: 'Assign members', variant: 'primary', form: 'assign' },
  Submit:          { path: 'submit',            label: 'Submit fix',     variant: 'primary', form: 'submit' },
  LeaderApprove:   { path: 'leader-approve',    label: 'Approve',        variant: 'success' },
  LeaderSendBack:  { path: 'leader-send-back',  label: 'Send back',      variant: 'outline-warning' },
  ManagerApprove:  { path: 'manager-approve',   label: 'Approve and send to client', variant: 'success', confirm: 'Send this response to the client?' },
  ManagerSendBack: { path: 'manager-send-back', label: 'Send back',      variant: 'outline-warning' },
  Close:           { path: 'close',             label: 'Accept and close', variant: 'success', confirm: 'Accept the fix and close this ticket?' },
  Reopen:          { path: 'reopen',            label: 'Reopen',         variant: 'outline-danger' },
}
