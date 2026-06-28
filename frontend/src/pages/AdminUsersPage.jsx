import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { createAdminUser, deleteAdminUser, getAdminUsers, updateAdminUser } from '../lib/api'
import UserFormModal from '../components/UserFormModal'
import Toast from '../components/Toast'

const ROLE_COLOR = { Admin: 'status-danger', Parent: 'status-success', Child: '' }
const ROLES = ['', 'Admin', 'Parent', 'Child']

function formatDate(iso) {
  if (!iso) return '—'
  return new Date(iso).toLocaleString()
}

function SortIcon({ col, sortCol, sortDir }) {
  if (sortCol !== col) return <span className="sort-icon sort-icon--none" aria-hidden="true">⇅</span>
  return <span className="sort-icon" aria-hidden="true">{sortDir === 'asc' ? '↑' : '↓'}</span>
}

function sortUsers(users, col, dir) {
  if (!col) return users
  return [...users].sort((a, b) => {
    let av = a[col]
    let bv = b[col]
    if (av == null) av = ''
    if (bv == null) bv = ''
    if (col === 'createdAt' || col === 'lastAccessAt') {
      av = av ? new Date(av).getTime() : 0
      bv = bv ? new Date(bv).getTime() : 0
      return dir === 'asc' ? av - bv : bv - av
    }
    av = String(av).toLowerCase()
    bv = String(bv).toLowerCase()
    if (av < bv) return dir === 'asc' ? -1 : 1
    if (av > bv) return dir === 'asc' ? 1 : -1
    return 0
  })
}

