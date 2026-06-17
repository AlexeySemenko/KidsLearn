import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import ChildStatsPanel from '../components/ChildStatsPanel'
import { getFriendNote, getFriendResults, getChildFriends, sendChildFriendInvite, updateFriendNote } from '../lib/api'

const FRIEND_EMOJIS = ['🐼', '🦊', '🐸', '🦁', '🐨', '🐯', '🦄', '🐻', '🐙', '🦋']
const FRIEND_COLORS = ['#6366f1', '#f59e0b', '#10b981', '#ef4444', '#3b82f6', '#ec4899', '#8b5cf6', '#14b8a6', '#f97316', '#84cc16']

function FriendNoteBubble({ friendshipId, friendChildId, friendName, accessToken, hubConnection, onClose }) {
  const [lastNoteText, setLastNoteText] = useState(undefined) // undefined = loading
  const [lastNoteIsFromMe, setLastNoteIsFromMe] = useState(false)
  const [myNote, setMyNote] = useState('')
  const [isEditing, setIsEditing] = useState(false)
  const [draft, setDraft] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const textareaRef = useRef(null)

  useEffect(() => {
    let mounted = true
    getFriendNote(accessToken, friendChildId)
      .then((data) => {
        if (!mounted) return
        setLastNoteText(data.lastNoteText ?? null)
        setLastNoteIsFromMe(data.lastNoteIsFromMe)
        setMyNote(data.myNote ?? '')
      })
      .catch(() => { if (mounted) setLastNoteText(null) })
    return () => { mounted = false }
  }, [accessToken, friendChildId])

  // Live updates via SignalR
  useEffect(() => {
    if (!hubConnection) return
    function onNoteUpdated(msg) {
      if (msg.friendshipId !== friendshipId) return
      setLastNoteText(msg.lastNoteText)
      setLastNoteIsFromMe(false)
    }
    hubConnection.on('FriendNoteUpdated', onNoteUpdated)
    return () => hubConnection.off('FriendNoteUpdated', onNoteUpdated)
  }, [hubConnection, friendshipId])

  function startEdit() {
    setDraft('')
    setIsEditing(true)
    setTimeout(() => textareaRef.current?.focus(), 50)
  }

  async function saveNote() {
    setIsSaving(true)
    try {
      const trimmed = draft.trim() || null
      await updateFriendNote(accessToken, friendChildId, trimmed)
      const saved = trimmed ?? ''
      setMyNote(saved)
      setLastNoteText(saved || null)
      setLastNoteIsFromMe(true)
      setIsEditing(false)
      onClose()
    } catch {
      // keep open on error
    } finally {
      setIsSaving(false)
    }
  }

  const label = lastNoteIsFromMe ? 'You' : friendName

  return (
    <div className="friend-note-popover">
      <div className="friend-note-bubble">
        <div className="friend-note-bubble-tail" aria-hidden="true" />
        {lastNoteText === undefined ? (
          <span className="friend-note-placeholder">Loading…</span>
        ) : isEditing ? (
          <>
            <textarea
              ref={textareaRef}
              className="friend-note-textarea"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              maxLength={500}
              rows={3}
              placeholder={`Write a message for ${friendName}… 💌`}
            />
            <div className="friend-note-actions">
              <button type="button" className="button" style={{ fontSize: '0.8rem', padding: '0.3rem 0.8rem' }} disabled={isSaving} onClick={saveNote}>
                {isSaving ? 'Sending…' : 'Send'}
              </button>
              <button type="button" className="button-secondary" style={{ fontSize: '0.8rem', padding: '0.3rem 0.8rem' }} onClick={onClose}>
                Cancel
              </button>
            </div>
          </>
        ) : (
          <button type="button" className="friend-note-view-btn" onClick={startEdit}>
            {lastNoteText
              ? <><span className="friend-note-label">{label}: </span><span className="friend-note-text">{lastNoteText}</span></>
              : <span className="friend-note-placeholder">Write a message… 💌</span>
            }
            <span className="friend-note-edit-hint">✏️</span>
          </button>
        )}
      </div>
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

  const [bubbleFriendId, setBubbleFriendId] = useState(null)
  const [hubConnection, setHubConnection] = useState(null)

  // Load friends
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

  // SignalR connection
  useEffect(() => {
    if (!session?.accessToken) return

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/friends', { accessTokenFactory: () => session.accessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.start().catch(() => {}) // ignore connection errors silently

    // Show envelope blink on friend card when they send a new message
    connection.on('FriendNoteUpdated', (msg) => {
      setFriends((prev) => prev.map((f) =>
        f.friendshipId === msg.friendshipId ? { ...f, hasUnreadMessage: true } : f
      ))
    })

    setHubConnection(connection)
    return () => { connection.stop() }
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

    try {
      const results = await getFriendResults(session.accessToken, friend.childId)
      setFriendResults(results)
    } catch {
      setFriendResults([])
    } finally {
      setFriendResultsLoading(false)
    }
  }

  function handleEnvelopeClick(e, friend) {
    e.stopPropagation()
    if (bubbleFriendId === friend.friendshipId) {
      setBubbleFriendId(null)
      return
    }
    setBubbleFriendId(friend.friendshipId)
    // Clear unread badge optimistically
    setFriends((prev) => prev.map((f) =>
      f.friendshipId === friend.friendshipId ? { ...f, hasUnreadMessage: false } : f
    ))
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
        <button type="button" className="button" onClick={() => setShowInviteModal(true)}>
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
            <div key={friend.friendshipId} className="friend-card-wrap">
              {bubbleFriendId === friend.friendshipId ? (
                <FriendNoteBubble
                  friendshipId={friend.friendshipId}
                  friendChildId={friend.childId}
                  friendName={friend.name}
                  accessToken={session.accessToken}
                  hubConnection={hubConnection}
                  onClose={() => setBubbleFriendId(null)}
                />
              ) : null}
              <button
                type="button"
                className={`parent-child-pillar${selectedFriend?.friendshipId === friend.friendshipId ? ' is-active' : ''}`}
                style={{ '--pillar-color': FRIEND_COLORS[i % FRIEND_COLORS.length] }}
                onClick={() => handleFriendClick(friend)}
              >
                <span className="pillar-avatar">{FRIEND_EMOJIS[i % FRIEND_EMOJIS.length]}</span>
                <span className="pillar-name">{friend.name}</span>
                <span className="pillar-grade">Grade {friend.grade}</span>
                {selectedFriend?.friendshipId === friend.friendshipId ? (
                  <span className="pillar-active-hint">tap to close</span>
                ) : null}
              </button>
              <button
                type="button"
                className={`friend-envelope-btn${friend.hasUnreadMessage ? ' is-unread' : ''}`}
                onClick={(e) => handleEnvelopeClick(e, friend)}
                aria-label={friend.hasUnreadMessage ? 'New message from friend' : 'Send message'}
              >
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <rect x="2" y="4" width="20" height="16" rx="2" />
                  <polyline points="2,4 12,13 22,4" />
                </svg>
              </button>
            </div>
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
