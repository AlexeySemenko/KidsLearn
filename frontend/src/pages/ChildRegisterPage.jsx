import { useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

function Stars() {
  return (
    <div className="kl-login-bg" aria-hidden="true">
      {['⭐', '🌟', '✨', '💫', '⭐', '✨', '🌟', '💫', '⭐', '🌟'].map((s, i) => (
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

export default function ChildRegisterPage() {
  const { isAuthenticated, role, registerChild } = useAuth()
  const [searchParams] = useSearchParams()
  const emailFromUrl = searchParams.get('email') ?? ''

  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  if (isAuthenticated) {
    return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setError('')

    if (!emailFromUrl) {
      setError('Invalid registration link. Please use the link from your email.')
      return
    }
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
      await registerChild({ email: emailFromUrl, password })
    } catch (err) {
      setError(err?.message ?? 'Registration failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="kl-login-root">
      <Stars />
      <Logo />
      <div className="kl-form-card">
        <h2 className="kl-form-title">Create your account</h2>
        <p className="kl-form-sub">Your parent enrolled you — set a password to get started.</p>

        <form className="kl-form-fields" onSubmit={handleSubmit} noValidate>
          <div className="kl-field">
            <label htmlFor="cr-email">Email</label>
            <input
              id="cr-email"
              type="email"
              value={emailFromUrl}
              readOnly
              style={{ opacity: 0.7, cursor: 'not-allowed' }}
            />
          </div>

          <div className="kl-field">
            <label htmlFor="cr-password">Password (min. 8 characters)</label>
            <input
              id="cr-password"
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
            />
          </div>

          <div className="kl-field">
            <label htmlFor="cr-confirm">Confirm password</label>
            <input
              id="cr-confirm"
              type="password"
              autoComplete="new-password"
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
          Already set up?{' '}
          <Link to="/login" className="kl-form-link">Sign in</Link>
        </p>
      </div>
    </main>
  )
}
