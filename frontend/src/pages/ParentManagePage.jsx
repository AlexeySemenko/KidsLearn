import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { useAuth } from '../auth/AuthProvider'
import { getLinkedParents, linkParentAccount, unlinkParentAccount } from '../lib/api'

function formatLinkedAt(value) {
  if (!value) return 'Unknown'
  try { return new Date(value).toLocaleString() } catch { return value }
}

function LinkParentModal({ onLink, onClose, isSubmitting, error }) {
  const [email, setEmail] = useState('')

  function handleSubmit(e) {
    e.preventDefault()
    onLink(email.trim().toLowerCase())
  }

  return createPortal(
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" style={{ maxWidth: 440, marginBlock: 'auto' }} onClick={(e) => e.stopPropagation()}>
        <div className="modal-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
          <h3 style={{ margin: 0 }}>Link parent account</h3>
          <button type="button" className="modal-close-btn" onClick={onClose} aria-label="Close">✕</button>
        </div>

        <p style={{ margin: '0 0 1.25rem', color: 'var(--muted)', fontSize: '0.9rem' }}>
          The other parent must already have an account in KidsLearnAI. Both accounts will share the same children, lessons, assignments, and reports.
        </p>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div className="field">
            <label htmlFor="link-parent-email">Parent email</label>
            <input
              id="link-parent-email"
              className="input"
              type="email"
              placeholder="other.parent@gmail.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoFocus
            />
          </div>

          {error ? <div className="alert" role="alert">{error}</div> : null}

          <div className="button-row modal-actions" style={{ marginTop: '0.25rem' }}>
            <button type="submit" className="button" disabled={isSubmitting}>
              {isSubmitting ? 'Linking...' : 'Link account'}
            </button>
            <button type="button" className="button-secondary" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>,
    document.body
  )
}

export default function ParentManagePage() {
  const { session } = useAuth()
  const [linkedParents, setLinkedParents] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [modalError, setModalError] = useState('')
  const [unlinkingId, setUnlinkingId] = useState(null)
  const [toast, setToast] = useState(null)

  function showToast(message, type = 'success') {
    setToast({ message, type })
    setTimeout(() => setToast(null), 5000)
  }

  useEffect(() => {
    let mounted = true
    async function load() {
      if (!session?.accessToken) { setIsLoading(false); return }
      try {
        const data = await getLinkedParents(session.accessToken)
        if (mounted) setLinkedParents(data)
      } catch (err) {
        if (mounted) setError(err.message)
      } finally {
        if (mounted) setIsLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [session?.accessToken])

  async function handleLink(email) {
    setIsSubmitting(true)
    setModalError('')
    try {
      const result = await linkParentAccount(session.accessToken, email)
      setLinkedParents((cur) =>
        cur.some((p) => p.parentId === result.linkedParent.parentId)
          ? cur
          : [...cur, result.linkedParent].sort((a, b) => a.email.localeCompare(b.email))
      )
      setIsModalOpen(false)
      if (result.emailSent) {
        showToast(`${result.linkedParent.email} linked. Notification email sent.`)
      } else {
        showToast(`${result.linkedParent.email} linked. Notification email could not be sent.`, 'warn')
      }
    } catch (err) {
      setModalError(err.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleUnlink(parentId) {
    const target = linkedParents.find((p) => p.parentId === parentId)
    if (!target) return
    if (!window.confirm(`Unlink ${target.email}? They will stop sharing this workspace.`)) return

    setUnlinkingId(parentId)
    try {
      await unlinkParentAccount(session.accessToken, parentId)
      setLinkedParents((cur) => cur.filter((p) => p.parentId !== parentId))
    } catch (err) {
      setError(err.message)
    } finally {
      setUnlinkingId(null)
    }
  }

  return (
    <section className="admin-users-page">
      <div className="children-list-header">
        <div>
          <h2>Linked parents</h2>
          <p>Parents sharing this workspace see the same children, lessons, assignments, and reports.</p>
        </div>
        <button
          type="button"
          className="button"
          onClick={() => { setIsModalOpen(true); setModalError('') }}
        >
          + Link parent
        </button>
      </div>

      {error ? <div className="alert">{error}</div> : null}

      {isLoading ? (
        <p className="children-empty">Loading...</p>
      ) : linkedParents.length === 0 ? (
        <p className="children-empty">No linked parents yet.</p>
      ) : (
        <div className="children-list">
          {linkedParents.map((parent) => (
            <article key={parent.parentId} className="assignment-row" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem' }}>
              <div className="assignment-copy">
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                  <div className="child-name">{parent.email}</div>
                  <span className="assignment-status-pill status-success">Linked</span>
                </div>
                <div className="child-meta">Linked {formatLinkedAt(parent.linkedAt)}</div>
              </div>
              <button
                type="button"
                className="button-secondary"
                style={{ padding: '0.3rem 0.65rem', fontSize: '0.78rem', borderColor: 'rgba(255,80,80,0.4)', color: 'var(--danger)', flexShrink: 0 }}
                disabled={unlinkingId === parent.parentId}
                onClick={() => handleUnlink(parent.parentId)}
              >
                {unlinkingId === parent.parentId ? 'Unlinking…' : 'Unlink'}
              </button>
            </article>
          ))}
        </div>
      )}

      {toast ? (
        <div className={`admin-toast admin-toast--${toast.type}`} role="status">
          {toast.message}
          <button type="button" className="admin-toast-close" onClick={() => setToast(null)} aria-label="Dismiss">✕</button>
        </div>
      ) : null}

      {isModalOpen ? (
        <LinkParentModal
          onLink={handleLink}
          onClose={() => setIsModalOpen(false)}
          isSubmitting={isSubmitting}
          error={modalError}
        />
      ) : null}
    </section>
  )
}
