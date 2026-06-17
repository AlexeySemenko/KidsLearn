import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import ChildStatsPanel from '../components/ChildStatsPanel'
import { getFriendNote, getFriendResults, getChildFriends, sendChildFriendInvite, updateFriendNote } from '../lib/api'

const FRIEND_EMOJIS = ['🐼', '🦊', '🐸', '🦁', '🐨', '🐯', '🦄', '🐻', '🐙', '🦋']
const FRIEND_COLORS = ['#6366f1', '#f59e0b', '#10b981', '#ef4444', '#3b82f6', '#ec4899', '#8b5cf6', '#14b8a6', '#f97316', '#84cc16']

function FriendNoteBubble({ friendChildId, accessToken }) {
  const [myNote, setMyNote] = useState(null)
  const [isEditing, setIsEditing] = useState(false)
  const [draft, setDraft] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const textareaRef = useRef(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        const data = await getFriendNote(accessToken, friendChildId)
        if (mounted) setMyNote(data.myNote ?? '')
      } catch {
        if (mounted) setMyNote('')
      }
    }
    load()
    return () => { mounted = false }
  }, [accessToken, friendChildId])

  function startEdit() {
    setDraft(myNote ?? '')
    setIsEditing(true)
    setTimeout(() => textareaRef.current?.focus(), 50)
  }

  async function saveNote() {
    setIsSaving(true)
    try {
      const trimmed = draft.trim() || null
      await updateFriendNote(accessToken, friendChildId, trimmed)
      setMyNote(trimmed ?? '')
      setIsEditing(false)
    } catch {
      // keep editing
    } finally {
      setIsSaving(false)
    }
  }

  function cancelEdit() {
    setIsEditing(false)
    setDraft('')
  }

  if (myNote === null) return null // still loading

  return (
    <div className="friend-note-bubble-wrap">
      {isEditing ? (
        <div className="friend-note-bubble friend-note-bubble--editing">
          <div className="friend-note-bubble-tail" aria-hidden="true" />
          <textarea
            ref={textareaRef}
            className="friend-note-textarea"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            maxLength={500}
            rows={3}
            placeholder="Write a message for your friend… 💌"
          />
          <div className="friend-note-actions">
            <button type="button" className="button" style={{ fontSize: '0.8rem', padding: '0.3rem 0.8rem' }} disabled={isSaving} onClick={saveNote}>
              {isSaving ? 'Saving…' : 'Save'}
            </button>
            <button type="button" className="button-secondary" style={{ fontSize: '0.8rem', padding: '0.3rem 0.8rem' }} onClick={cancelEdit}>
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <button type="button" className="friend-note-bubble friend-note-bubble--view" onClick={startEdit} title="Click to edit your message">
          <div className="friend-note-bubble-tail" aria-hidden="true" />
          {myNote
            ? <span className="friend-note-text">{myNote}</span>
            : <span className="friend-note-placeholder">Leave a message… 💌</span>
          }
          <span className="friend-note-edit-hint">✏️</span>
        </button>
      )}
    </div>
  )
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
  const [friendResults, setFriendResults] = useState([])
  const [friendResultsLoading, setFriendResultsLoading] = useState(false)

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

  async function handleFriendClick(friend) {
    if (selectedFriend?.friendshipId === friend.friendshipId) {
      setSelectedFriend(null)
      setFriendResults([])
      return
    }

    setSelectedFriend(friend)
    setFriendResults([])
    setFriendResultsLoading(true)

    // Clear envelope badge optimistically
    setFriends((prev) => prev.map((f) =>
      f.friendshipId === friend.friendshipId ? { ...f, hasUnreadMessage: false } : f
    ))

    try {
      const results = await getFriendResults(session.accessToken, friend.childId)
      setFriendResults(results)
    } catch {
      setFriendResults([])
    } finally {
      setFriendResultsLoading(false)
    }
  }

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
        <div className="parent-dash-pillars" style={{ flexWrap: 'wrap', gap: '1rem', marginBottom: '1.5rem' }}>
          {friends.map((friend, i) => (
            <button
              key={friend.friendshipId}
              type="button"
              className={`parent-child-pillar${selectedFriend?.friendshipId === friend.friendshipId ? ' is-active' : ''}`}
              style={{ '--pillar-color': FRIEND_COLORS[i % FRIEND_COLORS.length] }}
              onClick={() => handleFriendClick(friend)}
            >
              <span className="pillar-avatar">{FRIEND_EMOJIS[i % FRIEND_EMOJIS.length]}</span>
              <span className="pillar-name">{friend.name}</span>
              <span className="pillar-grade">Grade {friend.grade}</span>
              {friend.hasUnreadMessage ? (
                <span className="friend-unread-envelope" aria-label="New message">✉️</span>
              ) : null}
              {selectedFriend?.friendshipId === friend.friendshipId ? (
                <span className="pillar-active-hint">tap to close</span>
              ) : null}
            </button>
          ))}
        </div>
      ) : null}

      {selectedFriend ? (
        <div className="parent-child-stats-view">
          <div className="parent-child-stats-header" style={{ flexWrap: 'wrap', gap: '0.75rem', alignItems: 'flex-start' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <h3>
                {FRIEND_EMOJIS[friends.findIndex((f) => f.friendshipId === selectedFriend.friendshipId) % FRIEND_EMOJIS.length]}{' '}
                {selectedFriend.name}'s progress
              </h3>
              <span className="badge">Grade {selectedFriend.grade}</span>
            </div>
            <FriendNoteBubble
              friendChildId={selectedFriend.childId}
              accessToken={session.accessToken}
            />
          </div>
          <ChildStatsPanel results={friendResults} isLoading={friendResultsLoading} pendingCount={0} />
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
    </section>
  )
}
