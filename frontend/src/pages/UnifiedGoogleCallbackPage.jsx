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
  google_link_conflict: 'This email is already linked to another external account.',
  child_not_registered: 'This Google account is not linked to any student profile. Ask your parent to add you.',
}

function normalizeReturnPath(input) {
  if (!input || typeof input !== 'string') return '/parent'
  if (!input.startsWith('/') || input.startsWith('//')) return '/parent'
  return input
}

export default function UnifiedGoogleCallbackPage() {
  const [searchParams] = useSearchParams()
  const { finalizeUnifiedGoogleLogin } = useAuth()
  const [error, setError] = useState('')
  const hasStartedFinalize = useRef(false)
  const finalizeLoginRef = useRef(finalizeUnifiedGoogleLogin)

  const authCode = searchParams.get('authCode') ?? ''
  const callbackError = searchParams.get('error') ?? ''
  const returnPath = useMemo(
    () => normalizeReturnPath(searchParams.get('returnPath')),
    [searchParams],
  )

  useEffect(() => {
    finalizeLoginRef.current = finalizeUnifiedGoogleLogin
  }, [finalizeUnifiedGoogleLogin])

  useEffect(() => {
    if (hasStartedFinalize.current) return
    hasStartedFinalize.current = true
    let isMounted = true

    async function finalize() {
      if (callbackError) {
        const message = errorMessages[callbackError] ?? 'Google sign-in failed.'
        if (isMounted) setError(message)
        return
      }

      if (!authCode) {
        if (isMounted) setError('Missing Google authorization code.')
        return
      }

      try {
        const session = await finalizeLoginRef.current(authCode)
        if (!isMounted) return

        const role = session?.user?.role
        const target = role === 'Child' ? '/child' : returnPath
        window.location.replace(target)
      } catch (requestError) {
        if (!isMounted) return
        setError(requestError?.message ?? 'Failed to finalize Google sign-in.')
      }
    }

    finalize()
    return () => { isMounted = false }
  }, [authCode, callbackError, returnPath])

  return (
    <main className="auth-root">
      <section className="auth-layout">
        <article className="auth-card">
          <div className="brand-kicker">KidsLearn Auth</div>
          <h1>Completing Google sign in</h1>
          <p>Finalizing your session. You will be redirected automatically.</p>

          {error ? <div className="alert" role="alert">{error}</div> : null}
        </article>
      </section>
    </main>
  )
}
