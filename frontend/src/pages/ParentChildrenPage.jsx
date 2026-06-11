import { useEffect, useState } from 'react'
import { createChild, createChildWithGmail, deleteChild, getChildren, resetChildAccessCode, updateChild } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'

function validateChildPayload({ name, grade, accessCode }, { requireAccessCode = false } = {}) {
  const trimmedName = name.trim()
  const normalizedCode = accessCode.trim()
  const parsedGrade = Number(grade)

  if (!trimmedName) {
    return 'Name is required.'
  }

  if (!Number.isInteger(parsedGrade) || parsedGrade < 1 || parsedGrade > 12) {
    return 'Grade must be a whole number between 1 and 12.'
  }

  if ((requireAccessCode || normalizedCode) && normalizedCode.length < 4) {
    return 'Access code must contain at least 4 characters.'
  }

  return null
}

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
  const [isDeletingChildId, setIsDeletingChildId] = useState(null)
  const [isResettingChildId, setIsResettingChildId] = useState(null)
  const [statusMessage, setStatusMessage] = useState('')
  const [gmailForm, setGmailForm] = useState({ gmailEmail: '', name: '', grade: '1' })
  const [isSubmittingGmail, setIsSubmittingGmail] = useState(false)
  const [gmailTab, setGmailTab] = useState(false)

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
        setStatusMessage('')
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
    setStatusMessage('')
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

    const validationError = validateChildPayload(form)
    if (validationError) {
      setError(validationError)
      setStatusMessage('')
      setFreshAccessCode('')
      return
    }

    setIsSubmitting(true)
    setError('')
    setFreshAccessCode('')
    setStatusMessage('')

    try {
      const payload = {
        name: form.name.trim(),
        grade: Number(form.grade),
        accessCode: form.accessCode.trim() || null,
      }
      const response = await createChild(session.accessToken, payload)
      setChildren((current) => [...current, response.child])
      setFreshAccessCode(response.accessCode)
      setStatusMessage(`${response.child.name} was created successfully.`)
      setForm({ name: '', grade: '1', accessCode: '' })
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  function updateGmailField(name, value) {
    setGmailForm((current) => ({ ...current, [name]: value }))
  }

  async function handleCreateChildWithGmail(event) {
    event.preventDefault()
    if (!session?.accessToken) {
      return
    }

    const trimmedEmail = gmailForm.gmailEmail.trim().toLowerCase()
    const trimmedName = gmailForm.name.trim()
    const parsedGrade = Number(gmailForm.grade)

    if (!trimmedEmail.endsWith('@gmail.com')) {
      setError('Please enter a valid Gmail address (must end with @gmail.com).')
      setStatusMessage('')
      return
    }

    if (!trimmedName) {
      setError('Name is required.')
      setStatusMessage('')
      return
    }

    if (!Number.isInteger(parsedGrade) || parsedGrade < 1 || parsedGrade > 12) {
      setError('Grade must be a whole number between 1 and 12.')
      setStatusMessage('')
      return
    }

    setIsSubmittingGmail(true)
    setError('')
    setStatusMessage('')

    try {
      const payload = {
        gmailEmail: trimmedEmail,
        name: trimmedName,
        grade: parsedGrade,
      }
      const response = await createChildWithGmail(session.accessToken, payload)
      setChildren((current) => [...current, response.child])
      setStatusMessage(`${response.child.name} was created with Gmail SSO. They can now sign in using Google.`)
      setGmailForm({ gmailEmail: '', name: '', grade: '1' })
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsSubmittingGmail(false)
    }
  }

  async function handleResetAccessCode(childId) {
    if (!session?.accessToken) {
      return
    }

    setError('')
    setFreshAccessCode('')
    setStatusMessage('')
    setIsResettingChildId(childId)

    try {
      const response = await resetChildAccessCode(session.accessToken, childId)
      setFreshAccessCode(`Child ${response.childId}: ${response.accessCode}`)
      setStatusMessage('Access code was reset successfully.')
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsResettingChildId(null)
    }
  }

  async function handleSaveChild(childId) {
    if (!session?.accessToken) {
      return
    }

    const validationError = validateChildPayload(editForm)
    if (validationError) {
      setError(validationError)
      setStatusMessage('')
      setFreshAccessCode('')
      return
    }

    setError('')
    setFreshAccessCode('')
    setStatusMessage('')
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
      setStatusMessage(`${response.name} was updated successfully.`)
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

    const child = children.find((item) => item.id === childId)
    if (!child) {
      return
    }

    const isConfirmed = window.confirm(`Delete ${child.name}? This action cannot be undone.`)
    if (!isConfirmed) {
      return
    }

    setError('')
    setFreshAccessCode('')
    setStatusMessage('')
    setIsDeletingChildId(childId)

    try {
      await deleteChild(session.accessToken, childId)
      setChildren((current) => current.filter((child) => child.id !== childId))
      setStatusMessage(`${child.name} was deleted.`)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsDeletingChildId(null)
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
        <div style={{ marginBottom: '1rem' }}>
          <div className="button-row" style={{ gap: '0.5rem' }}>
            <button
              type="button"
              className={gmailTab ? 'button-secondary' : 'button-secondary' + (gmailTab ? '' : ' active')}
              onClick={() => setGmailTab(false)}
              style={{ backgroundColor: gmailTab ? 'transparent' : 'var(--accent)', color: gmailTab ? 'var(--text)' : 'var(--bg)' }}
            >
              Access Code
            </button>
            <button
              type="button"
              className={gmailTab ? 'button-secondary' + ' active' : 'button-secondary'}
              onClick={() => setGmailTab(true)}
              style={{ backgroundColor: gmailTab ? 'var(--accent)' : 'transparent', color: gmailTab ? 'var(--bg)' : 'var(--text)' }}
            >
              Gmail SSO
            </button>
          </div>
        </div>

        {!gmailTab ? (
          <>
            <h3>Create child with access code</h3>
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
          </>
        ) : (
          <>
            <h3>Create child with Gmail SSO</h3>
            <p>Add a child with Gmail address. They can sign in using Google.</p>

            <form className="auth-form compact-form" onSubmit={handleCreateChildWithGmail}>
              <div className="field">
                <label htmlFor="child-gmail">Gmail address</label>
                <input
                  id="child-gmail"
                  className="input"
                  type="email"
                  value={gmailForm.gmailEmail}
                  onChange={(event) => updateGmailField('gmailEmail', event.target.value)}
                  placeholder="child@gmail.com"
                  required
                />
              </div>

              <div className="field">
                <label htmlFor="child-gmail-name">Name</label>
                <input
                  id="child-gmail-name"
                  className="input"
                  value={gmailForm.name}
                  onChange={(event) => updateGmailField('name', event.target.value)}
                  placeholder="Mia"
                  required
                />
              </div>

              <div className="field">
                <label htmlFor="child-gmail-grade">Grade</label>
                <input
                  id="child-gmail-grade"
                  className="input"
                  type="number"
                  min="1"
                  max="12"
                  value={gmailForm.grade}
                  onChange={(event) => updateGmailField('grade', event.target.value)}
                  required
                />
              </div>

              <button type="submit" className="button" disabled={isSubmittingGmail}>
                {isSubmittingGmail ? 'Creating...' : 'Create child with Gmail'}
              </button>
            </form>
          </>
        )}

        {statusMessage ? (
          <div className="info-block success-block children-status-block">
            <strong>Update</strong>
            <span>{statusMessage}</span>
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
                        disabled={isResettingChildId === child.id || isDeletingChildId === child.id}
                        onClick={() => startEditing(child)}
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        className="button-secondary"
                        disabled={isResettingChildId === child.id || isDeletingChildId === child.id}
                        onClick={() => handleResetAccessCode(child.id)}
                      >
                        {isResettingChildId === child.id ? 'Resetting...' : 'Reset code'}
                      </button>
                      <button
                        type="button"
                        className="button-secondary danger-button"
                        disabled={isDeletingChildId === child.id || isResettingChildId === child.id}
                        onClick={() => handleDeleteChild(child.id)}
                      >
                        {isDeletingChildId === child.id ? 'Deleting...' : 'Delete'}
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
