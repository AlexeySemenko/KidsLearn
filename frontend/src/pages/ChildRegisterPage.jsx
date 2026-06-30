import { useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getChildRegisterGoogleStartUrl } from '../lib/api'

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
  const tokenFromUrl = searchParams.get('token') ?? ''

  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  if (isAuthenticated) {
    return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
  }

  if (!tokenFromUrl) {
    return (
      <main className="kl-login-root">
        <Stars />
        <Logo />
        <div className="kl-form-card">
          <h2 className="kl-form-title">Invalid link</h2>
          <p className="kl-form-sub">
            This registration link is invalid or has expired. Ask your parent to resend the invitation email.
          </p>
          <p className="kl-form-footer">
            Already set up? <Link to="/login" className="kl-form-link">Sign in</Link>
          </p>
        </div>
      </main>
    )
  }

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
      await registerChild({ token: tokenFromUrl, password })
    } catch (err) {
      setError(err?.message ?? 'Registration failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  function handleGoogleSignIn() {
    window.location.href = getChildRegisterGoogleStartUrl(tokenFromUrl)
  }

  return (
    <main className="kl-login-root">
      <Stars />
      <Logo />
      <div className="kl-form-card">
        <h2 className="kl-form-title">Create your account</h2>
        <p className="kl-form-sub">Your parent enrolled you — choose how to set up your account.</p>

        <button
          type="button"
          className="kl-google-btn"
          onClick={handleGoogleSignIn}
          disabled={loading}
        >
          <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true">
            <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/>
            <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/>
            <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/>
            <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/>
            <path fill="none" d="M0 0h48v48H0z"/>
          </svg>
          Continue with Google
        </button>

        <div className="kl-divider">
          <span>or set a password</span>
        </div>

        <form className="kl-form-fields" onSubmit={handleSubmit} noValidate>
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
