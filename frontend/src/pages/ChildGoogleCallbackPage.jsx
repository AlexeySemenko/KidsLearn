import { useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

const errorMessages = {
  google_access_denied: 'Google sign-in was cancelled.',
  google_invalid_callback: 'Google sign-in callback is invalid.',
  google_invalid_state: 'Google sign-in session expired. Please try again.',
  google_not_configured: 'Google sign-in is not configured on the server.',
  google_exchange_failed: 'Google sign-in failed while exchanging tokens.',
  google_profile_invalid: 'Google account profile is missing required data.',
  google_email_not_verified: 'Google account email must be verified.',
  child_not_registered: 'You are not registered in KidsLearn. Ask your parent to add you.',
  registration_token_invalid: 'This registration link is invalid or has expired. Ask your parent to resend the invitation.',
  child_already_registered: 'This account is already set up. Please sign in.',
  google_email_mismatch: 'The Google account email does not match the email your parent enrolled. Use the correct Google account.',
}

function normalizeReturnPath(input) {
  if (!input || typeof input !== 'string') {
    return '/child'
  }

  if (!input.startsWith('/') || input.startsWith('//')) {
    return '/child'
  }

  return input
}

export default function ChildGoogleCallbackPage() {
  const [searchParams] = useSearchParams()
  const { finalizeChildGoogleLogin } = useAuth()
  const [error, setError] = useState('')
  const hasStartedFinalize = useRef(false)
  const finalizeLoginRef = useRef(finalizeChildGoogleLogin)

  const authCode = searchParams.get('authCode') ?? ''
  const callbackError = searchParams.get('error') ?? ''
  const returnPath = useMemo(
    () => normalizeReturnPath(searchParams.get('returnPath')),
    [searchParams],
  )

  useEffect(() => {
    finalizeLoginRef.current = finalizeChildGoogleLogin
  }, [finalizeChildGoogleLogin])

  useEffect(() => {
    if (hasStartedFinalize.current) {
      return
    }

    hasStartedFinalize.current = true
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
        const session = await finalizeLoginRef.current(authCode)
        if (!isMounted) {
          return
        }

        const target = session?.user?.role === 'Child' ? returnPath : '/child'
        window.location.replace(target)
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
  }, [authCode, callbackError, returnPath])

  return (
    <main className="auth-root">
      <section className="auth-layout">
        <article className="auth-card">
          <div className="brand-kicker">KidsLearn Auth</div>
          <h1>Completing Google sign in</h1>

          {error && (
            <p style={{ color: 'var(--error)', marginTop: '1rem', marginBottom: 0 }}>
              {error}
            </p>
          )}

          {!error && (
            <p style={{ marginTop: '1rem', marginBottom: 0, color: 'var(--muted)' }}>
              Please wait...
            </p>
          )}
        </article>
      </section>
    </main>
  )
}
