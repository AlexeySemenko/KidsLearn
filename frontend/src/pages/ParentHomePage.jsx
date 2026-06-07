import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getAssignments, getChildren, getLessons } from '../lib/api'

function formatDate(value) {
  if (!value) {
    return 'No due date'
  }

  return new Date(value).toLocaleString()
}

export default function ParentHomePage() {
  const { session } = useAuth()
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [children, setChildren] = useState([])
  const [lessons, setLessons] = useState([])
  const [assignments, setAssignments] = useState([])

  useEffect(() => {
    let isMounted = true

    async function loadDashboard() {
      if (!session?.accessToken) {
        if (isMounted) {
          setIsLoading(false)
        }
        return
      }

      try {
        setError('')
        const [childrenResponse, lessonsResponse, assignmentsResponse] = await Promise.all([
          getChildren(session.accessToken),
          getLessons(session.accessToken),
          getAssignments(session.accessToken),
        ])

        if (!isMounted) {
          return
        }

        setChildren(childrenResponse)
        setLessons(lessonsResponse.items ?? [])
        setAssignments(assignmentsResponse)
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

    loadDashboard()

    return () => {
      isMounted = false
    }
  }, [session?.accessToken])

  const childNameById = useMemo(
    () => new Map(children.map((child) => [child.id, child.name])),
    [children],
  )

  const lessonTitleById = useMemo(
    () => new Map(lessons.map((lesson) => [lesson.id, lesson.title])),
    [lessons],
  )

  const completedAssignments = useMemo(
    () => assignments.filter((assignment) => assignment.status === 'Completed').length,
    [assignments],
  )

  const overdueAssignments = useMemo(() => {
    const now = Date.now()
    return assignments.filter((assignment) => (
      assignment.dueDate
      && assignment.status !== 'Completed'
      && new Date(assignment.dueDate).getTime() < now
    )).length
  }, [assignments])

  const completionRate = assignments.length === 0
    ? 0
    : Math.round((completedAssignments / assignments.length) * 100)

  const recentAssignments = useMemo(() => {
    return [...assignments]
      .sort((a, b) => new Date(b.assignedAt).getTime() - new Date(a.assignedAt).getTime())
      .slice(0, 5)
  }, [assignments])

  return (
    <section className="panel-grid">
      <article className="hero-card">
        <div className="brand-kicker">Parent dashboard</div>
        <h2>Live workspace overview.</h2>
        <p>
          Monitor children, lessons, and assignment execution from one place,
          then jump directly into the workspace that needs attention.
        </p>
        <div className="badge-row">
          <span className="badge">Children: {children.length}</span>
          <span className="badge">Lessons: {lessons.length}</span>
          <span className="badge">Assignments: {assignments.length}</span>
        </div>
      </article>

      <article className="side-card panel-card">
        <h3>Quick actions</h3>
        <p>Open the target workspace in one click.</p>
        <div className="button-row">
          <Link className="button-secondary inline-link" to="/parent/children">
            Manage children
          </Link>
          <Link className="button-secondary inline-link" to="/parent/lessons">
            Manage lessons
          </Link>
          <Link className="button-secondary inline-link" to="/parent/assignments">
            Open assignments
          </Link>
          <Link className="button-secondary inline-link" to="/parent/reports">
            Open reports
          </Link>
        </div>
      </article>

      {error ? <div className="alert">{error}</div> : null}

      {isLoading ? (
        <article className="assignments-list-card">
          <p className="children-empty">Loading dashboard data...</p>
        </article>
      ) : null}

      {!isLoading ? (
        <article className="assignments-list-card">
          <div className="children-list-header">
            <div>
              <h3>Performance snapshot</h3>
              <p>Current delivery state across the parent workspace.</p>
            </div>
            <span className="badge">Updated live</span>
          </div>

          <div className="reports-metrics-grid">
            <article className="report-metric-card">
              <span className="section-kicker">Children</span>
              <strong>{children.length}</strong>
              <p>Active child profiles in your workspace.</p>
            </article>

            <article className="report-metric-card">
              <span className="section-kicker">Lessons</span>
              <strong>{lessons.length}</strong>
              <p>Total lessons available for assignment.</p>
            </article>

            <article className="report-metric-card">
              <span className="section-kicker">Completion rate</span>
              <strong>{completionRate}%</strong>
              <p>{completedAssignments} of {assignments.length} assignments completed.</p>
            </article>

            <article className="report-metric-card">
              <span className="section-kicker">Overdue</span>
              <strong>{overdueAssignments}</strong>
              <p>Assignments past due and still pending.</p>
            </article>
          </div>
        </article>
      ) : null}

      {!isLoading ? (
        <article className="assignments-list-card">
          <div className="children-list-header">
            <div>
              <h3>Recent assignments</h3>
              <p>Most recently assigned lessons across all children.</p>
            </div>
            <span className="badge">Top 5</span>
          </div>

          {recentAssignments.length === 0 ? (
            <p className="children-empty">No assignments yet. Create the first assignment from the assignments page.</p>
          ) : (
            <div className="children-list">
              {recentAssignments.map((assignment) => (
                <article key={assignment.id} className="assignment-row">
                  <div className="assignment-copy">
                    <div className="assignment-topline">
                      <div className="assignment-lesson-title">
                        {assignment.lessonTitle
                          ?? lessonTitleById.get(assignment.lessonId)
                          ?? assignment.lessonId}
                      </div>
                      <span className={`assignment-status-pill ${assignment.status === 'Completed' ? 'status-success' : ''}`}>
                        {assignment.status}
                      </span>
                    </div>

                    <div className="child-meta">
                      Child: {childNameById.get(assignment.childId) ?? assignment.childId}
                    </div>

                    <div className="assignment-timeline">
                      <span className="assignment-meta-chip">Assigned {formatDate(assignment.assignedAt)}</span>
                      <span className="assignment-meta-chip">Due {formatDate(assignment.dueDate)}</span>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </article>
      ) : null}
    </section>
  )
}
