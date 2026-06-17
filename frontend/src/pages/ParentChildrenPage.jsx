import { createPortal } from 'react-dom'
import { useEffect, useMemo, useState } from 'react'
import { createChildWithGmail, deleteChild, getChildren, updateChild } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'

const SORT_OPTIONS = [
  { value: 'name-asc',   label: 'Name A → Z' },
  { value: 'name-desc',  label: 'Name Z → A' },
  { value: 'grade-asc',  label: 'Grade low → high' },
  { value: 'grade-desc', label: 'Grade high → low' },
]

function sortChildren(list, sort) {
  return [...list].sort((a, b) => {
    if (sort === 'name-asc')   return a.name.localeCompare(b.name)
    if (sort === 'name-desc')  return b.name.localeCompare(a.name)
    if (sort === 'grade-asc')  return a.grade - b.grade
    if (sort === 'grade-desc') return b.grade - a.grade
    return 0
  })
}

function CreateChildModal({ onSave, onClose, isSaving, error }) {
  const [form, setForm] = useState({ gmailEmail: '', name: '', grade: '1' })

  function handleSubmit(e) {
    e.preventDefault()
    const email = form.gmailEmail.trim().toLowerCase()
    const name  = form.name.trim()
    const grade = Number(form.grade)
    if (!email || !name || grade < 1 || grade > 12) return
    onSave({ gmailEmail: email, name, grade })
  }

  return createPortal(
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" style={{ maxWidth: 460 }} onClick={(e) => e.stopPropagation()}>
        <div className="modal-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <h3 style={{ margin: 0 }}>Add child</h3>
          <button type="button" className="modal-close-btn" onClick={onClose}>✕</button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="create-gmail">Gmail address</label>
            <input
              id="create-gmail"
              className="input"
              type="email"
              placeholder="child@gmail.com"
              value={form.gmailEmail}
              onChange={(e) => setForm((f) => ({ ...f, gmailEmail: e.target.value }))}
              required
            />
            <span className="field-hint">Child will sign in with Google using this account.</span>
          </div>
          <div className="field" style={{ marginTop: '0.75rem' }}>
            <label htmlFor="create-name">Name</label>
            <input
              id="create-name"
              className="input"
              placeholder="Mia"
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              required
            />
          </div>
          <div className="field" style={{ marginTop: '0.75rem' }}>
            <label htmlFor="create-grade">Grade</label>
            <input
              id="create-grade"
              className="input"
              type="number"
              min="1"
              max="12"
              value={form.grade}
              onChange={(e) => setForm((f) => ({ ...f, grade: e.target.value }))}
              required
            />
          </div>
          {error ? <div className="alert" style={{ marginTop: '0.75rem' }}>{error}</div> : null}
          <div className="button-row modal-actions" style={{ marginTop: '1.25rem' }}>
            <button type="submit" className="button" disabled={isSaving}>
              {isSaving ? 'Adding...' : 'Add child'}
            </button>
            <button type="button" className="button-secondary" onClick={onClose}>Cancel</button>
          </div>
        </form>
      </div>
    </div>,
    document.body,
  )
}

function EditChildModal({ child, onSave, onClose, isSaving, error }) {
  const [form, setForm] = useState({ name: child.name, grade: String(child.grade) })

  function handleSubmit(e) {
    e.preventDefault()
    const name  = form.name.trim()
    const grade = Number(form.grade)
    if (!name || grade < 1 || grade > 12) return
    onSave({ name, grade })
  }

  return createPortal(
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" style={{ maxWidth: 400 }} onClick={(e) => e.stopPropagation()}>
        <div className="modal-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <h3 style={{ margin: 0 }}>Edit {child.name}</h3>
          <button type="button" className="modal-close-btn" onClick={onClose}>✕</button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="edit-name">Name</label>
            <input
              id="edit-name"
              className="input"
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              required
            />
          </div>
          <div className="field" style={{ marginTop: '0.75rem' }}>
            <label htmlFor="edit-grade">Grade</label>
            <input
              id="edit-grade"
              className="input"
              type="number"
              min="1"
              max="12"
              value={form.grade}
              onChange={(e) => setForm((f) => ({ ...f, grade: e.target.value }))}
              required
            />
          </div>
          {error ? <div className="alert" style={{ marginTop: '0.75rem' }}>{error}</div> : null}
          <div className="button-row modal-actions" style={{ marginTop: '1.25rem' }}>
            <button type="submit" className="button" disabled={isSaving}>
              {isSaving ? 'Saving...' : 'Save'}
            </button>
            <button type="button" className="button-secondary" onClick={onClose}>Cancel</button>
          </div>
        </form>
      </div>
    </div>,
    document.body,
  )
}

