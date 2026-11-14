// The ONE place that calls the API. Every page goes through api(), so the token,
// JSON handling and error format are dealt with once instead of on every screen.

const TOKEN_KEY = 'mizan.token'

export const tokenStore = {
  get: () => sessionStorage.getItem(TOKEN_KEY),
  set: (token) => sessionStorage.setItem(TOKEN_KEY, token),
  clear: () => sessionStorage.removeItem(TOKEN_KEY),
}

// Thrown for any non-2xx response. Carries the ProblemDetails the API sends back.
export class ApiError extends Error {
  constructor(status, title, fieldErrors = {}) {
    super(title)
    this.status = status
    // Validation failures (400) list messages per field: { Title: ["..."], ... }
    this.fieldErrors = fieldErrors
  }
}

// Set by AuthContext so a 401 anywhere logs the user out in one place
let onUnauthorized = () => {}
export function setUnauthorizedHandler(handler) {
  onUnauthorized = handler
}

// True when a request was cancelled on purpose (the page closed before the answer came)
export const isAbort = (err) => err?.name === 'AbortError'

// signal: lets a page cancel its request when it closes, like CancellationToken on the backend
export async function api(path, { method = 'GET', body, signal } = {}) {
  const headers = { Accept: 'application/json' }
  const token = tokenStore.get()
  if (token) headers.Authorization = `Bearer ${token}`
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  let response
  try {
    // Relative URL: in development the Vite proxy forwards /api to the backend
    response = await fetch(`/api${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal,
    })
  } catch (err) {
    if (isAbort(err)) throw err
    throw new ApiError(0, 'Cannot reach the server. Is the API running?')
  }

  // Some endpoints answer with no body, and a proxy error page isn't JSON
  const text = await response.text()
  let data = null
  try {
    data = text ? JSON.parse(text) : null
  } catch {
    data = null
  }

  if (!response.ok) {
    // An expired or revoked token (or a deactivated user): the session is over.
    // Only if that token is still the current one: a late answer to an old request
    // must not log out a session that started after it.
    if (response.status === 401 && token && token === tokenStore.get()) onUnauthorized()

    // The API always sends a ProblemDetails body. Without one, the Vite proxy
    // couldn't reach the API at all (it isn't running, or is still starting).
    if (!data && response.status >= 500)
      throw new ApiError(response.status, 'Cannot reach the server. Is the API running?')

    throw new ApiError(
      response.status,
      data?.title ?? `Request failed (${response.status})`,
      data?.errors ?? {},
    )
  }

  return data
}
