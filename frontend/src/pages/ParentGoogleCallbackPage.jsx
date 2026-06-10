import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

const errorMessages = {
  google_access_denied: 'Google sign-in was cancelled.',
  google_invalid_callback: 'Google sign-in callback is invalid.',
  google_invalid_state: 'Google sign-in session expired. Please try again.',
  google_not_configured: 'Google sign-in is not configured on the server.',
  google_exchange_failed: 'Google sign-in failed while exchanging tokens.',
  google_profile_invalid: 'Google account profile is missing required data.',
  google_email_not_verified: 'Google account email must be verified.',
  google_role_not_allowed: 'This account cannot use Google sign-in for parent access.',
  google_link_conflict: 'This email is already linked to another external account.',
}

function normalizeReturnPath(input) {
  if (!input || typeof input !== 'string') {
    return '/parent'
  }

  if (!input.startsWith('/') || input.startsWith('//')) {
    return '/parent'
  }

  return input
}

export default function ParentGoogleCallbackPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { finalizeParentGoogleLogin } = useAuth()
  const [error, setError] = useState('')

  const authCode = searchParams.get('authCode') ?? ''
  const callbackError = searchParams.get('error') ?? ''
  const returnPath = useMemo(
    () => normalizeReturnPath(searchParams.get('returnPath')),
    [searchParams],
  )

  useEffect(() => {
    let isMounted = true

    async function finalize() {
      if (callbackError) {
        const message = errorMessages[callbackError] ?? 'Google sign-in failed.'
        if (isMounted) {
          setError(message)
        }
        return
      }

      if (!authCode) {
        if (isMounted) {
          setError('Missing Google authorization code.')
        }
        return
      }

      try {
        const session = await finalizeParentGoogleLogin(authCode)
        if (!isMounted) {
          return
        }

        const target = session?.user?.role === 'Parent' ? returnPath : '/parent'
        navigate(target, { replace: true })
      } catch (requestError) {
        if (!isMounted) {
          return
        }

        setError(requestError?.message ?? 'Failed to finalize Google sign-in.')
      }
    }

    finalize()

    return () => {
      isMounted = false
    }
  }, [authCode, callbackError, finalizeParentGoogleLogin, navigate, returnPath])

  return (
    <main className="auth-root">
      <section className="auth-layout">
        <article className="auth-card">
          <div className="brand-kicker">KidsLearn Auth</div>
          <h1>Completing Google sign in</h1>
          <p>Finalizing your parent session. You will be redirected automatically.</p>

          {error ? <div className="alert" role="alert">{error}</div> : null}
        </article>
      </section>
    </main>
  )
}
