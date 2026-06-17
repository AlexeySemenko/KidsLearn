import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { getChildFriends, sendChildFriendInvite } from '../lib/api'

const FRIEND_EMOJIS = ['🐼', '🦊', '🐸', '🦁', '🐨', '🐯', '🦄', '🐻', '🐙', '🦋']
const FRIEND_COLORS = ['#6366f1', '#f59e0b', '#10b981', '#ef4444', '#3b82f6', '#ec4899', '#8b5cf6', '#14b8a6', '#f97316', '#84cc16']

function formatDate(value) {
  if (!value) return ''
  return new Date(value).toLocaleDateString()
}

export default function ChildFriendsPage() {
  const { session } = useAuth()
  const [friends, setFriends] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const [showInviteModal, setShowInviteModal] = useState(false)
  const [inviteEmail, setInviteEmail] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [inviteError, setInviteError] = useState('')
  const [inviteSuccess, setInviteSuccess] = useState('')

  const [selectedFriend, setSelectedFriend] = useState(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      if (!session?.accessToken) { setIsLoading(false); return }
      try {
        const data = await getChildFriends(session.accessToken)
        if (mounted) setFriends(data)
      } catch (err) {
        if (mounted) setError(err.message)
      } finally {
        if (mounted) setIsLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [session?.accessToken])

  async function handleSendInvite(event) {
    event.preventDefault()
    if (!inviteEmail.trim()) return

    setIsSending(true)
    setInviteError('')
    setInviteSuccess('')

    try {
      await sendChildFriendInvite(session.accessToken, inviteEmail.trim())
      setInviteSuccess(`Invitation sent to ${inviteEmail.trim()}!`)
      setInviteEmail('')
    } catch (err) {
      setInviteError(err.message)
    } finally {
      setIsSending(false)
    }
  }

  function closeInviteModal() {
    setShowInviteModal(false)
    setInviteEmail('')
    setInviteError('')
    setInviteSuccess('')
  }

  return (
    <section className="child-dash-page">
      <div className="children-list-header" style={{ marginBottom: '1.5rem' }}>
        <div>
          <h2>Friends 🤝</h2>
          <p>{isLoading ? '…' : `${friends.length} friend${friends.length !== 1 ? 's' : ''}`}</p>
        </div>
        <button
          type="button"
          className="button"
          onClick={() => setShowInviteModal(true)}
        >
          + Add friend
        </button>
      </div>

      {error ? <div className="alert">{error}</div> : null}

      {isLoading ? <p className="children-empty">Loading friends...</p> : null}

      {!isLoading && friends.length === 0 ? (
        <p className="children-empty">No friends yet. Invite someone! 🙌</p>
      ) : null}

      {!isLoading && friends.length > 0 ? (
        <div className="parent-dash-pillars" style={{ flexWrap: 'wrap', gap: '1rem' }}>
          {friends.map((friend, i) => (
            <button
              key={friend.friendshipId}
              type="button"
              className="parent-child-pillar"
              style={{ '--pillar-color': FRIEND_COLORS[i % FRIEND_COLORS.length] }}
              onClick={() => setSelectedFriend(friend)}
            >
              <span className="pillar-avatar">{FRIEND_EMOJIS[i % FRIEND_EMOJIS.length]}</span>
              <span className="pillar-name">{friend.name}</span>
              <span className="pillar-grade">Grade {friend.grade}</span>
            </button>
          ))}
        </div>
      ) : null}

      {/* Invite modal */}
      {showInviteModal ? (
        <div className="modal-overlay" role="presentation">
          <section className="modal-card" role="dialog" aria-modal="true" aria-labelledby="invite-friend-title">
            <div className="children-list-header modal-header">
              <div>
                <h3 id="invite-friend-title">Add friend</h3>
                <p>Enter your friend's email to send them an invite.</p>
              </div>
              <button type="button" className="button-secondary" onClick={closeInviteModal}>Close</button>
            </div>

            {inviteSuccess ? (
              <div className="info-block success-block" role="status">
                <strong>Sent!</strong>
                <span>{inviteSuccess}</span>
              </div>
            ) : (
              <form className="auth-form compact-form" onSubmit={handleSendInvite}>
                <div className="field">
                  <label htmlFor="friend-invite-email">Friend's email</label>
                  <input
                    id="friend-invite-email"
                    className="input"
                    type="email"
                    value={inviteEmail}
                    onChange={(e) => setInviteEmail(e.target.value)}
                    placeholder="friend@example.com"
                    autoFocus
                    required
                  />
                </div>
                {inviteError ? <div className="alert" role="alert">{inviteError}</div> : null}
                <div className="button-row modal-actions">
                  <button type="submit" className="button" disabled={isSending}>
                    {isSending ? 'Sending…' : 'Send invite'}
                  </button>
                  <button type="button" className="button-secondary" onClick={closeInviteModal}>Cancel</button>
                </div>
              </form>
            )}
          </section>
        </div>
      ) : null}

      {/* Friend detail modal */}
      {selectedFriend ? (
        <div className="modal-overlay" role="presentation">
          <section className="modal-card" role="dialog" aria-modal="true" aria-labelledby="friend-detail-title">
            <div className="children-list-header modal-header">
              <div>
                <h3 id="friend-detail-title">
                  {FRIEND_EMOJIS[friends.indexOf(selectedFriend) % FRIEND_EMOJIS.length]} {selectedFriend.name}
                </h3>
                <p>Friend details</p>
              </div>
              <button type="button" className="button-secondary" onClick={() => setSelectedFriend(null)}>Close</button>
            </div>

            <div className="child-detail-grid" style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', padding: '0.5rem 0' }}>
              <div className="child-detail-row">
                <span className="child-meta">Grade</span>
                <strong>{selectedFriend.grade}</strong>
              </div>
              <div className="child-detail-row">
                <span className="child-meta">Friends since</span>
                <strong>{formatDate(selectedFriend.friendsSince)}</strong>
              </div>
            </div>

            <div className="button-row modal-actions">
              <button type="button" className="button-secondary" onClick={() => setSelectedFriend(null)}>Close</button>
            </div>
          </section>
        </div>
      ) : null}
    </section>
  )
}
