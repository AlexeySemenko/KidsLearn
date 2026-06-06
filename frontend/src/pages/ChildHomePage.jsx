import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { getChildAssignments } from '../lib/api'

function formatDate(value) {
  if (!value) {
    return 'No due date'
  }

  return new Date(value).toLocaleString()
}

function shortId(value) {
  if (!value) {
    return 'unknown'
  }

  return value.slice(0, 8)
}

export default function ChildHomePage() {
  const { session } = useAuth()
  const [assignments, setAssignments] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let isMounted = true

    async function loadAssignments() {
      if (!session?.accessToken) {
        if (isMounted) {
          setIsLoading(false)
        }
        return
      }

      try {
        setError('')
        const response = await getChildAssignments(session.accessToken)
        if (isMounted) {
          setAssignments(response)
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

    loadAssignments()

    return () => {
      isMounted = false
    }
  }, [session?.accessToken])

  return (
    <section className="panel-grid">
      <article className="hero-card assignments-hero">
        <div className="brand-kicker">Epic 4.1</div>
        <h2>Assigned work is now loaded from the child API.</h2>
        <p>
          This screen now shows real assignments for the logged-in child account.
          Solving and results detail can build directly on top of this list.
        </p>
        <div className="badge-row">
          <span className="badge">GET /child/assignments</span>
          <span className="badge">Child scope only</span>
          <span className="badge">Ready for solving flow</span>
        </div>
      </article>

      <article className="panel-card assignments-form-card">
        <h3>Workload snapshot</h3>
        <p>Track what is assigned and what still needs to be completed.</p>
        <div className="metric">{assignments.length}</div>
        <div className="metric-copy">Total assignments in your queue</div>
      </article>

      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>My assignments</h3>
            <p>Latest assigned work available for solving.</p>
          </div>
          <span className="badge">{assignments.length} records</span>
        </div>

        {isLoading ? <p className="children-empty">Loading assignments...</p> : null}
        {!isLoading && assignments.length === 0 ? <p className="children-empty">No assignments yet. New work will appear here.</p> : null}
        {error ? <div className="alert assignments-alert">{error}</div> : null}

        {!isLoading && assignments.length > 0 ? (
          <div className="children-list">
            {assignments.map((assignment) => (
              <article key={assignment.id} className="assignment-row">
                <div className="assignment-copy">
                  <div className="assignment-topline">
                    <div className="child-name">Lesson {shortId(assignment.lessonId)}</div>
                    <span className="assignment-status-pill">{assignment.status}</span>
                  </div>

                  <div className="child-meta">Assignment {shortId(assignment.id)}</div>

                  <div className="assignment-timeline">
                    <span className="assignment-meta-chip">Assigned {formatDate(assignment.assignedAt)}</span>
                    <span className="assignment-meta-chip">Due {formatDate(assignment.dueDate)}</span>
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </article>
    </section>
  )
}
