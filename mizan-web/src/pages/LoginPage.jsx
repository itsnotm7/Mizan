import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'

// The seeded demo accounts (see DbSeeder.cs). Clicking one fills the form,
// so switching roles during the presentation takes one click.
const demoAccounts = [
  { label: 'Client', email: 'client@mizan.local' },
  { label: 'Teams Manager', email: 'manager@mizan.local' },
  { label: 'Team Leader', email: 'leader@mizan.local' },
  { label: 'Member One', email: 'member1@mizan.local' },
  { label: 'Member Two', email: 'member2@mizan.local' },
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
    <div className="container d-flex justify-content-center align-items-center min-vh-100">
      <div className="card shadow-sm w-100 login-card">
        <div className="card-body p-4">
          <h1 className="h3 text-center mb-1">Mizan <span className="text-primary">ميزان</span></h1>
          <p className="text-center text-muted mb-4">Code review requests, weighed before they ship</p>

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
                {account.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
