import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { getLinkedParents, linkParentAccount, unlinkParentAccount } from '../lib/api'

function formatLinkedAt(value) {
  if (!value) {
    return 'Unknown'
  }

  try {
    return new Date(value).toLocaleString()
  } catch {
    return value
  }
}

export default function ParentManagePage() {
  const { session } = useAuth()
  const [email, setEmail] = useState('')
  const [linkedParents, setLinkedParents] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [unlinkingParentId, setUnlinkingParentId] = useState(null)
  const [error, setError] = useState('')
  const [statusMessage, setStatusMessage] = useState('')

  useEffect(() => {
    let isMounted = true

    async function loadLinkedParents() {
      if (!session?.accessToken) {
        if (isMounted) {
          setIsLoading(false)
        }
        return
      }

      try {
        setError('')
        const response = await getLinkedParents(session.accessToken)
        if (isMounted) {
          setLinkedParents(response)
        }
      } catch (requestError) {
        if (isMounted) {
          setError(requestError.message)
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadLinkedParents()

    return () => {
      isMounted = false
    }
  }, [session?.accessToken])

  async function handleLinkParent(event) {
    event.preventDefault()
    if (!session?.accessToken) {
      return
    }

    const trimmedEmail = email.trim().toLowerCase()
    if (!trimmedEmail) {
      setError('Parent email is required.')
      setStatusMessage('')
      return
    }

    setIsSubmitting(true)
    setError('')
    setStatusMessage('')

    try {
      const linked = await linkParentAccount(session.accessToken, trimmedEmail)
      setLinkedParents((current) => {
        if (current.some((item) => item.parentId === linked.parentId)) {
          return current
        }

        return [...current, linked].sort((a, b) => a.email.localeCompare(b.email))
      })
      setStatusMessage(`${linked.email} is now linked to this parent account.`)
      setEmail('')
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleUnlinkParent(linkedParentId) {
    if (!session?.accessToken) {
      return
    }

    const linkedParent = linkedParents.find((item) => item.parentId === linkedParentId)
    if (!linkedParent) {
      return
    }

    const isConfirmed = window.confirm(`Unlink ${linkedParent.email}? They will stop sharing children, lessons, assignments, and reports.`)
    if (!isConfirmed) {
      return
    }

    setUnlinkingParentId(linkedParentId)
    setError('')
    setStatusMessage('')

    try {
      await unlinkParentAccount(session.accessToken, linkedParentId)
      setLinkedParents((current) => current.filter((item) => item.parentId !== linkedParentId))
      setStatusMessage(`${linkedParent.email} has been unlinked.`)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setUnlinkingParentId(null)
    }
  }

  return (
    <section className="panel-grid">
      <article className="hero-card children-hero">
        <div className="brand-kicker">Parent management</div>
        <h2>Link another parent account</h2>
        <p>
          Linking another parent means both parent logins share the same workspace.
          You will both see and manage the same children, lessons, assignments, and reports together.
        </p>
      </article>

      <article className="panel-card children-form-card">
        <h3>Link parent by email</h3>
        <p>Enter another parent account email to share this account workspace.</p>

        <form className="children-form" onSubmit={handleLinkParent}>
          <div className="field">
            <label htmlFor="linked-parent-email">Parent email</label>
            <input
              id="linked-parent-email"
              className="input"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="other.parent@gmail.com"
              required
            />
          </div>

          <div className="button-row">
            <button type="submit" className="button" disabled={isSubmitting}>
              {isSubmitting ? 'Linking...' : 'Link parent account'}
            </button>
          </div>
        </form>
      </article>

      <article className="children-list-card" style={{ gridColumn: 'span 12' }}>
        <div className="children-list-header">
          <div>
            <h3>Linked parents</h3>
            <p>Linked parents share this same workspace.</p>
          </div>
          <span className="badge">{linkedParents.length} linked</span>
        </div>

        {isLoading ? <p className="children-empty">Loading linked parents...</p> : null}
        {!isLoading && linkedParents.length === 0 ? <p className="children-empty">No linked parents yet.</p> : null}
        {error ? <div className="alert assignments-alert">{error}</div> : null}
        {statusMessage ? <div className="info-block success-block assignments-status-block">{statusMessage}</div> : null}

        {!isLoading && linkedParents.length > 0 ? (
          <div className="children-list">
            {linkedParents.map((parent) => (
              <article key={parent.parentId} className="assignment-row">
                <div className="assignment-copy">
                  <div className="assignment-topline">
                    <div className="child-name">{parent.email}</div>
                    <span className="assignment-status-pill status-success">Linked</span>
                  </div>
                  <div className="child-meta">Linked at {formatLinkedAt(parent.linkedAt)}</div>
                </div>

                <div className="button-row">
                  <button
                    type="button"
                    className="button-secondary"
                    disabled={unlinkingParentId === parent.parentId}
                    onClick={() => handleUnlinkParent(parent.parentId)}
                  >
                    {unlinkingParentId === parent.parentId ? 'Unlinking...' : 'Unlink'}
                  </button>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </article>
    </section>
  )
}
