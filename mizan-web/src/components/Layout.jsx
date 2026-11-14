import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'
import { roleLabel } from '../utils/labels.js'

// The navbar around every logged-in page. <Outlet /> is where the current page renders.
export default function Layout() {
  const { user, logout } = useAuth()

  return (
    <>
      <nav className="navbar navbar-dark bg-primary mb-4">
        <div className="container">
          <Link className="navbar-brand fw-semibold" to="/tickets">
            Mizan <span className="opacity-75">ميزان</span>
          </Link>
          <div className="d-flex align-items-center gap-3 text-white">
            <span>
              {user.name}{' '}
              <span className="badge text-bg-light">{roleLabel(user.role)}</span>
            </span>
            <button className="btn btn-outline-light btn-sm" onClick={logout}>
              Log out
            </button>
          </div>
        </div>
      </nav>

      <main className="container pb-5">
        <Outlet />
      </main>
    </>
  )
}
