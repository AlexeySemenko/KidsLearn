import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import {
  getChildAssignments,
  getChildResultDetail,
  getChildResults,
} from '../lib/api'
import LessonViewModal from '../components/LessonViewModal'
import SolveMissionModal from '../components/SolveMissionModal'
import ChildStatsPanel, { SUBJECT_EMOJI, scoreEmoji, scoreVariant } from '../components/ChildStatsPanel'

function getGreeting() {
  const h = new Date().getHours()
  if (h < 6)  return { text: 'Good night',     emoji: '🌙' }
  if (h < 12) return { text: 'Good morning',   emoji: '🌅' }
  if (h < 17) return { text: 'Good afternoon', emoji: '☀️' }
  if (h < 22) return { text: 'Good evening',   emoji: '🌤️' }
  return       { text: 'Good night',     emoji: '🌙' }
}

function formatDate(value) {
  if (!value) return 'No due date'
  return new Date(value).toLocaleString()
}

function shortId(value) {
  return value ? value.slice(0, 8) : 'unknown'
}

export default function ChildHomePage() {
  const { session, user } = useAuth()

  const [assignments, setAssignments] = useState([])
  const [results, setResults]         = useState([])
  const [isLoading, setIsLoading]     = useState(true)
  const [error, setError]             = useState('')

  const [solvingAssignmentId, setSolvingAssignmentId] = useState(null)
  const [viewingResult, setViewingResult]             = useState(null)
  const [isLoadingResult, setIsLoadingResult]         = useState(false)

  useEffect(() => {
    let mounted = true

    async function load() {
      if (!session?.accessToken) {
        if (mounted) setIsLoading(false)
        return
      }
      try {
        const [a, r] = await Promise.all([
          getChildAssignments(session.accessToken),
          getChildResults(session.accessToken),
        ])
        if (mounted) {
          setAssignments(a)
          setResults(r)
        }
      } catch (err) {
        if (mounted) setError(err.message)
      } finally {
        if (mounted) setIsLoading(false)
      }
    }

    load()
    return () => { mounted = false }
  }, [session?.accessToken])

  const { text: greetText, emoji: greetEmoji } = getGreeting()
  const firstName = user?.displayName?.trim().split(/\s+/)[0]
    || user?.email?.split('@')[0]?.split(/[._]/)[0]
    || 'Explorer'
  const displayName = firstName.charAt(0).toUpperCase() + firstName.slice(1)

  const pendingAssignments   = assignments.filter((a) => a.status !== 'Completed')
  const completedAssignments = assignments.filter((a) => a.status === 'Completed')
  const pendingCount = pendingAssignments.length

  function handleAssignmentStatusChange(assignmentId, status) {
    setAssignments((cur) => cur.map((a) => a.id === assignmentId ? { ...a, status } : a))
    if (status === 'Completed') {
      getChildResults(session.accessToken).then(setResults).catch(() => {})
    }
  }

  async function handleViewResult(assignmentId) {
    if (!session?.accessToken) return
    const result = results.find((r) => r.assignmentId === assignmentId)
    if (!result) return
    setIsLoadingResult(true)
    setError('')
    try {
      const detail = await getChildResultDetail(session.accessToken, result.resultId)
      setViewingResult(detail)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoadingResult(false)
    }
  }

  return (
    <section className="child-dash-page">
      <div className="child-dashboard-bubbles" aria-hidden="true">
        <span className="child-dash-bubble d1">⭐</span>
        <span className="child-dash-bubble d2">🎈</span>
        <span className="child-dash-bubble d3">🌟</span>
        <span className="child-dash-bubble d4">✨</span>
        <span className="child-dash-bubble d5">🚀</span>
      </div>

      <div className="child-dash-hero">
        <div className="child-dash-hero-text">
          <div className="child-dash-kicker">{greetEmoji} {greetText}</div>
          <h2 className="child-dash-name">{displayName}!</h2>
          <p className="child-dash-sub">
            {pendingCount > 0
              ? `You have ${pendingCount} mission${pendingCount === 1 ? '' : 's'} waiting. Let's go! 💪`
              : 'All missions done for now. Great work! 🎉'}
          </p>
        </div>

        <div className="child-dash-hero-art" aria-hidden="true">
          <span className="hero-art-rocket">🚀</span>
          <span className="hero-art-planet">🪐</span>
          <span className="hero-art-star hs1">⭐</span>
          <span className="hero-art-star hs2">✨</span>
          <span className="hero-art-star hs3">🌟</span>
          <span className="hero-art-star hs4">💫</span>
        </div>
      </div>

      <ChildStatsPanel results={results} isLoading={isLoading} pendingCount={pendingCount} />

      {/* ── Missions waiting ── */}
      <div className="child-missions-card">
        <div className="children-list-header">
          <div>
            <h3>⏳ Missions waiting</h3>
            <p>Assigned — ready to start.</p>
          </div>
          <span className="badge">{isLoading ? '…' : pendingAssignments.length}</span>
        </div>

        {isLoading ? <p className="children-empty child-empty">Loading...</p> : null}
        {!isLoading && pendingAssignments.length === 0 ? (
          <p className="children-empty child-empty">All caught up! No missions waiting. 🎉</p>
        ) : null}

        {!isLoading && pendingAssignments.length > 0 ? (
          <div className="children-list">
            {pendingAssignments.map((assignment) => {
              const isInProgress = assignment.status === 'InProgress'
              const pillClass = isInProgress ? '' : 'status-new'
              const pillLabel = isInProgress ? '⚡ In progress' : '✨ New'
              const subjectIcon = SUBJECT_EMOJI[assignment.lessonSubject] || '📚'
              return (
                <article key={assignment.id} className="assignment-row">
                  <div className="assignment-copy">
                    <div className="assignment-topline">
                      <div className="child-name">
                        <span style={{ marginRight: '0.35em' }}>{subjectIcon}</span>
                        {assignment.lessonTitle || `Lesson ${shortId(assignment.lessonId)}`}
                      </div>
                      <span className={`assignment-status-pill ${pillClass}`}>{pillLabel}</span>
                    </div>
                    <div className="assignment-timeline">
                      <span className="assignment-meta-chip">Assigned {formatDate(assignment.assignedAt)}</span>
                      {assignment.dueDate ? (
                        <span className="assignment-meta-chip">Due {formatDate(assignment.dueDate)}</span>
                      ) : null}
                      {assignment.assignedByName ? (
                        <span className="assignment-meta-chip">By {assignment.assignedByName}</span>
                      ) : null}
                    </div>
                  </div>
                  <div className="button-row child-actions">
                    <button
                      type="button"
                      className="button-secondary child-start-button"
                      onClick={() => setSolvingAssignmentId(assignment.id)}
                    >
                      🚀 Start mission
                    </button>
                  </div>
                </article>
              )
            })}
          </div>
        ) : null}
      </div>

      {/* ── Missions done ── */}
      <div className="child-missions-card">
        <div className="children-list-header">
          <div>
            <h3>✅ Missions done</h3>
            <p>Completed this week.</p>
          </div>
          <span className="badge">{isLoading ? '…' : completedAssignments.length}</span>
        </div>

        {isLoading ? <p className="children-empty child-empty">Loading...</p> : null}
        {!isLoading && completedAssignments.length === 0 ? (
          <p className="children-empty child-empty">No completed missions yet. Go get some! 💪</p>
        ) : null}
        {error ? <div className="alert assignments-alert">{error}</div> : null}

        {!isLoading && completedAssignments.length > 0 ? (
          <div className="children-list">
            {completedAssignments.map((assignment) => {
              const resultForAssignment = results.find((r) => r.assignmentId === assignment.id)
              const subjectIcon = SUBJECT_EMOJI[assignment.lessonSubject] || '📚'
              return (
                <article key={assignment.id} className="assignment-row">
                  <div className="assignment-copy">
                    <div className="assignment-topline">
                      <div className="child-name">
                        <span style={{ marginRight: '0.35em' }}>{subjectIcon}</span>
                        {assignment.lessonTitle || `Lesson ${shortId(assignment.lessonId)}`}
                      </div>
                      {resultForAssignment ? (
                        <span className={`assignment-status-pill ${scoreVariant(resultForAssignment.score)}`}>
                          {scoreEmoji(resultForAssignment.score)} {resultForAssignment.score}%
                        </span>
                      ) : (
                        <span className="assignment-status-pill status-success">✅ Done</span>
                      )}
                    </div>
                    <div className="assignment-timeline">
                      <span className="assignment-meta-chip">Assigned {formatDate(assignment.assignedAt)}</span>
                      {assignment.dueDate ? (
                        <span className="assignment-meta-chip">Due {formatDate(assignment.dueDate)}</span>
                      ) : null}
                    </div>
                  </div>
                  <div className="button-row child-actions">
                    <button
                      type="button"
                      className="button-secondary child-start-button child-view-button"
                      disabled={isLoadingResult}
                      onClick={() => handleViewResult(assignment.id)}
                    >
                      {isLoadingResult ? '⏳' : '👁 View'}
                    </button>
                  </div>
                </article>
              )
            })}
          </div>
        ) : null}
      </div>

      {viewingResult ? (
        <LessonViewModal
          title={viewingResult.lessonTitle}
          subtitle={`${scoreEmoji(viewingResult.score)} ${viewingResult.score}% · ${viewingResult.correctAnswers}/${viewingResult.totalQuestions} correct`}
          questions={viewingResult.breakdown}
          onClose={() => setViewingResult(null)}
          renderQuestion={(item, index) => (
            <article key={item.questionId} className="assignment-row question-card">
              <div className="assignment-copy">
                <div className="assignment-topline">
                  <div className="child-name">Question {index + 1}</div>
                  <span className={`assignment-status-pill ${item.correct ? 'status-success' : 'status-danger'}`}>
                    {item.correct ? '✅ Correct' : '❌ Incorrect'}
                  </span>
                </div>
                <div>{item.questionText}</div>
                <div className="question-options">
                  {item.answers.map((answer) => {
                    const wasSelected = answer.answerId === item.selectedAnswerOptionId
                    const isCorrect = answer.isCorrect
                    let cls = 'question-option'
                    if (isCorrect) cls += ' correct-answer'
                    if (wasSelected && !isCorrect) cls += ' wrong-selected'
                    return (
                      <div key={answer.answerId} className={cls}>
                        <span>{answer.answerText}</span>
                        {isCorrect ? <span className="answer-correct-badge">✓ Correct</span> : null}
                        {wasSelected && !isCorrect ? <span className="answer-wrong-badge">✗ Your answer</span> : null}
                        {wasSelected && isCorrect ? <span className="answer-correct-badge">✓ Your answer</span> : null}
                      </div>
                    )
                  })}
                </div>
                {item.explanation ? (
                  <div className="child-meta">Explanation: {item.explanation}</div>
                ) : null}
              </div>
            </article>
          )}
          footer={(
            <div className="result-summary-footer">
              <div className={`mission-complete ${viewingResult.score >= 90 ? 'grade-perfect' : viewingResult.score >= 70 ? 'grade-great' : viewingResult.score >= 50 ? 'grade-good' : 'grade-ok'}`}
                style={{ paddingTop: '1rem', paddingBottom: '0.5rem' }}>
                <div className="mission-complete-emoji" aria-hidden="true">
                  {viewingResult.score >= 90 ? '🌟' : viewingResult.score >= 70 ? '🎊' : viewingResult.score >= 50 ? '👍' : '😊'}
                </div>
                <div className="mission-complete-title">
                  {viewingResult.score >= 90 ? 'Perfect!' : viewingResult.score >= 70 ? 'Great job!' : viewingResult.score >= 50 ? 'Good job!' : 'Keep trying!'}
                </div>
                <div className="mission-complete-score">
                  {viewingResult.score}% &nbsp;·&nbsp; {viewingResult.correctAnswers}/{viewingResult.totalQuestions} correct
                </div>
              </div>
              <div className="button-row modal-actions">
                <button type="button" className="button-secondary" onClick={() => setViewingResult(null)}>Close</button>
              </div>
            </div>
          )}
        />
      ) : null}

      <SolveMissionModal
        assignmentId={solvingAssignmentId}
        accessToken={session?.accessToken}
        onClose={() => setSolvingAssignmentId(null)}
        onAssignmentStatusChange={handleAssignmentStatusChange}
      />
    </section>
  )
}
