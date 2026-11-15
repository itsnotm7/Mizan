import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'
import Logo from '../components/Logo.jsx'

// The seeded demo accounts (see DbSeeder.cs). Clicking one fills the form,
// so switching roles during the presentation takes one click.
const demoAccounts = [
  { label: 'Client', email: 'client@mizan.local', icon: 'bi-person' },
  { label: 'Teams Manager', email: 'manager@mizan.local', icon: 'bi-diagram-3' },
  { label: 'Team Leader', email: 'leader@mizan.local', icon: 'bi-person-badge' },
  { label: 'Member One', email: 'member1@mizan.local', icon: 'bi-code-slash' },
  { label: 'Member Two', email: 'member2@mizan.local', icon: 'bi-code-slash' },
]

// The three selling points on the navy side panel
const highlights = [
  { icon: 'bi-shield-check', text: 'Every fix is reviewed by the leader and the manager before the client sees it' },
  { icon: 'bi-git', text: 'Before and after commits saved with every ticket' },
  { icon: 'bi-clock-history', text: 'A full audit trail of who did what, and when' },
]

export default function LoginPage() {
  const { user, login } = useAuth()
  const location = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // Logged in (already, or just now): go back to the page they wanted, or the ticket list.
  // This one line handles the redirect after a successful login too.
  if (user) return <Navigate to={location.state?.from ?? '/tickets'} replace />

  async function handleSubmit(event) {
    event.preventDefault() // Stop the browser from reloading the page
    setError(null)
    setSubmitting(true)
    try {
      await login(email, password)
    } catch (err) {
      setError(err.message)
      setSubmitting(false)
    }
  }

  return (
    <div className="container d-flex justify-content-center align-items-center min-vh-100 py-4">
      <div className="card w-100 login-shell">
        <div className="row g-0">
          {/* Branded side panel: hidden on phones, where the form alone fits better */}
          <div className="col-md-5 login-panel d-none d-md-flex flex-column justify-content-between p-4">
            <div>
              <Logo size={36} />
              <p className="mt-3 mb-4 opacity-75">Code review requests, weighed before they ship.</p>
              <ul className="list-unstyled d-flex flex-column gap-3 mb-0">
                {highlights.map((h) => (
                  <li key={h.icon} className="d-flex gap-2 small">
                    <i className={`bi ${h.icon} fs-5`} aria-hidden="true"></i>
                    <span className="opacity-75">{h.text}</span>
                  </li>
                ))}
              </ul>
            </div>
            <p className="small opacity-50 mb-0 mt-4">CoderZ final project · 2026</p>
          </div>

          <div className="col-md-7">
            <div className="card-body p-4 p-lg-5">
              {/* On phones the side panel is hidden, so the logo shows above the form instead */}
              <div className="d-md-none text-center mb-3"><Logo size={32} light={false} /></div>
              <h1 className="h4 page-title mb-1">Sign in</h1>
              <p className="text-muted mb-4">Welcome back. Pick a demo account below or type your email.</p>

              {error && <div className="alert alert-danger py-2">{error}</div>}

              <form onSubmit={handleSubmit}>
                <div className="mb-3">
                  <label htmlFor="email" className="form-label">Email</label>
                  <input
                    id="email"
                    type="email"
                    className="form-control"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    autoFocus
                  />
                </div>
                <div className="mb-3">
                  <label htmlFor="password" className="form-label">Password</label>
                  <input
                    id="password"
                    type="password"
                    className="form-control"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                  />
                </div>
                <button type="submit" className="btn btn-primary w-100" disabled={submitting}>
                  {submitting ? 'Signing in…' : 'Sign in'}
                </button>
              </form>

              <hr />
              <p className="small text-muted mb-2">Demo accounts (password from the seeder):</p>
              <div className="d-flex flex-wrap gap-2">
                {demoAccounts.map((account) => (
                  <button
                    key={account.email}
                    type="button"
                    className="btn btn-outline-secondary btn-sm"
                    onClick={() => setEmail(account.email)}
                  >
                    <i className={`bi ${account.icon} me-1`} aria-hidden="true"></i>
                    {account.label}
                  </button>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
