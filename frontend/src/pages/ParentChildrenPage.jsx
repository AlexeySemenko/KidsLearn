import { useEffect, useState } from 'react'
import { createChild, deleteChild, getChildren, resetChildAccessCode, updateChild } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'

export default function ParentChildrenPage() {
  const { session } = useAuth()
  const [children, setChildren] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [freshAccessCode, setFreshAccessCode] = useState('')
  const [form, setForm] = useState({ name: '', grade: '1', accessCode: '' })
  const [editingChildId, setEditingChildId] = useState(null)
  const [editForm, setEditForm] = useState({ name: '', grade: '1', accessCode: '' })
  const [isSavingEdit, setIsSavingEdit] = useState(false)

  useEffect(() => {
    let isMounted = true

    async function loadChildren() {
      if (!session?.accessToken) {
        if (isMounted) {
          setIsLoading(false)
        }
        return
      }

      try {
        setError('')
        const response = await getChildren(session.accessToken)
        if (isMounted) {
          setChildren(response)
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

    loadChildren()

    return () => {
      isMounted = false
    }
  }, [session?.accessToken])

  function updateField(name, value) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  function startEditing(child) {
    setError('')
    setFreshAccessCode('')
    setEditingChildId(child.id)
    setEditForm({
      name: child.name,
      grade: String(child.grade),
      accessCode: '',
    })
  }

  function cancelEditing() {
    setEditingChildId(null)
    setEditForm({ name: '', grade: '1', accessCode: '' })
  }

  function updateEditField(name, value) {
    setEditForm((current) => ({ ...current, [name]: value }))
  }

  async function handleCreateChild(event) {
    event.preventDefault()
    if (!session?.accessToken) {
      return
    }

    setIsSubmitting(true)
    setError('')
    setFreshAccessCode('')

    try {
      const payload = {
        name: form.name.trim(),
        grade: Number(form.grade),
        accessCode: form.accessCode.trim() || null,
      }
      const response = await createChild(session.accessToken, payload)
      setChildren((current) => [...current, response.child])
      setFreshAccessCode(response.accessCode)
      setForm({ name: '', grade: '1', accessCode: '' })
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleResetAccessCode(childId) {
    if (!session?.accessToken) {
      return
    }

    setError('')
    setFreshAccessCode('')

    try {
      const response = await resetChildAccessCode(session.accessToken, childId)
      setFreshAccessCode(`Child ${response.childId}: ${response.accessCode}`)
    } catch (requestError) {
      setError(requestError.message)
    }
  }

  async function handleSaveChild(childId) {
    if (!session?.accessToken) {
      return
    }

    setError('')
    setFreshAccessCode('')
    setIsSavingEdit(true)

    try {
      const response = await updateChild(session.accessToken, childId, {
        name: editForm.name.trim(),
        grade: Number(editForm.grade),
        accessCode: editForm.accessCode.trim() || null,
      })

      setChildren((current) => current.map((child) => (
        child.id === childId ? response : child
      )))
      cancelEditing()
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsSavingEdit(false)
    }
  }

  async function handleDeleteChild(childId) {
    if (!session?.accessToken) {
      return
    }

    setError('')
    setFreshAccessCode('')

    try {
      await deleteChild(session.accessToken, childId)
      setChildren((current) => current.filter((child) => child.id !== childId))
    } catch (requestError) {
      setError(requestError.message)
    }
  }

  return (
    <section className="panel-grid">
      <article className="hero-card children-hero">
        <div className="brand-kicker">Epic 3.2</div>
        <h2>Children management is now connected to the API.</h2>
        <p>
          This first parent data screen loads real children, creates new child accounts,
          reveals issued access codes, and supports reset/delete actions.
        </p>
        <div className="badge-row">
          <span className="badge">GET /children</span>
          <span className="badge">POST /children</span>
          <span className="badge">PATCH /children/{'{id}'}</span>
          <span className="badge">POST /access-code/reset</span>
          <span className="badge">DELETE /children/{'{id}'}</span>
        </div>
      </article>

      <article className="panel-card children-form-card">
        <h3>Create child</h3>
        <p>Add a child profile and optionally set a custom access code.</p>

        <form className="auth-form compact-form" onSubmit={handleCreateChild}>
          <div className="field">
            <label htmlFor="child-name">Name</label>
            <input
              id="child-name"
              className="input"
              value={form.name}
              onChange={(event) => updateField('name', event.target.value)}
              placeholder="Mia"
              required
            />
          </div>

          <div className="field">
            <label htmlFor="child-grade">Grade</label>
            <input
              id="child-grade"
              className="input"
              type="number"
              min="1"
              max="12"
              value={form.grade}
              onChange={(event) => updateField('grade', event.target.value)}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="child-access-code">Custom access code</label>
            <input
              id="child-access-code"
              className="input"
              value={form.accessCode}
              onChange={(event) => updateField('accessCode', event.target.value)}
              placeholder="Optional, min 4 chars"
            />
          </div>

          <button type="submit" className="button" disabled={isSubmitting}>
            {isSubmitting ? 'Creating...' : 'Create child'}
          </button>
        </form>

        {freshAccessCode ? (
          <div className="info-block success-block">
            <strong>Issued access code</strong>
            <span>{freshAccessCode}</span>
          </div>
        ) : null}

        {error ? <div className="alert children-alert">{error}</div> : null}
      </article>

      <article className="children-list-card">
        <div className="children-list-header">
          <div>
            <h3>Children</h3>
            <p>Real data from the authenticated parent session.</p>
          </div>
          <span className="badge">{children.length} records</span>
        </div>

        {isLoading ? <p className="children-empty">Loading children...</p> : null}

        {!isLoading && children.length === 0 ? (
          <p className="children-empty">No children yet. Create the first child from the panel on the right.</p>
        ) : null}

        {!isLoading && children.length > 0 ? (
          <div className="children-list">
            {children.map((child) => (
              <article key={child.id} className="child-row">
                {editingChildId === child.id ? (
                  <>
                    <div className="child-edit-grid">
                      <div className="field">
                        <label htmlFor={`edit-name-${child.id}`}>Name</label>
                        <input
                          id={`edit-name-${child.id}`}
                          className="input"
                          value={editForm.name}
                          onChange={(event) => updateEditField('name', event.target.value)}
                        />
                      </div>
                      <div className="field">
                        <label htmlFor={`edit-grade-${child.id}`}>Grade</label>
                        <input
                          id={`edit-grade-${child.id}`}
                          className="input"
                          type="number"
                          min="1"
                          max="12"
                          value={editForm.grade}
                          onChange={(event) => updateEditField('grade', event.target.value)}
                        />
                      </div>
                      <div className="field child-access-field">
                        <label htmlFor={`edit-code-${child.id}`}>New access code</label>
                        <input
                          id={`edit-code-${child.id}`}
                          className="input"
                          value={editForm.accessCode}
                          onChange={(event) => updateEditField('accessCode', event.target.value)}
                          placeholder="Optional, min 4 chars"
                        />
                      </div>
                    </div>
                    <div className="button-row child-actions">
                      <button
                        type="button"
                        className="button"
                        disabled={isSavingEdit}
                        onClick={() => handleSaveChild(child.id)}
                      >
                        {isSavingEdit ? 'Saving...' : 'Save'}
                      </button>
                      <button type="button" className="button-secondary" onClick={cancelEditing}>
                        Cancel
                      </button>
                    </div>
                  </>
                ) : (
                  <>
                    <div>
                      <div className="child-name">{child.name}</div>
                      <div className="child-meta">Grade {child.grade} · {child.id}</div>
                    </div>
                    <div className="button-row child-actions">
                      <button
                        type="button"
                        className="button-secondary"
                        onClick={() => startEditing(child)}
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        className="button-secondary"
                        onClick={() => handleResetAccessCode(child.id)}
                      >
                        Reset code
                      </button>
                      <button
                        type="button"
                        className="button-secondary danger-button"
                        onClick={() => handleDeleteChild(child.id)}
                      >
                        Delete
                      </button>
                    </div>
                  </>
                )}
              </article>
            ))}
          </div>
        ) : null}
      </article>
    </section>
  )
}
