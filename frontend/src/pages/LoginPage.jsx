import { useEffect, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getParentGoogleStartUrl, getChildGoogleStartUrl } from '../lib/api'

const variants = {
  parent: {
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
  },
  child: {
    title: 'Child sign in',
    description: 'Open assignments, solve questions, and review results from a focused workspace.',
    submitLabel: 'Continue as child',
    alternateLabel: 'Parent access',
    alternateTo: '/login/parent',
    alternateCopy: 'Use parent login instead',
    fieldOne: {
      name: 'childId',
      label: 'Child ID',
      type: 'text',
      placeholder: '00000000-0000-0000-0000-000000000000',
    },
    fieldTwo: {
      name: 'accessCode',
      label: 'Access code',
      type: 'password',
      placeholder: 'Enter child access code',
    },
    hint: 'Uses POST /api/v1/auth/child-login and preserves the session on reload.',
  },
}

export default function LoginPage({ variant }) {
  const config = variants[variant]
  const navigate = useNavigate()
  const location = useLocation()
  const { isAuthenticated, loginParent, loginChild, role } = useAuth()
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [form, setForm] = useState(() => (
    variant === 'parent'
      ? { email: '', password: '' }
      : { childId: '', accessCode: '' }
  ))

  useEffect(() => {
    setError('')
    setForm(
      variant === 'parent'
        ? { email: '', password: '' }
        : { childId: '', accessCode: '' },
    )
  }, [variant])

  if (isAuthenticated) {
    return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
  }

  const targetPath = location.state?.from?.pathname ?? (variant === 'parent' ? '/parent' : '/child')

  async function handleSubmit(event) {
    event.preventDefault()
    setError('')
    setIsSubmitting(true)

    try {
      if (variant === 'parent') {
        await loginParent({
          email: (form.email ?? '').trim(),
          password: form.password ?? '',
        })
      } else {
        await loginChild({
          childId: (form.childId ?? '').trim(),
          accessCode: (form.accessCode ?? '').trim(),
        })
      }

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
              {variant === 'parent' ? (
                <button
                  type="button"
                  className="button-secondary google-auth-button"
                  onClick={() => window.location.assign(getParentGoogleStartUrl(targetPath))}
                  disabled={isSubmitting}
                >
                  <span className="google-auth-icon" aria-hidden="true">
                    <svg viewBox="0 0 24 24" focusable="false">
                      <path
                        fill="#EA4335"
                        d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.8-6-6.2s2.7-6.2 6-6.2c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 6.9 2 2.7 6.4 2.7 11.8s4.2 9.8 9.3 9.8c5.4 0 9-3.8 9-9.1 0-.6-.1-1-.1-1.4H12z"
                      />
                      <path
                        fill="#34A853"
                        d="M3.8 7.6l3.2 2.3C7.8 7.6 9.7 6 12 6c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 8.4 2 5.2 4.2 3.8 7.6z"
                      />
                      <path
                        fill="#FBBC05"
                        d="M12 22c2.6 0 4.8-.9 6.4-2.6l-3-2.5c-.8.6-2 1.2-3.4 1.2-3.9 0-5.2-2.6-5.5-3.9l-3.1 2.4C4.7 19.9 8 22 12 22z"
                      />
                      <path
                        fill="#4285F4"
                        d="M21 12.5c0-.7-.1-1.2-.2-1.8H12v3.9h5.1c-.2 1-.8 2.1-1.8 2.8l3 2.5c1.8-1.7 2.7-4.2 2.7-7.4z"
                      />
                    </svg>
                  </span>
                  <span className="google-auth-label">Sign in with Google</span>
                </button>
              ) : (
                <button
                  type="button"
                  className="button-secondary google-auth-button"
                  onClick={() => window.location.assign(getChildGoogleStartUrl(targetPath))}
                  disabled={isSubmitting}
                >
                  <span className="google-auth-icon" aria-hidden="true">
                    <svg viewBox="0 0 24 24" focusable="false">
                      <path
                        fill="#EA4335"
                        d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.8-6-6.2s2.7-6.2 6-6.2c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 6.9 2 2.7 6.4 2.7 11.8s4.2 9.8 9.3 9.8c5.4 0 9-3.8 9-9.1 0-.6-.1-1-.1-1.4H12z"
                      />
                      <path
                        fill="#34A853"
                        d="M3.8 7.6l3.2 2.3C7.8 7.6 9.7 6 12 6c1.9 0 3.1.8 3.9 1.5l2.7-2.6C16.9 2.8 14.7 2 12 2 8.4 2 5.2 4.2 3.8 7.6z"
                      />
                      <path
                        fill="#FBBC05"
                        d="M12 22c2.6 0 4.8-.9 6.4-2.6l-3-2.5c-.8.6-2 1.2-3.4 1.2-3.9 0-5.2-2.6-5.5-3.9l-3.1 2.4C4.7 19.9 8 22 12 22z"
                      />
                      <path
                        fill="#4285F4"
                        d="M21 12.5c0-.7-.1-1.2-.2-1.8H12v3.9h5.1c-.2 1-.8 2.1-1.8 2.8l3 2.5c1.8-1.7 2.7-4.2 2.7-7.4z"
                      />
                    </svg>
                  </span>
                  <span className="google-auth-label">Sign in with Google</span>
                </button>
              )}
            </div>
          </form>
        </article>

        <aside className="auth-aside">
          <div className="brand-kicker">Implementation slice</div>
          <h2>{variant === 'parent' ? 'Auth + session foundation' : 'Role-aware access path'}</h2>
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
            <span>
              {variant === 'parent'
                ? 'Parent dashboard, children management, lessons, and assignments.'
                : 'Child assignments list, solving workflow, and result detail.'}
            </span>
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
