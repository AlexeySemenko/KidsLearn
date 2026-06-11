import { useEffect, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getParentGoogleStartUrl, getChildGoogleStartUrl } from '../lib/api'

const GOOGLE_SVG = (
  <svg viewBox="0 0 24 24" focusable="false" aria-hidden="true">
    <path fill="#EA4335" d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.8-6-6.2s2.7-6.2 6-6.2c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 6.9 2 2.7 6.4 2.7 11.8s4.2 9.8 9.3 9.8c5.4 0 9-3.8 9-9.1 0-.6-.1-1-.1-1.4H12z" />
    <path fill="#34A853" d="M3.8 7.6l3.2 2.3C7.8 7.6 9.7 6 12 6c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 8.4 2 5.2 4.2 3.8 7.6z" />
    <path fill="#FBBC05" d="M12 22c2.6 0 4.8-.9 6.4-2.6l-3-2.5c-.8.6-2 1.2-3.4 1.2-3.9 0-5.2-2.6-5.5-3.9l-3.1 2.4C4.7 19.9 8 22 12 22z" />
    <path fill="#4285F4" d="M21 12.5c0-.7-.1-1.2-.2-1.8H12v3.9h5.1c-.2 1-.8 2.1-1.8 2.8l3 2.5c1.8-1.7 2.7-4.2 2.7-7.4z" />
  </svg>
)

const parentConfig = {
  title: 'Parent sign in',
  description: 'Manage children, lessons, assignments, reports, and AI lesson workflows.',
  submitLabel: 'Continue as parent',
  alternateLabel: 'Child access',
  alternateTo: '/login/child',
  alternateCopy: 'Use child login instead',
  fieldOne: {
    name: 'email',
    label: 'Email',
    type: 'email',
    placeholder: 'parent@kidslearn.local',
  },
  fieldTwo: {
    name: 'password',
    label: 'Password',
    type: 'password',
    placeholder: 'Enter your password',
  },
  hint: 'Uses POST /api/v1/auth/login and stores access + refresh tokens.',
}

function ChildLoginPage() {
  const { isAuthenticated, role } = useAuth()
  const location = useLocation()

  if (isAuthenticated) {
    return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
  }

  const targetPath = location.state?.from?.pathname ?? '/child'

  return (
    <main className="child-login-root">
      <div className="child-login-bubbles" aria-hidden="true">
        <span className="bubble b1">⭐</span>
        <span className="bubble b2">🎈</span>
        <span className="bubble b3">🌟</span>
        <span className="bubble b4">🎉</span>
        <span className="bubble b5">✨</span>
        <span className="bubble b6">🚀</span>
        <span className="bubble b7">🎯</span>
        <span className="bubble b8">📚</span>
      </div>

      <section className="child-login-card">
        <div className="child-login-mascot" aria-hidden="true">
          <svg viewBox="0 0 120 120" fill="none" xmlns="http://www.w3.org/2000/svg">
            <circle cx="60" cy="48" r="26" fill="#f4d35e" />
            <ellipse cx="60" cy="82" rx="28" ry="20" fill="#f4d35e" />
            <circle cx="51" cy="44" r="5" fill="#0f2745" />
            <circle cx="69" cy="44" r="5" fill="#0f2745" />
            <circle cx="53" cy="42" r="2" fill="white" />
            <circle cx="71" cy="42" r="2" fill="white" />
            <path d="M50 56 Q60 65 70 56" stroke="#0f2745" strokeWidth="2.5" strokeLinecap="round" fill="none" />
            <circle cx="34" cy="46" r="7" fill="#f4d35e" />
            <circle cx="86" cy="46" r="7" fill="#f4d35e" />
            <circle cx="34" cy="46" r="4" fill="#ffb703" />
            <circle cx="86" cy="46" r="4" fill="#ffb703" />
            <rect x="38" y="22" width="44" height="7" rx="3.5" fill="#1a3a5c" />
            <polygon points="60,13 82,25 38,25" fill="#1a3a5c" />
            <line x1="82" y1="25" x2="86" y2="36" stroke="#1a3a5c" strokeWidth="2" />
            <circle cx="86" cy="38" r="3.5" fill="#ffb703" />
          </svg>
        </div>

        <h1 className="child-login-title">Hi there! 👋</h1>
        <p className="child-login-subtitle">Sign in to see your assignments and start learning!</p>

        <button
          type="button"
          className="child-google-button"
          onClick={() => window.location.assign(getChildGoogleStartUrl(targetPath))}
        >
          <span className="child-google-icon">{GOOGLE_SVG}</span>
          <span>Sign in with Google</span>
        </button>

        <a className="child-login-parent-link" href="/login/parent">
          Are you a parent? Sign in here →
        </a>
      </section>
    </main>
  )
}