export default function ParentChildrenPage() {
  const { session } = useAuth()
  const [children, setChildren] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const [sortKey, setSortKey]       = useState('name-asc')
  const [filterName, setFilterName] = useState('')

  const [showCreate, setShowCreate]   = useState(false)
  const [isCreating, setIsCreating]   = useState(false)
  const [createError, setCreateError] = useState('')

  const [editTarget, setEditTarget]   = useState(null)
  const [isSavingEdit, setIsSavingEdit] = useState(false)
  const [editError, setEditError]     = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)
  const [isDeleting, setIsDeleting]     = useState(false)

  useEffect(() => {
    let mounted = true
    async function load() {
      if (!session?.accessToken) { setIsLoading(false); return }
      try {
        const data = await getChildren(session.accessToken)
        if (mounted) setChildren(data)
      } catch (err) {
        if (mounted) setError(err.message)
      } finally {
        if (mounted) setIsLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [session?.accessToken])

  async function handleCreate(payload) {
    setIsCreating(true)
    setCreateError('')
    try {
      const result = await createChildWithGmail(session.accessToken, payload)
      setChildren((cur) => [...cur, result.child])
      setShowCreate(false)
    } catch (err) {
      setCreateError(err.message)
    } finally {
      setIsCreating(false)
    }
  }

  async function handleSaveEdit(payload) {
    if (!editTarget) return
    setIsSavingEdit(true)
    setEditError('')
    try {
      const updated = await updateChild(session.accessToken, editTarget.id, payload)
      setChildren((cur) => cur.map((c) => c.id === updated.id ? updated : c))
      setEditTarget(null)
    } catch (err) {
      setEditError(err.message)
    } finally {
      setIsSavingEdit(false)
    }
  }

  async function handleDelete(child) {
    setIsDeleting(true)
    try {
      await deleteChild(session.accessToken, child.id)
      setChildren((cur) => cur.filter((c) => c.id !== child.id))
      setDeleteTarget(null)
    } catch (err) {
      setError(err.message)
      setDeleteTarget(null)
    } finally {
      setIsDeleting(false)
    }
  }

  const displayed = useMemo(() => {
    const filtered = filterName
      ? children.filter((c) => c.name.toLowerCase().includes(filterName.toLowerCase()))
      : children
    return sortChildren(filtered, sortKey)
  }, [children, filterName, sortKey])

  return (
    <section className="assignments-page">
      <div className="children-list-header">
        <div>
          <h2>Children</h2>
          <p>Manage children linked to your account.</p>
        </div>
        <button
          type="button"
          className="button"
          onClick={() => { setShowCreate(true); setCreateError('') }}
        >
          + Add child
        </button>
      </div>

      {error ? <div className="alert">{error}</div> : null}

      <div className="admin-filter-bar">
        <input
          className="admin-filter-input"
          type="text"
          placeholder="Search by name…"
          value={filterName}
          onChange={(e) => setFilterName(e.target.value)}
          aria-label="Filter by name"
        />
        <select
          className="admin-filter-input"
          value={sortKey}
          onChange={(e) => setSortKey(e.target.value)}
          aria-label="Sort children"
          style={{ flex: '0 1 200px' }}
        >
          {SORT_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
        <span className="badge" style={{ alignSelf: 'center', marginLeft: 'auto' }}>
          {displayed.length} child{displayed.length !== 1 ? 'ren' : ''}
        </span>
      </div>

      {isLoading ? <p className="children-empty">Loading children...</p> : null}

      {!isLoading && displayed.length === 0 ? (
        <p className="children-empty">
          {children.length === 0
            ? 'No children yet. Add the first child!'
            : 'No children match the search.'}
        </p>
      ) : null}

      {!isLoading && displayed.length > 0 ? (
        <div className="children-list">
          {displayed.map((child) => (
            <article key={child.id} className="child-row">
              <div>
                <div className="child-name">{child.name}</div>
                <div className="child-meta">
                  Grade {child.grade}
                  {child.gmailEmail ? (
                    <span style={{ marginLeft: '0.5em' }}>· {child.gmailEmail}</span>
                  ) : null}
                </div>
              </div>
              <div className="button-row child-actions">
                <button
                  type="button"
                  className="button-secondary"
                  style={{ padding: '0.35rem 0.75rem', fontSize: '0.8rem' }}
                  onClick={() => { setEditTarget(child); setEditError('') }}
                >
                  Edit
                </button>
                <button
                  type="button"
                  className="button-secondary"
                  style={{ padding: '0.35rem 0.75rem', fontSize: '0.8rem', borderColor: 'rgba(255,80,80,0.4)', color: 'var(--danger)' }}
                  onClick={() => setDeleteTarget(child)}
                >
                  Delete
                </button>
              </div>
            </article>
          ))}
        </div>
      ) : null}

      {showCreate ? (
        <CreateChildModal
          onSave={handleCreate}
          onClose={() => setShowCreate(false)}
          isSaving={isCreating}
          error={createError}
        />
      ) : null}

      {editTarget ? (
        <EditChildModal
          child={editTarget}
          onSave={handleSaveEdit}
          onClose={() => setEditTarget(null)}
          isSaving={isSavingEdit}
          error={editError}
        />
      ) : null}

      {deleteTarget ? (
        <div className="modal-overlay" onClick={() => setDeleteTarget(null)}>
          <div className="modal-card" style={{ maxWidth: 420 }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem' }}>
              <h3 style={{ margin: 0 }}>Delete child</h3>
              <button type="button" className="modal-close-btn" onClick={() => setDeleteTarget(null)}>✕</button>
            </div>
            <p style={{ margin: '0 0 1.5rem' }}>
              Are you sure you want to delete <strong>{deleteTarget.name}</strong>?
              All their assignments and results will also be removed. This cannot be undone.
            </p>
            <div className="button-row modal-actions">
              <button
                type="button"
                className="button"
                style={{ background: 'linear-gradient(135deg, #ff5757, #c0392b)' }}
                disabled={isDeleting}
                onClick={() => handleDelete(deleteTarget)}
              >
                {isDeleting ? 'Deleting...' : 'Delete'}
              </button>
              <button type="button" className="button-secondary" onClick={() => setDeleteTarget(null)}>
                Cancel
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  )
}
