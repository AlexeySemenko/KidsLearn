import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { getChildAssignments, getChildResultDetail, getChildResults } from '../lib/api'
import LessonViewModal from '../components/LessonViewModal'
import SolveMissionModal from '../components/SolveMissionModal'
import { SUBJECT_EMOJI, scoreEmoji, scoreVariant } from '../components/ChildStatsPanel'

function formatDate(value) {
  if (!value) return 'Unknown date'
  return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

export default function ChildResultsPage() {
  const { session } = useAuth()
  const [assignments, setAssignments] = useState([])
  const [results, setResults] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [viewingResult, setViewingResult] = useState(null)
  const [isLoadingDetail, setIsLoadingDetail] = useState(false)
  const [solvingAssignmentId, setSolvingAssignmentId] = useState(null)

  useEffect(() => {
    let isMounted = true

    async function load() {
      if (!session?.accessToken) {
        if (isMounted) setIsLoading(false)
        return
      }

      try {
        const [a, r] = await Promise.all([
          getChildAssignments(session.accessToken),
          getChildResults(session.accessToken),
        ])
        if (isMounted) {
          setAssignments(a)
          setResults(r)
        }
      } catch (err) {
        if (isMounted) setError(err.message)
      } finally {
        if (isMounted) setIsLoading(false)
      }
    }

    load()
    return () => { isMounted = false }
  }, [session?.accessToken])

  function handleAssignmentStatusChange(assignmentId, status) {
    setAssignments((cur) => cur.map((a) => a.id === assignmentId ? { ...a, status } : a))
    if (status === 'Completed') {
      getChildResults(session.accessToken).then(setResults).catch(() => {})
    }
  }

  async function handleView(resultId) {
    if (!session?.accessToken) return
    setIsLoadingDetail(true)
    setError('')
    try {
      const detail = await getChildResultDetail(session.accessToken, resultId)
      setViewingResult(detail)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoadingDetail(false)
    }
  }

  const pendingAssignments = assignments.filter((a) => a.status !== 'Completed')

  return (
    <section className="panel-grid child-panel-grid">

      {/* ── Waiting lessons ── */}
      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>⏳ Waiting lessons</h3>
            <p>Assigned — ready to start.</p>
          </div>
          <span className="badge">{isLoading ? '…' : pendingAssignments.length} waiting</span>
        </div>

        {isLoading ? <p className="children-empty child-empty">Loading...</p> : null}
        {!isLoading && pendingAssignments.length === 0 ? (
          <p className="children-empty child-empty">No missions waiting. All caught up! 🎉</p>
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
                        {assignment.lessonTitle}
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
      </article>

      {/* ── Completed lessons ── */}
      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>My lessons</h3>
            <p>All completed missions and your scores.</p>
          </div>
          <span className="badge">{results.length} completed</span>
        </div>

        {isLoading ? <p className="children-empty child-empty">Loading results...</p> : null}
        {!isLoading && results.length === 0 && !error ? (
          <p className="children-empty child-empty">No completed missions yet. Finish a mission to see your results here.</p>
        ) : null}
        {error ? <div className="alert assignments-alert">{error}</div> : null}

        {!isLoading && results.length > 0 ? (
          <div className="children-list">
            {results.map((result) => (
              <article key={result.resultId} className="assignment-row">
                <div className="assignment-copy">
                  <div className="assignment-topline">
                    <div className="child-name">{result.lessonTitle}</div>
                    <span className={`assignment-status-pill ${scoreVariant(result.score)}`}>
                      {scoreEmoji(result.score)} {result.score}%
                    </span>
                  </div>
                  <div className="assignment-timeline">
                    <span className="assignment-meta-chip">Completed {formatDate(result.completedAt)}</span>
                    <span className="assignment-meta-chip">{result.correctAnswers}/{result.totalQuestions} correct</span>
                  </div>
                </div>
                <div className="button-row child-actions">
                  <button
                    type="button"
                    className="button-secondary child-start-button"
                    disabled={isLoadingDetail}
                    onClick={() => handleView(result.resultId)}
                  >
                    {isLoadingDetail ? '⏳' : '📋 View result'}
                  </button>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </article>

      <SolveMissionModal
        assignmentId={solvingAssignmentId}
        accessToken={session?.accessToken}
        onClose={() => setSolvingAssignmentId(null)}
        onAssignmentStatusChange={handleAssignmentStatusChange}
      />

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
                  &nbsp;·&nbsp; {formatDate(viewingResult.completedAt)}
                </div>
              </div>
              <div className="button-row modal-actions">
                <button type="button" className="button-secondary" onClick={() => setViewingResult(null)}>Close</button>
              </div>
            </div>
          )}
        />
      ) : null}
    </section>
  )
}
