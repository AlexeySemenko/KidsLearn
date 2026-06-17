import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getAssignments, getChildren, getLessons, getParentChildResults } from '../lib/api'
import ChildStatsPanel from '../components/ChildStatsPanel'

const PILLAR_COLORS = ['#60a5fa', '#34d399', '#a78bfa', '#f87171', '#fbbf24', '#fb923c']
const PILLAR_EMOJIS = ['🦊', '🐻', '🐼', '🦁', '🐸', '🦋']

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
                  <span className="pillar-active-hint">tap to go back</span>
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
          {/* ── Parent overview stats ── */}
          {isLoading ? (
            <p className="children-empty" style={{ padding: '2rem 0' }}>Loading dashboard...</p>
          ) : (
            <>
              <div className="parent-overview-grid">
                <div className="parent-overview-card" data-accent="blue">
                  <span className="parent-ov-icon">👦</span>
                  <span className="parent-ov-value">{children.length}</span>
                  <span className="parent-ov-label">Children</span>
                </div>
                <div className="parent-overview-card" data-accent="green">
                  <span className="parent-ov-icon">📚</span>
                  <span className="parent-ov-value">{lessons.length}</span>
                  <span className="parent-ov-label">Lessons</span>
                </div>
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

              <div className="parent-dash-actions-card">
                <div className="children-list-header">
                  <div>
                    <h3>Quick actions</h3>
                    <p>Open the target workspace in one click.</p>
                  </div>
                </div>
                <div className="button-row">
                  <Link className="button-secondary inline-link" to="/parent/children">Manage children</Link>
                  <Link className="button-secondary inline-link" to="/parent/lessons">Manage lessons</Link>
                  <Link className="button-secondary inline-link" to="/parent/assignments">Assignments</Link>
                  <Link className="button-secondary inline-link" to="/parent/reports">Reports</Link>
                </div>
              </div>
            </>
          )}
        </>
      )}
    </section>
  )
}
