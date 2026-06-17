import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { acceptChildFriendInvite, getChildFriendInvite } from '../lib/api'

export default function ChildFriendInvitePage() {
  const { token } = useParams()
  const navigate = useNavigate()
  const { session } = useAuth()

  const [invite, setInvite] = useState(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isAccepting, setIsAccepting] = useState(false)
  const [error, setError] = useState('')
  const [accepted, setAccepted] = useState(false)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        const data = await getChildFriendInvite(token)
        if (mounted) setInvite(data)
      } catch (err) {
        if (mounted) setError(err.message)
      } finally {
        if (mounted) setIsLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [token])

  async function handleAccept() {
    if (!session?.accessToken) return
    setIsAccepting(true)
    setError('')
    try {
      await acceptChildFriendInvite(session.accessToken, token)
      setAccepted(true)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsAccepting(false)
    }
  }

  if (isLoading) {
    return (
      <section className="child-dash-page">
        <p className="children-empty">Loading invitation...</p>
      </section>
    )
  }

  if (error && !invite) {
    return (
      <section className="child-dash-page">
        <div className="alert">{error}</div>
        <div className="button-row" style={{ marginTop: '1rem' }}>
          <button type="button" className="button-secondary" onClick={() => navigate('/child/friends')}>Go to Friends</button>
        </div>
      </section>
    )
  }

  if (accepted) {
    return (
      <section className="child-dash-page" style={{ textAlign: 'center', paddingTop: '3rem' }}>
        <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>🎉</div>
        <h2>You're now friends with {invite?.requesterName}!</h2>
        <p className="child-meta" style={{ marginTop: '0.5rem' }}>Grade {invite?.requesterGrade}</p>
        <div className="button-row" style={{ marginTop: '1.5rem', justifyContent: 'center' }}>
          <button type="button" className="button" onClick={() => navigate('/child/friends')}>See my friends</button>
        </div>
      </section>
    )
  }

  if (invite?.status === 'Accepted') {
    return (
      <section className="child-dash-page" style={{ textAlign: 'center', paddingTop: '3rem' }}>
        <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>✅</div>
        <h2>This invitation was already accepted.</h2>
        <div className="button-row" style={{ marginTop: '1.5rem', justifyContent: 'center' }}>
          <button type="button" className="button-secondary" onClick={() => navigate('/child/friends')}>Go to Friends</button>
        </div>
      </section>
    )
  }

  return (
    <section className="child-dash-page" style={{ textAlign: 'center', paddingTop: '3rem' }}>
      <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>🤝</div>
      <h2>Friend request</h2>
      <p style={{ marginTop: '0.75rem', fontSize: '1.1rem' }}>
        <strong>{invite?.requesterName}</strong> (Grade {invite?.requesterGrade}) wants to be friends with you!
      </p>

      {error ? <div className="alert" style={{ marginTop: '1rem' }}>{error}</div> : null}

      <div className="button-row" style={{ marginTop: '2rem', justifyContent: 'center' }}>
        <button type="button" className="button" disabled={isAccepting} onClick={handleAccept}>
          {isAccepting ? 'Accepting…' : '✅ Yes, be friends!'}
        </button>
        <button type="button" className="button-secondary" onClick={() => navigate('/child/friends')}>
          Maybe later
        </button>
      </div>
    </section>
  )
}
