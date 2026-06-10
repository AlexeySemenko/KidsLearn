import { createContext, useContext, useEffect, useState } from 'react'
import {
  finalizeGoogleParentAuth,
  loginChild,
  loginParent,
  refreshSession,
  revokeSession,
} from '../lib/api'

const STORAGE_KEY = 'kidslearn.session'
const AuthContext = createContext(null)

function readStoredSession() {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

function persistSession(session) {
  if (typeof window === 'undefined') {
    return
  }

  if (!session) {
    window.localStorage.removeItem(STORAGE_KEY)
    return
  }

  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
}

function normalizeAuthResponse(response) {
  return {
    accessToken: response.accessToken,
    refreshToken: response.refreshToken,
    expiresInSeconds: response.expiresInSeconds,
    user: response.user,
  }
}

export function AuthProvider({ children }) {
  const [session, setSession] = useState(() => readStoredSession())
  const [isBootstrapping, setIsBootstrapping] = useState(true)

  useEffect(() => {
    let isMounted = true

    async function bootstrap() {
      const stored = readStoredSession()

      if (!stored?.refreshToken) {
        if (isMounted) {
          setIsBootstrapping(false)
        }
        return
      }

      try {
        const refreshed = normalizeAuthResponse(await refreshSession(stored.refreshToken))
        if (!isMounted) {
          return
        }

        persistSession(refreshed)
        setSession(refreshed)
      } catch {
        if (!isMounted) {
          return
        }

        persistSession(null)
        setSession(null)
      } finally {
        if (isMounted) {
          setIsBootstrapping(false)
        }
      }
    }

    bootstrap()

    return () => {
      isMounted = false
    }
  }, [])

  async function handleParentLogin(credentials) {
    const response = await loginParent(credentials)
    const nextSession = normalizeAuthResponse(response)
    persistSession(nextSession)
    setSession(nextSession)
    return nextSession
  }

  async function handleChildLogin(credentials) {
    const response = await loginChild(credentials)
    const nextSession = normalizeAuthResponse(response)
    persistSession(nextSession)
    setSession(nextSession)
    return nextSession
  }

  async function handleGoogleParentFinalize(authCode) {
    const response = await finalizeGoogleParentAuth(authCode)
    const nextSession = normalizeAuthResponse(response)
    persistSession(nextSession)
    setSession(nextSession)
    return nextSession
  }

  async function handleLogout() {
    const refreshToken = session?.refreshToken
    persistSession(null)
    setSession(null)

    if (!refreshToken) {
      return
    }

    try {
      await revokeSession(refreshToken)
    } catch {
      // Ignore revoke failures during client logout.
    }
  }

  async function refreshCurrentSession() {
    if (!session?.refreshToken) {
      throw new Error('No refresh token available.')
    }

    const response = await refreshSession(session.refreshToken)
    const nextSession = normalizeAuthResponse(response)
    persistSession(nextSession)
    setSession(nextSession)
    return nextSession
  }

  const value = {
    session,
    user: session?.user ?? null,
    role: session?.user?.role ?? null,
    isAuthenticated: Boolean(session?.accessToken),
    isBootstrapping,
    loginParent: handleParentLogin,
    loginChild: handleChildLogin,
    finalizeParentGoogleLogin: handleGoogleParentFinalize,
    logout: handleLogout,
    refreshSession: refreshCurrentSession,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)

  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.')
  }

  return context
}
