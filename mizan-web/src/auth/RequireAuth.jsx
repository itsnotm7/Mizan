import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext.jsx'

// Wraps pages that need a login. This only decides what to SHOW:
// the API still checks the token on every request, so hiding a page is not the security.
export default function RequireAuth({ children }) {
  const { user, checking, signedOut } = useAuth()
  const location = useLocation()

  if (checking) {
    return <div className="text-center text-muted mt-5">Loading…</div>
  }

  if (!user) {
    // A visitor who opened a link while logged out: remember it, so login sends them there.
    // After a logout (or an expired session) start fresh instead: the next person to log in
    // may be a different account, and must not land on the previous user's page.
    const state = signedOut ? undefined : { from: location.pathname + location.search }
    return <Navigate to="/login" replace state={state} />
  }

  return children
}