export default function AdminUsersPage() {
  const { session } = useAuth()
  const [users, setUsers] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const [filterEmail, setFilterEmail] = useState('')
  const [filterRole, setFilterRole] = useState('')
  const [sortCol, setSortCol] = useState('')
  const [sortDir, setSortDir] = useState('asc')

  const [modalUser, setModalUser] = useState(null)
  const [isSaving, setIsSaving] = useState(false)
  const [modalError, setModalError] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)
  const [isDeleting, setIsDeleting] = useState(false)

  const [toast, setToast] = useState(null)  // { message, type: 'success' | 'warn' }

  function showToast(message, type = 'success') {
    setToast({ message, type })
  }

  useEffect(() => {
    let mounted = true
    async function load() {
      if (!session?.accessToken) { setIsLoading(false); return }
      try {
        const data = await getAdminUsers(session.accessToken)
        if (mounted) setUsers(data)
      } catch (err) {
        if (mounted) setError(err.message)
      } finally {
        if (mounted) setIsLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [session?.accessToken])

  function handleSort(col) {
    setSortCol((prev) => {
      if (prev === col) {
        setSortDir((d) => d === 'asc' ? 'desc' : 'asc')
        return col
      }
      setSortDir('asc')
      return col
    })
  }

  async function handleSave(payload) {
    setModalError('')
    setIsSaving(true)
    try {
      if (modalUser?.id) {
        const updated = await updateAdminUser(session.accessToken, modalUser.id, payload)
        setUsers((cur) => cur.map((u) => u.id === updated.id ? updated : u))
        showToast('User updated.')
      } else {
        const result = await createAdminUser(session.accessToken, payload)
        setUsers((cur) => [...cur, result.user])
        if (result.emailSent) {
          showToast(`User created. Invitation email sent to ${result.user.email}.`)
        } else {
          showToast(`User created. Invitation email could not be sent (SMTP not configured).`, 'warn')
        }
      }
      setModalUser(null)
    } catch (err) {
      setModalError(err.message)
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDelete(user) {
    setIsDeleting(true)
    try {
      await deleteAdminUser(session.accessToken, user.id)
      setUsers((cur) => cur.filter((u) => u.id !== user.id))
      setDeleteTarget(null)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsDeleting(false)
    }
  }

  const filtered = users.filter((u) => {
    if (filterEmail && !u.email.toLowerCase().includes(filterEmail.toLowerCase())) return false
    if (filterRole && u.role !== filterRole) return false
    return true
  })
  const sorted = sortUsers(filtered, sortCol, sortDir)

  function SortTh({ col, children, style }) {
    return (
      <th
        className="admin-th-sortable"
        style={style}
        onClick={() => handleSort(col)}
        aria-sort={sortCol === col ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
      >
        <span className="admin-th-inner">
          {children}
          <SortIcon col={col} sortCol={sortCol} sortDir={sortDir} />
        </span>
      </th>
    )
  }

  return (
    <section className="admin-users-page">
      <div className="children-list-header">
        <div>
          <h2>Users <span className="badge badge--admin">ADMIN</span></h2>
          <p>Manage all users — create, edit roles, or remove accounts.</p>
        </div>
        <button
          type="button"
          className="button"
          onClick={() => { setModalUser({}); setModalError('') }}
        >
          + Add user
        </button>
      </div>

      {error ? <div className="alert">{error}</div> : null}

      <div className="admin-filter-bar">
        <input
          className="admin-filter-input"
          type="text"
          placeholder="Filter by email…"
          value={filterEmail}
          onChange={(e) => setFilterEmail(e.target.value)}
          aria-label="Filter by email"
        />
        <select
          className="admin-filter-input"
          value={filterRole}
          onChange={(e) => setFilterRole(e.target.value)}
          aria-label="Filter by role"
        >
          {ROLES.map((r) => (
            <option key={r} value={r}>{r || 'All roles'}</option>
          ))}
        </select>
      </div>

      {isLoading ? (
        <p className="children-empty">Loading users...</p>
      ) : (
        <div className="admin-users-table-wrap">
          <table className="admin-users-table">
            <thead>
              <tr>
                <SortTh col="email">Name / Email</SortTh>
                <SortTh col="role">Role</SortTh>
                <SortTh col="externalProvider">Auth</SortTh>
                <SortTh col="createdAt">Created</SortTh>
                <SortTh col="lastAccessAt">Last access</SortTh>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {sorted.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '2rem', opacity: 0.5 }}>
                    No users match the current filters.
                  </td>
                </tr>
              ) : sorted.map((user) => (
                <tr key={user.id}>
                  <td data-label="Name / Email">
                    <div className="admin-user-name">{user.displayName || <em className="muted">—</em>}</div>
                    <div className="admin-user-email">{user.email}</div>
                  </td>
                  <td data-label="Role">
                    <span className={`assignment-status-pill ${ROLE_COLOR[user.role] ?? ''}`}>
                      {user.role}
                    </span>
                  </td>
                  <td data-label="Auth">
                    <span className="assignment-meta-chip">
                      {user.externalProvider ?? 'password'}
                    </span>
                  </td>
                  <td data-label="Created" className="admin-date-cell">{formatDate(user.createdAt)}</td>
                  <td data-label="Last access" className="admin-date-cell">{formatDate(user.lastAccessAt)}</td>
                  <td data-label="">
                    <div className="button-row" style={{ gap: '0.5rem' }}>
                      <button
                        type="button"
                        className="button-secondary"
                        style={{ padding: '0.35rem 0.75rem', fontSize: '0.8rem' }}
                        onClick={() => { setModalUser(user); setModalError('') }}
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        className="button-secondary"
                        style={{ padding: '0.35rem 0.75rem', fontSize: '0.8rem', borderColor: 'rgba(255,80,80,0.4)', color: 'var(--danger)' }}
                        onClick={() => setDeleteTarget(user)}
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modalUser !== null ? (
        <UserFormModal
          user={modalUser?.id ? modalUser : null}
          onSave={handleSave}
          onClose={() => setModalUser(null)}
          isSaving={isSaving}
          error={modalError}
        />
      ) : null}

      {deleteTarget ? (
        <div className="modal-overlay" onClick={() => setDeleteTarget(null)}>
          <div className="modal-card" style={{ maxWidth: 420, marginBlock: 'auto' }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1rem' }}>
              <h3 style={{ margin: 0 }}>Delete user</h3>
              <button type="button" className="modal-close-btn" onClick={() => setDeleteTarget(null)}>✕</button>
            </div>
            <p style={{ margin: '0 0 1.5rem' }}>
              Are you sure you want to delete <strong>{deleteTarget.displayName || deleteTarget.email}</strong>?
              This cannot be undone.
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
      {toast ? <Toast message={toast.message} type={toast.type} onDismiss={() => setToast(null)} /> : null}
    </section>
  )
}
