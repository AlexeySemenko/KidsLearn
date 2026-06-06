import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

const navByRole = {
  Parent: [
    { to: '/parent', label: 'Dashboard' },
    { to: '/parent/children', label: 'Children' },
    { to: '/parent/lessons', label: 'Lessons' },
    { to: '/parent/reports', label: 'Reports' },
  ],
  Child: [
    { to: '/child', label: 'Assignments' },
    { to: '/child/results', label: 'Results' },
  ],
}

export default function AppShell() {
  const location = useLocation()
  const { logout, role, user } = useAuth()
  const navItems = navByRole[role] ?? []

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-kicker">KidsLearn Platform</span>
          <span className="brand-title">{role === 'Child' ? 'Child workspace' : 'Parent workspace'}</span>
          <span className="brand-copy">
            Single-container app shell for the first frontend delivery slice.
          </span>
        </div>

        <nav>
          <ul className="nav-list">
            {navItems.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end={item.to === '/parent' || item.to === '/child'}
                  className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}
                >
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </aside>

      <main className="content">
        <header className="header">
          <div className="header-copy">
            <span className="header-eyebrow">Authenticated shell</span>
            <h1>{user?.email ?? 'KidsLearn session'}</h1>
            <p>
              Route: {location.pathname}. This foundation now supports role-aware navigation,
              persisted auth state, and clean next steps for dashboard screens.
            </p>
          </div>

          <div className="button-row">
            <span className="badge">Role: {role ?? 'Unknown'}</span>
            <button type="button" className="logout-button" onClick={logout}>
              Log out
            </button>
          </div>
        </header>

        <Outlet />
      </main>
    </div>
  )
}
