import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext.jsx'
import { roleLabel } from '../utils/labels.js'
import Logo from './Logo.jsx'

// The navbar around every logged-in page. <Outlet /> is where the current page renders.
export default function Layout() {
  const { user, logout } = useAuth()

  return (
    <>
      <nav className="navbar navbar-dark navbar-mizan mb-4">
        <div className="container">
          <Link className="navbar-brand" to="/tickets">
            <Logo />
          </Link>
          <div className="d-flex align-items-center gap-3 text-white">
            <span className="d-flex align-items-center gap-2">
              <i className="bi bi-person-circle fs-5 opacity-75" aria-hidden="true"></i>
              <span className="d-none d-sm-inline">{user.name}</span>
              <span className="badge role-badge">{roleLabel(user.role)}</span>
            </span>
            <button className="btn btn-outline-light btn-sm" onClick={logout}>
              <i className="bi bi-box-arrow-right me-1" aria-hidden="true"></i>
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
