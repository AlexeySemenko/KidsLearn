import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

const THEME_STORAGE_KEY = 'kidslearn.theme.mode'

const navByRole = {
  Parent: [
    { to: '/parent', label: 'Dashboard' },
    { to: '/parent/children', label: 'Children' },
    { to: '/parent/lessons', label: 'Lessons' },
    { to: '/parent/assignments', label: 'Assignments' },
    { to: '/parent/ai', label: 'AI Lessons' },
    { to: '/parent/reports', label: 'Reports' },
  ],
  Child: [
    { to: '/child', label: 'Assignments' },
    { to: '/child/results', label: 'Results' },
  ],
}

function toNameOnly(value) {
  if (!value || typeof value !== 'string') {
    return null
  }

  const trimmed = value.trim()
  if (!trimmed) {
    return null
  }

  const localPart = trimmed.includes('@') ? trimmed.split('@')[0] : trimmed
  const firstToken = localPart.split(/[._\-\s]+/).find(Boolean) ?? localPart

  if (!firstToken) {
    return null
  }

  return `${firstToken.charAt(0).toUpperCase()}${firstToken.slice(1)}`
}

export default function AppShell() {
  const location = useLocation()
  const { logout, role, user } = useAuth()
  const navItems = navByRole[role] ?? []
  const [isSidebarOpen, setIsSidebarOpen] = useState(false)
  const [themeMode, setThemeMode] = useState('system')
  const [prefersDark, setPrefersDark] = useState(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return false
    }

    return window.matchMedia('(prefers-color-scheme: dark)').matches
  })

  const resolvedTheme = themeMode === 'system' ? (prefersDark ? 'dark' : 'light') : themeMode
  const displayName = toNameOnly(user?.displayName) || toNameOnly(user?.email)
  const shellTitle = role === 'Child'
    ? `Hello, ${displayName || 'learner'}!`
    : (displayName || 'KidsLearn session')

  useEffect(() => {
    setIsSidebarOpen(false)
  }, [location.pathname, role])

  useEffect(() => {
    if (typeof window === 'undefined') {
      return
    }

    const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY)
    if (storedTheme === 'dark' || storedTheme === 'light' || storedTheme === 'system') {
      setThemeMode(storedTheme)
    }
  }, [])

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return undefined
    }

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')

    function handleChange(event) {
      setPrefersDark(event.matches)
    }

    if (typeof mediaQuery.addEventListener === 'function') {
      mediaQuery.addEventListener('change', handleChange)
      return () => mediaQuery.removeEventListener('change', handleChange)
    }

    mediaQuery.addListener(handleChange)
    return () => mediaQuery.removeListener(handleChange)
  }, [])

  useEffect(() => {
    if (typeof window === 'undefined') {
      return
    }

    window.document.documentElement.setAttribute('data-theme', resolvedTheme)

    if (themeMode === 'system') {
      window.localStorage.removeItem(THEME_STORAGE_KEY)
      return
    }

    window.localStorage.setItem(THEME_STORAGE_KEY, themeMode)
  }, [resolvedTheme, themeMode])

  function toggleDarkMode() {
    setThemeMode((current) => {
      const currentResolved = current === 'system' ? (prefersDark ? 'dark' : 'light') : current
      return currentResolved === 'dark' ? 'light' : 'dark'
    })
  }

  return (
    <div className={`app-shell${isSidebarOpen ? ' nav-open' : ''}${role === 'Child' ? ' child-shell' : ''}`}>
      <div className="mobile-shell-bar">
        <span className="mobile-shell-title">{role === 'Child' ? 'Child workspace' : 'Parent workspace'}</span>
        <button
          type="button"
          className="mobile-menu-button"
          aria-expanded={isSidebarOpen}
          aria-controls="app-sidebar-nav"
          aria-label={isSidebarOpen ? 'Close navigation menu' : 'Open navigation menu'}
          onClick={() => setIsSidebarOpen((current) => !current)}
        >
          <span aria-hidden="true">{isSidebarOpen ? '×' : '☰'}</span>
        </button>
      </div>

      {isSidebarOpen ? (
        <button
          type="button"
          className="sidebar-overlay"
          aria-label="Close navigation menu"
          onClick={() => setIsSidebarOpen(false)}
        />
      ) : null}

      <aside className={`sidebar${isSidebarOpen ? ' open' : ''}`}>
        <div className="brand">
          <span className="brand-kicker">KidsLearn Platform</span>
          <span className="brand-title">{role === 'Child' ? 'Child workspace' : 'Parent workspace'}</span>
          <span className="brand-copy">
            Single-container app shell for the first frontend delivery slice.
          </span>
        </div>

        <nav id="app-sidebar-nav">
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

        <div className="theme-toggle-inline">
          <span>Dark mode</span>
          <button
            type="button"
            role="switch"
            aria-checked={resolvedTheme === 'dark'}
            aria-label="Toggle dark mode"
            className={`theme-switch${resolvedTheme === 'dark' ? ' active' : ''}`}
            onClick={toggleDarkMode}
          >
            <span className="theme-switch-thumb" aria-hidden="true" />
          </button>
        </div>
      </aside>

      <main className={`content${role === 'Child' ? ' child-shell-content' : ''}`}>
        {role === 'Child' ? (
          <div className="child-shell-bubbles" aria-hidden="true">
            <span className="child-shell-bubble c1">⭐</span>
            <span className="child-shell-bubble c2">🎈</span>
            <span className="child-shell-bubble c3">✨</span>
            <span className="child-shell-bubble c4">🚀</span>
            <span className="child-shell-bubble c5">🌟</span>
            <span className="child-shell-bubble c6">☁️</span>
          </div>
        ) : null}

        <header className={`header${role === 'Child' ? ' child-shell-header' : ''}`}>
          <div className="header-copy">
            <span className="header-eyebrow">{role === 'Child' ? 'Learning adventure' : 'Authenticated shell'}</span>
            <h1>{shellTitle}</h1>
            <p>
              {role === 'Child'
                ? 'Your missions, progress, and results all live here. Pick a mission and have fun learning.'
                : `Route: ${location.pathname}. This foundation now supports role-aware navigation,
              persisted auth state, and clean next steps for dashboard screens.`}
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
