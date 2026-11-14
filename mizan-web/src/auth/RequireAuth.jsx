import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext.jsx'

// Wraps pages that need a login. This only decides what to SHOW:
// the API still checks the token on every request, so hiding a page is not the security.
export default function RequireAuth({ children }) {
  const { user, checking } = useAuth()
  const location = useLocation()

  if (checking) {
    return <div className="text-center text-muted mt-5">Loading…</div>
  }

  if (!user) {
    // Remember where they were going, so login can send them back there
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return children
}
