// The API's 400 response lists messages per field: { "RepositoryUrl": ["..."] }.
// Returns the first message for one field, or null. The key's casing depends on the
// server's settings, so the lookup ignores case.
export function fieldError(errors, name) {
  if (!errors) return null
  const key = Object.keys(errors).find((k) => k.toLowerCase() === name.toLowerCase())
  if (!key) return null
  const value = errors[key]
  return Array.isArray(value) ? value[0] : value
}

// The one message for the alert at the top of a form.
// knownFields: the inputs this form shows. Their messages appear under the input itself;
// any other key (e.g. "$.teamId" when the JSON didn't bind) has nowhere to show, so it goes here.
export function summarizeError(err, knownFields) {
  const errors = err.fieldErrors ?? {}
  const known = knownFields.map((f) => f.toLowerCase())
  const unmatchedKeys = Object.keys(errors).filter((k) => !known.includes(k.toLowerCase()))
  const matchedCount = Object.keys(errors).length - unmatchedKeys.length

  const parts = unmatchedKeys.flatMap((k) => (Array.isArray(errors[k]) ? errors[k] : [errors[k]]))
  if (matchedCount > 0) parts.unshift('Please fix the highlighted fields.')
  return parts.length > 0 ? parts.join(' ') : err.message
}