export default function LoginPage({ variant }) {
  if (variant === 'child') return <ChildLoginPage />

  const config = parentConfig
  const navigate = useNavigate()
  const location = useLocation()
  const { isAuthenticated, loginParent, role } = useAuth()
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [form, setForm] = useState({ email: '', password: '' })

  useEffect(() => {
    setError('')
    setForm({ email: '', password: '' })
  }, [variant])

  if (isAuthenticated) {
    return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
  }

  const targetPath = location.state?.from?.pathname ?? '/parent'

  async function handleSubmit(event) {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      await loginParent({
        email: (form.email ?? '').trim(),
        password: form.password ?? '',
      })

      navigate(targetPath, { replace: true })
    } catch (requestError) {
      if (requestError.status === 401) {
        setError('Credentials were rejected. Check the values and try again.')
      } else {
        setError(requestError.message)
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  function updateField(name, value) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  return (
    <main className="auth-root">
      <section className="auth-layout">
        <article className="auth-card">
          <div className="brand-kicker">KidsLearn Auth</div>
          <h1>{config.title}</h1>
          <p>{config.description}</p>

          <form className="auth-form" onSubmit={handleSubmit}>
            <div className="field">
              <label htmlFor={config.fieldOne.name}>{config.fieldOne.label}</label>
              <input
                id={config.fieldOne.name}
                className="input"
                type={config.fieldOne.type}
                placeholder={config.fieldOne.placeholder}
                value={form[config.fieldOne.name]}
                onChange={(event) => updateField(config.fieldOne.name, event.target.value)}
                autoComplete={variant === 'parent' ? 'username' : 'off'}
                required
              />
            </div>

            <div className="field">
              <label htmlFor={config.fieldTwo.name}>{config.fieldTwo.label}</label>
              <input
                id={config.fieldTwo.name}
                className="input"
                type={config.fieldTwo.type}
                placeholder={config.fieldTwo.placeholder}
                value={form[config.fieldTwo.name]}
                onChange={(event) => updateField(config.fieldTwo.name, event.target.value)}
                autoComplete={variant === 'parent' ? 'current-password' : 'off'}
                required
              />
            </div>

            {error ? <div className="alert" role="alert" aria-live="assertive">{error}</div> : null}

            <div className="button-row">
              <button type="submit" className="button" disabled={isSubmitting}>
                {isSubmitting ? 'Signing in...' : config.submitLabel}
              </button>
              <Link className="button-secondary inline-link" to={config.alternateTo}>
                {config.alternateLabel}
              </Link>
              <button
                type="button"
                className="button-secondary google-auth-button"
                onClick={() => window.location.assign(getParentGoogleStartUrl(targetPath))}
                disabled={isSubmitting}
              >
                <span className="google-auth-icon" aria-hidden="true">{GOOGLE_SVG}</span>
                <span className="google-auth-label">Sign in with Google</span>
              </button>
            </div>
          </form>
        </article>

        <aside className="auth-aside">
          <div className="brand-kicker">Implementation slice</div>
          <h2>Auth + session foundation</h2>
          <p>
            This is the first frontend delivery step from the plan: real login flows,
            persisted session state, and protected parent/child routes.
          </p>

          <div className="info-block">
            <strong>Current contract</strong>
            <span>{config.hint}</span>
          </div>

          <div className="info-block">
            <strong>Next planned UI</strong>
            <span>Parent dashboard, children management, lessons, and assignments.</span>
          </div>

          <div className="info-block">
            <strong>Need the other entry path?</strong>
            <span>
              <Link className="inline-link" to={config.alternateTo}>
                {config.alternateCopy}
              </Link>
            </span>
          </div>
        </aside>
      </section>
    </main>
  )
}
