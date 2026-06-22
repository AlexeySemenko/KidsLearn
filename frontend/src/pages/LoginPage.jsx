import { Link, Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getChildGoogleStartUrl, getParentGoogleStartUrl } from '../lib/api'

const GOOGLE_SVG = (
  <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true" className="google-icon-svg">
    <path fill="#EA4335" d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.8-6-6.2s2.7-6.2 6-6.2c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 6.9 2 2.7 6.4 2.7 11.8s4.2 9.8 9.3 9.8c5.4 0 9-3.8 9-9.1 0-.6-.1-1-.1-1.4H12z" />
    <path fill="#34A853" d="M3.8 7.6l3.2 2.3C7.8 7.6 9.7 6 12 6c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 8.4 2 5.2 4.2 3.8 7.6z" />
    <path fill="#FBBC05" d="M12 22c2.6 0 4.8-.9 6.4-2.6l-3-2.5c-.8.6-2 1.2-3.4 1.2-3.9 0-5.2-2.6-5.5-3.9l-3.1 2.4C4.7 19.9 8 22 12 22z" />
    <path fill="#4285F4" d="M21 12.5c0-.7-.1-1.2-.2-1.8H12v3.9h5.1c-.2 1-.8 2.1-1.8 2.8l3 2.5c1.8-1.7 2.7-4.2 2.7-7.4z" />
  </svg>
)

export default function LoginPage() {
  const { isAuthenticated, role } = useAuth()
  const location = useLocation()

  if (isAuthenticated) {
    return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
  }

  const returnPath = location.state?.from?.pathname

  return (
    <main className="kl-login-root">
      {/* Floating background stars */}
      <div className="kl-login-bg" aria-hidden="true">
        {['⭐','🌟','✨','💫','⭐','✨','🌟','💫','⭐','🌟'].map((s, i) => (
          <span key={i} className={`kl-star kl-star-${i + 1}`}>{s}</span>
        ))}
      </div>

      {/* Logo */}
      <Link to="/" className="kl-logo" aria-label="KidsLearnAI home">
        <div className="kl-logo-rocket" aria-hidden="true">🚀</div>
        <div className="kl-logo-wordmark">
          <span className="kl-wm-kids">Kids</span><span className="kl-wm-learn">Learn</span><span className="kl-wm-ai">AI</span>
        </div>
        <p className="kl-logo-tagline">Learn smart. Grow fast. Have fun.</p>
      </Link>

      {/* Role cards */}
      <div className="kl-role-row">
        <button
          type="button"
          className="kl-role-card kl-role-parent"
          onClick={() => window.location.assign(getParentGoogleStartUrl(returnPath ?? '/parent'))}
        >
          <span className="kl-role-emoji" aria-hidden="true">👨‍👩‍👧</span>
          <span className="kl-role-title">I'm a Parent</span>
          <span className="kl-role-sub">Manage children, lessons &amp; reports</span>
          <span className="kl-google-btn">
            {GOOGLE_SVG}
            Sign in with Google
          </span>
        </button>

        <button
          type="button"
          className="kl-role-card kl-role-child"
          onClick={() => window.location.assign(getChildGoogleStartUrl(returnPath ?? '/child'))}
        >
          <span className="kl-role-emoji" aria-hidden="true">🧒</span>
          <span className="kl-role-title">I'm a Kid</span>
          <span className="kl-role-sub">View missions &amp; start learning</span>
          <span className="kl-google-btn">
            {GOOGLE_SVG}
            Sign in with Google
          </span>
        </button>
      </div>
    </main>
  )
}
