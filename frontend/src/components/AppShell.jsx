import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

const THEME_STORAGE_KEY = 'kidslearn.theme.mode'

const navByRole = {
  Parent: [
    { to: '/parent', label: 'Home' },
    { to: '/parent/children', label: 'Children' },
    { to: '/parent/lessons', label: 'Lessons' },
    { to: '/parent/assignments', label: 'Assignments' },
    { to: '/parent/reports', label: 'Reports' },
    { to: '/parent/manage', label: 'Link parent' },
  ],
  Child: [
    { to: '/child', label: 'Home' },
    { to: '/child/results', label: 'My lessons' },
    { to: '/child/friends', label: 'Friends' },
  ],
  Admin: [
    { to: '/parent', label: 'Home' },
    { to: '/parent/children', label: 'Children' },
    { to: '/parent/lessons', label: 'Lessons' },
    { to: '/parent/assignments', label: 'Assignments' },
    { to: '/parent/reports', label: 'Reports' },
    { to: '/parent/manage', label: 'Link parent' },
    { to: '/admin/users', label: 'Users', adminBadge: true },
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

function UserMenu({ displayName, userInitial, role, user, isOpen, onToggle, onLogout, showRole }) {
  const btnRef = useRef(null)
  const [dropdownStyle, setDropdownStyle] = useState({})

  useEffect(() => {
    if (isOpen && btnRef.current) {
      const rect = btnRef.current.getBoundingClientRect()
      setDropdownStyle({
        position: 'fixed',
        top: rect.bottom + 6,
        right: window.innerWidth - rect.right,
      })
    }
  }, [isOpen])

  return (
    <div className="user-menu">
      <button
        ref={btnRef}
        type="button"
        className="user-avatar-btn"
        onClick={onToggle}
        aria-expanded={isOpen}
        aria-haspopup="true"
      >
        <span className="user-avatar-initial" aria-hidden="true">{userInitial}</span>
        <span className="user-avatar-name">{displayName || user?.email}</span>
        {showRole ? <span className={`user-avatar-role${role === 'Admin' ? ' user-avatar-role--admin' : ''}`}>{role}</span> : null}
      </button>

      {isOpen ? createPortal(
        <div className="user-dropdown" role="menu" style={dropdownStyle} data-user-dropdown="true">
          <div className="user-dropdown-header">
            <strong>{displayName || '—'}</strong>
            {user?.email ? <span>{user.email}</span> : null}
            <span className={`user-dropdown-role${role === 'Admin' ? ' user-dropdown-role--admin' : ''}`}>{role}</span>
          </div>
          <hr className="user-dropdown-divider" />
          <button
            type="button"
            className="user-dropdown-item"
            role="menuitem"
            onClick={onLogout}
          >
            Log out
          </button>
        </div>,
        document.body
      ) : null}
    </div>
  )
}

export default function AppShell() {
  const location = useLocation()
  const { logout, role, user } = useAuth()
  const navItems = navByRole[role] ?? []
  const [isSidebarOpen, setIsSidebarOpen] = useState(false)
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false)
  const [themeMode, setThemeMode] = useState('system')
  const [prefersDark, setPrefersDark] = useState(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return false
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  })

  const resolvedTheme = themeMode === 'system' ? (prefersDark ? 'dark' : 'light') : themeMode
  const displayName = toNameOnly(user?.displayName) || toNameOnly(user?.email)
  const userInitial = (displayName || user?.email || '?').charAt(0).toUpperCase()

  const currentNavItem = navItems
    .filter((item) => location.pathname === item.to || location.pathname.startsWith(item.to + '/'))
    .sort((a, b) => b.to.length - a.to.length)[0]
  const pageTitle = currentNavItem?.label ?? ''
  const homeRoute = role === 'Child' ? '/child' : '/parent'

  useEffect(() => {
    setIsSidebarOpen(false)
  }, [location.pathname, role])

  useEffect(() => {
    if (!isUserMenuOpen) {
      return undefined
    }

    function handleClickOutside(event) {
      if (!event.target.closest('.user-menu') && !event.target.closest('[data-user-dropdown]')) {
        setIsUserMenuOpen(false)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [isUserMenuOpen])

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

  function handleUserMenuToggle() {
    setIsUserMenuOpen((v) => !v)
  }

  function handleLogout() {
    setIsUserMenuOpen(false)
    logout()
  }

  const userMenuProps = {
    displayName,
    userInitial,
    role,
    user,
    isOpen: isUserMenuOpen,
    onToggle: handleUserMenuToggle,
    onLogout: handleLogout,
  }

  return (
    <div className={`app-shell${isSidebarOpen ? ' nav-open' : ''}${role === 'Child' ? ' child-shell' : ''}`}>
      <div className="mobile-shell-bar">
        <span className="mobile-shell-title">
          <Link to={homeRoute} className="mobile-shell-brand-link">KidsLearnAI</Link>
          {pageTitle ? <span className="mobile-shell-page-title"> › {pageTitle}</span> : null}
        </span>

        <div className="mobile-shell-bar-right">
          {/* User menu visible only on mobile, no role badge */}
          <div className="mobile-user-slot">
            <UserMenu {...userMenuProps} showRole={false} />
          </div>

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
          <span className="brand-title">KidsLearnAI</span>
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
                  {item.adminBadge ? <span className="badge badge--admin nav-admin-badge">ADMIN</span> : null}
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

      <div className={`content${role === 'Child' ? ' child-shell-content' : ''}`}>
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

        <div className="topbar">
          <span className="topbar-title">{pageTitle}</span>

          {/* User menu visible only on desktop, with role badge */}
          <div className="desktop-user-slot">
            <UserMenu {...userMenuProps} showRole={true} />
          </div>
        </div>

        <main className="shell-main">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
