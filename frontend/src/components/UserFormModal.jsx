import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'

const ROLES = ['Parent', 'Child']

export default function UserFormModal({ user, onSave, onClose, isSaving, error }) {
  const isEdit = Boolean(user?.id)

  const [email, setEmail] = useState(user?.email ?? '')
  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [role, setRole] = useState(user?.role === 'Admin' ? 'Parent' : (user?.role ?? 'Parent'))

  useEffect(() => {
    setEmail(user?.email ?? '')
    setDisplayName(user?.displayName ?? '')
    setRole(user?.role === 'Admin' ? 'Parent' : (user?.role ?? 'Parent'))
  }, [user])

  function handleSubmit(e) {
    e.preventDefault()
    if (isEdit) {
      onSave({ displayName: displayName || null, role })
    } else {
      onSave({ email: email.trim(), displayName: displayName.trim() || null, role })
    }
  }

  return createPortal(
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" style={{ marginBlock: 'auto', maxWidth: 480 }} onClick={(e) => e.stopPropagation()}>
        <div className="modal-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
          <h3 style={{ margin: 0 }}>{isEdit ? 'Edit user' : 'Add user'}</h3>
          <button type="button" className="modal-close-btn" onClick={onClose} aria-label="Close">✕</button>
        </div>

        <form className="user-form" onSubmit={handleSubmit}>
          {!isEdit ? (
            <div className="field">
              <label htmlFor="uf-email">Email</label>
              <input
                id="uf-email"
                className="input"
                type="email"
                placeholder="user@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoFocus
              />
            </div>
          ) : (
            <div className="field">
              <label>Email</label>
              <div className="input input--readonly">{user.email}</div>
            </div>
          )}

          <div className="field">
            <label htmlFor="uf-name">Display name</label>
            <input
              id="uf-name"
              className="input"
              type="text"
              placeholder="Full name (optional)"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
          </div>

          <div className="field">
            <label htmlFor="uf-role">Role</label>
            <select
              id="uf-role"
              className="input"
              value={role}
              onChange={(e) => setRole(e.target.value)}
            >
              {ROLES.map((r) => (
                <option key={r} value={r}>{r}</option>
              ))}
            </select>
          </div>

          {error ? <div className="alert" role="alert">{error}</div> : null}

          <div className="button-row modal-actions" style={{ marginTop: '0.5rem' }}>
            <button type="submit" className="button" disabled={isSaving}>
              {isSaving ? 'Saving...' : isEdit ? 'Save changes' : 'Add user & send invite'}
            </button>
            <button type="button" className="button-secondary" onClick={onClose} disabled={isSaving}>
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>,
    document.body
  )
}
