// Display helpers: turn the API's values into friendly text and Bootstrap colours

const statusColors = {
  Submitted: 'secondary',
  Routed: 'info',
  InProgress: 'primary',
  LeaderReview: 'warning',
  ManagerReview: 'warning',
  ResponseSent: 'success',
  Closed: 'dark',
  Reopened: 'danger',
  Rejected: 'danger',
}

const priorityColors = {
  Low: 'secondary',
  Medium: 'info',
  High: 'warning',
  Critical: 'danger',
}

// "LeaderReview" -> "Leader Review"
export function splitWords(value) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export const statusColor = (status) => statusColors[status] ?? 'secondary'
export const priorityColor = (priority) => priorityColors[priority] ?? 'secondary'
export const roleLabel = (role) => splitWords(role)

// Audit log action names → readable text. Anything not listed is just split into words.
const actionLabels = {
  Created: 'Ticket created',
  Route: 'Routed',
  TeamAssigned: 'Team chosen',
  MemberAssigned: 'Member assigned',
  Assign: 'Work started',
  Submit: 'Fix submitted',
  LeaderApprove: 'Leader approved',
  LeaderSendBack: 'Leader sent back',
  ManagerApprove: 'Manager approved and sent',
  ManagerSendBack: 'Manager sent back',
  Close: 'Closed by client',
  Reopen: 'Reopened by client',
  Reject: 'Rejected',
}
export const actionLabel = (action) => actionLabels[action] ?? splitWords(action)

// The API stores times in UTC. If the value has no zone marker, treat it as UTC
// so the browser converts it to local time instead of reading it as local already.
export function formatDate(value) {
  if (!value) return ''
  const hasZone = /[zZ]|[+-]\d\d:\d\d$/.test(value)
  return new Date(hasZone ? value : `${value}Z`).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}
