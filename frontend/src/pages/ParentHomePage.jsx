import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getAssignments, getChildren, getLessons, getParentChildResults } from '../lib/api'
import ChildStatsPanel, { SUBJECT_EMOJI, scoreEmoji, scoreVariant } from '../components/ChildStatsPanel'

const PILLAR_COLORS = ['#60a5fa', '#34d399', '#a78bfa', '#f87171', '#fbbf24', '#fb923c']
const PILLAR_EMOJIS = ['🦊', '🐻', '🐼', '🦁', '🐸', '🦋']

const STATUS_PILL = {
  Assigned:   { cls: 'status-new', label: '✨ Assigned' },
  InProgress: { cls: '',           label: '⚡ In progress' },
  Completed:  { cls: 'status-success', label: '✅ Done' },
}

function formatRelative(iso) {
  if (!iso) return ''
  const diff = Date.now() - new Date(iso).getTime()
  const mins  = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days  = Math.floor(diff / 86400000)
  if (mins < 2)   return 'just now'
  if (mins < 60)  return `${mins}m ago`
  if (hours < 24) return `${hours}h ago`
  if (days < 7)   return `${days}d ago`
  return new Date(iso).toLocaleDateString()
}

export default function ParentHomePage() {
  const { session } = useAuth()
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [children, setChildren] = useState([])
  const [lessons, setLessons] = useState([])
  const [assignments, setAssignments] = useState([])

  const [selectedChildId, setSelectedChildId] = useState(null)
  const [childResults, setChildResults] = useState([])
  const [childResultsLoading, setChildResultsLoading] = useState(false)

  useEffect(() => {
    let isMounted = true

    async function loadDashboard() {
      if (!session?.accessToken) {
        if (isMounted) setIsLoading(false)
        return
      }

      try {
        setError('')
        const [childrenResponse, lessonsResponse, assignmentsResponse] = await Promise.all([
          getChildren(session.accessToken),
          getLessons(session.accessToken),
          getAssignments(session.accessToken),
        ])

        if (!isMounted) return

        setChildren(childrenResponse)
        setLessons(lessonsResponse.items ?? [])
        setAssignments(assignmentsResponse)
      } catch (requestError) {
        if (isMounted) setError(requestError.message)
      } finally {
        if (isMounted) setIsLoading(false)
      }
    }

    loadDashboard()
    return () => { isMounted = false }
  }, [session?.accessToken])

  async function handlePillarClick(childId) {
    if (selectedChildId === childId) {
      setSelectedChildId(null)
      setChildResults([])
      return
    }

    setSelectedChildId(childId)
    setChildResultsLoading(true)
    try {
      const results = await getParentChildResults(session.accessToken, childId)
      setChildResults(results)
    } catch (err) {
      setError(err.message)
      setChildResults([])
    } finally {
      setChildResultsLoading(false)
    }
  }

  const selectedChild = children.find((c) => c.id === selectedChildId)
  const completedAssignments = assignments.filter((a) => a.status === 'Completed').length
  const overdueAssignments = assignments.filter(
    (a) => a.dueDate && a.status !== 'Completed' && new Date(a.dueDate).getTime() < Date.now()
  ).length
  const completionRate = assignments.length === 0
    ? 0
    : Math.round((completedAssignments / assignments.length) * 100)

  const recentLessons = useMemo(() =>
    [...lessons].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt)).slice(0, 3),
    [lessons])

  const recentAssigned = useMemo(() =>
    [...assignments]
      .filter((a) => a.status !== 'Completed')
      .sort((a, b) => new Date(b.assignedAt) - new Date(a.assignedAt))
      .slice(0, 3),
    [assignments])

  const recentSolved = useMemo(() =>
    [...assignments]
      .filter((a) => a.status === 'Completed')
      .sort((a, b) => new Date(b.assignedAt) - new Date(a.assignedAt))
      .slice(0, 3),
    [assignments])

  return (
    <section className="parent-dash-page">
      {error ? <div className="alert" style={{ marginBottom: '1rem' }}>{error}</div> : null}

      {/* ── Children pillars ── */}
      {!isLoading && children.length > 0 ? (
        <div className="parent-dash-pillars-wrap">
          <div className="parent-dash-pillars-label">My children</div>
          <div className="parent-dash-pillars">
            {children.map((child, i) => (
              <button
                key={child.id}
                type="button"
                className={`parent-child-pillar${selectedChildId === child.id ? ' is-active' : ''}`}
                style={{ '--pillar-color': PILLAR_COLORS[i % PILLAR_COLORS.length] }}
                onClick={() => handlePillarClick(child.id)}
              >
                <span className="pillar-avatar">{PILLAR_EMOJIS[i % PILLAR_EMOJIS.length]}</span>
                <span className="pillar-name">{child.name}</span>
                <span className="pillar-grade">Grade {child.grade}</span>
                {selectedChildId === child.id ? (
                  <span className="pillar-active-hint">tap to close</span>
                ) : null}
              </button>
            ))}
          </div>
        </div>
      ) : null}

      {/* ── Selected child stats ── */}
      {selectedChildId ? (
        <div className="parent-child-stats-view">
          <div className="parent-child-stats-header">
            <h3>
              {PILLAR_EMOJIS[children.findIndex((c) => c.id === selectedChildId) % PILLAR_EMOJIS.length]}{' '}
              {selectedChild?.name ?? 'Child'}'s progress
            </h3>
            <span className="badge">Grade {selectedChild?.grade}</span>
          </div>
          <ChildStatsPanel results={childResults} isLoading={childResultsLoading} />
        </div>
      ) : (
        <>
          {isLoading ? (
            <p className="children-empty" style={{ padding: '2rem 0' }}>Loading dashboard...</p>
          ) : (
            <>
              {/* ── Overview stats ── */}
              <div className="parent-overview-grid">
                <Link to="/parent/children" className="parent-overview-card parent-overview-card--link" data-accent="blue">
                  <span className="parent-ov-icon">👦</span>
                  <span className="parent-ov-value">{children.length}</span>
                  <span className="parent-ov-label">Children</span>
                </Link>
                <Link to="/parent/lessons" className="parent-overview-card parent-overview-card--link" data-accent="green">
                  <span className="parent-ov-icon">📚</span>
                  <span className="parent-ov-value">{lessons.length}</span>
                  <span className="parent-ov-label">Lessons</span>
                </Link>
                <div className="parent-overview-card" data-accent="yellow">
                  <span className="parent-ov-icon">✅</span>
                  <span className="parent-ov-value">{completionRate}%</span>
                  <span className="parent-ov-label">Completion rate</span>
                </div>
                <div className="parent-overview-card" data-accent="red">
                  <span className="parent-ov-icon">⏰</span>
                  <span className="parent-ov-value">{overdueAssignments}</span>
                  <span className="parent-ov-label">Overdue</span>
                </div>
              </div>

              {/* ── Recent activity ── */}
              <div className="parent-dash-recent-grid">
                {/* Last created lessons */}
                <div className="parent-dash-recent-card">
                  <div className="parent-dash-recent-header">
                    <h4>📚 Recent lessons</h4>
                    <Link to="/parent/lessons" className="parent-dash-recent-link">View all →</Link>
                  </div>
                  {recentLessons.length === 0 ? (
                    <p className="parent-dash-recent-empty">No lessons yet.</p>
                  ) : recentLessons.map((lesson) => (
                    <div key={lesson.id} className="parent-dash-recent-item">
                      <div className="parent-dash-recent-main">
                        <span className="parent-dash-recent-title">
                          {SUBJECT_EMOJI[lesson.subject] || '📚'} {lesson.title}
                        </span>
                        <span className="parent-dash-recent-meta">Grade {lesson.grade} · {lesson.subject}</span>
                      </div>
                      <span className="parent-dash-recent-time">{formatRelative(lesson.createdAt)}</span>
                    </div>
                  ))}
                </div>

                {/* Last assigned */}
                <div className="parent-dash-recent-card">
                  <div className="parent-dash-recent-header">
                    <h4>📋 Recently assigned</h4>
                    <Link to="/parent/assignments" className="parent-dash-recent-link">View all →</Link>
                  </div>
                  {recentAssigned.length === 0 ? (
                    <p className="parent-dash-recent-empty">No active assignments.</p>
                  ) : recentAssigned.map((a) => (
                    <div key={a.id} className="parent-dash-recent-item">
                      <div className="parent-dash-recent-main">
                        <span className="parent-dash-recent-title">{a.childName}</span>
                        <span className="parent-dash-recent-meta">
                          {SUBJECT_EMOJI[a.lessonSubject] || '📚'} {a.lessonTitle}
                        </span>
                      </div>
                      <span className={`assignment-status-pill ${STATUS_PILL[a.status]?.cls ?? ''}`} style={{ fontSize: '0.7rem', padding: '0.15rem 0.5rem' }}>
                        {STATUS_PILL[a.status]?.label ?? a.status}
                      </span>
                    </div>
                  ))}
                </div>

                {/* Last solved */}
                <div className="parent-dash-recent-card">
                  <div className="parent-dash-recent-header">
                    <h4>🏆 Recently solved</h4>
                    <Link to="/parent/assignments" className="parent-dash-recent-link">View all →</Link>
                  </div>
                  {recentSolved.length === 0 ? (
                    <p className="parent-dash-recent-empty">No completed assignments yet.</p>
                  ) : recentSolved.map((a) => (
                    <div key={a.id} className="parent-dash-recent-item">
                      <div className="parent-dash-recent-main">
                        <span className="parent-dash-recent-title">{a.childName}</span>
                        <span className="parent-dash-recent-meta">
                          {SUBJECT_EMOJI[a.lessonSubject] || '📚'} {a.lessonTitle}
                        </span>
                      </div>
                      {a.score != null ? (
                        <span className={`assignment-status-pill ${scoreVariant(a.score)}`} style={{ fontSize: '0.7rem', padding: '0.15rem 0.5rem' }}>
                          {scoreEmoji(a.score)} {a.score}%
                        </span>
                      ) : null}
                    </div>
                  ))}
                </div>
              </div>
            </>
          )}
        </>
      )}
    </section>
  )
}
