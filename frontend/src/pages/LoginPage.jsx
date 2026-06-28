import { useState } from 'react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getUnifiedGoogleStartUrl } from '../lib/api'

const GOOGLE_SVG = (
  <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true" className="google-icon-svg">
    <path fill="#EA4335" d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.8-6-6.2s2.7-6.2 6-6.2c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 6.9 2 2.7 6.4 2.7 11.8s4.2 9.8 9.3 9.8c5.4 0 9-3.8 9-9.1 0-.6-.1-1-.1-1.4H12z" />
    <path fill="#34A853" d="M3.8 7.6l3.2 2.3C7.8 7.6 9.7 6 12 6c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 8.4 2 5.2 4.2 3.8 7.6z" />
    <path fill="#FBBC05" d="M12 22c2.6 0 4.8-.9 6.4-2.6l-3-2.5c-.8.6-2 1.2-3.4 1.2-3.9 0-5.2-2.6-5.5-3.9l-3.1 2.4C4.7 19.9 8 22 12 22z" />
    <path fill="#4285F4" d="M21 12.5c0-.7-.1-1.2-.2-1.8H12v3.9h5.1c-.2 1-.8 2.1-1.8 2.8l3 2.5c1.8-1.7 2.7-4.2 2.7-7.4z" />
  </svg>
)

function BackButton({ onClick }) {
  return (
    <button type="button" className="kl-form-back" onClick={onClick}>
      ← Back
    </button>
  )
}

function LoginForm({ onRegister, googleStartUrl }) {
  const { loginParent } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await loginParent({ email, password })
    } catch (err) {
      setError(err?.message ?? 'Invalid email or password.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="kl-form-card">
      <h2 className="kl-form-title">Sign in</h2>
      <p className="kl-form-sub">Welcome back!</p>

      <form className="kl-form-fields" onSubmit={handleSubmit} noValidate>
        <div className="kl-field">
          <label htmlFor="login-email">Email</label>
          <input
            id="login-email"
            type="email"
            autoComplete="off"
            value={email}
            onChange={e => setEmail(e.target.value)}
            required
          />
        </div>

        <div className="kl-field">
          <label htmlFor="login-password">Password</label>
          <input
            id="login-password"
            type="password"
            autoComplete="off"
            value={password}
            onChange={e => setPassword(e.target.value)}
            required
          />
        </div>

        {error ? <p className="kl-form-error" role="alert">{error}</p> : null}

        <button type="submit" className="kl-form-submit" disabled={loading}>
          {loading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

      <div className="kl-or-divider"><span>or</span></div>

      <button
        type="button"
        className="kl-google-entry-btn"
        onClick={() => window.location.assign(googleStartUrl)}
      >
        {GOOGLE_SVG}
        Sign in with Google
      </button>

      <p className="kl-form-footer">
        Don&apos;t have an account?{' '}
        <button type="button" className="kl-form-link" onClick={onRegister}>
          Register
        </button>
      </p>
    </div>
  )
}

function RegisterForm({ onLogin }) {
  const { registerParent } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (password.length < 8) {
      setError('Password must be at least 8 characters.')
      return
    }
    if (password !== confirm) {
      setError('Passwords do not match.')
      return
    }

    setLoading(true)
    try {
      await registerParent({ email, password })
      // AuthProvider sets session after auto-login; ProtectedRoute handles redirect
    } catch (err) {
      setError(err?.message ?? 'Registration failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="kl-form-card">
      <BackButton onClick={onLogin} />
      <h2 className="kl-form-title">Create account</h2>
      <p className="kl-form-sub">Parents only — children are added from the dashboard.</p>

      <form className="kl-form-fields" onSubmit={handleSubmit} noValidate>
        <div className="kl-field">
          <label htmlFor="reg-email">Email</label>
          <input
            id="reg-email"
            type="email"
            autoComplete="off"
            value={email}
            onChange={e => setEmail(e.target.value)}
            required
          />
        </div>

        <div className="kl-field">
          <label htmlFor="reg-password">Password (min. 8 characters)</label>
          <input
            id="reg-password"
            type="password"
            autoComplete="off"
            value={password}
            onChange={e => setPassword(e.target.value)}
            required
          />
        </div>

        <div className="kl-field">
          <label htmlFor="reg-confirm">Confirm password</label>
          <input
            id="reg-confirm"
            type="password"
            autoComplete="off"
            value={confirm}
            onChange={e => setConfirm(e.target.value)}
            required
          />
        </div>

        {error ? <p className="kl-form-error" role="alert">{error}</p> : null}

        <button type="submit" className="kl-form-submit" disabled={loading}>
          {loading ? 'Creating account…' : 'Create account'}
        </button>
      </form>

      <p className="kl-form-footer">
        Already have an account?{' '}
        <button type="button" className="kl-form-link" onClick={onLogin}>
          Sign in
        </button>
      </p>
    </div>
  )
}

export default function LoginPage() {
  const { isAuthenticated, role } = useAuth()
  const location = useLocation()
  const [view, setView] = useState('login')

  if (isAuthenticated) {
    return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
  }

  const returnPath = location.state?.from?.pathname ?? '/parent'
  const googleStartUrl = getUnifiedGoogleStartUrl(returnPath)

  if (view === 'register') {
    return (
      <main className="kl-login-root">
        <Stars />
        <Logo />
        <RegisterForm onLogin={() => setView('login')} />
      </main>
    )
  }

  return (
    <main className="kl-login-root">
      <Stars />
      <Logo />
      <LoginForm onRegister={() => setView('register')} googleStartUrl={googleStartUrl} />
    </main>
  )
}

function Stars() {
  return (
    <div className="kl-login-bg" aria-hidden="true">
      {['⭐','🌟','✨','💫','⭐','✨','🌟','💫','⭐','🌟'].map((s, i) => (
        <span key={i} className={`kl-star kl-star-${i + 1}`}>{s}</span>
      ))}
    </div>
  )
}

function Logo() {
  return (
    <Link to="/" className="kl-logo" aria-label="KidsLearnAI home">
      <div className="kl-logo-rocket" aria-hidden="true">🚀</div>
      <div className="kl-logo-wordmark">
        <span className="kl-wm-kids">Kids</span><span className="kl-wm-learn">Learn</span><span className="kl-wm-ai">AI</span>
      </div>
      <p className="kl-logo-tagline">Learn smart. Grow fast. Have fun.</p>
    </Link>
  )
}
