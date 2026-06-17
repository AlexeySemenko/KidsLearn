import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import {
  completeChildAssignment,
  getChildAssignmentForSolving,
  getChildAssignments,
  getChildResultDetail,
  getChildResults,
  submitChildAssignmentAnswers,
} from '../lib/api'
import LessonViewModal from '../components/LessonViewModal'
import ChildStatsPanel, { SUBJECT_EMOJI, scoreEmoji, scoreVariant } from '../components/ChildStatsPanel'

function getGreeting() {
  const h = new Date().getHours()
  if (h < 6)  return { text: 'Good night',     emoji: '🌙' }
  if (h < 12) return { text: 'Good morning',   emoji: '🌅' }
  if (h < 17) return { text: 'Good afternoon', emoji: '☀️' }
  if (h < 22) return { text: 'Good evening',   emoji: '🌤️' }
  return       { text: 'Good night',     emoji: '🌙' }
}

function completionGrade(score) {
  if (score >= 90) return { label: 'Perfect!',    emoji: '🌟', css: 'grade-perfect' }
  if (score >= 70) return { label: 'Great job!',  emoji: '🎊', css: 'grade-great' }
  if (score >= 50) return { label: 'Good job!',   emoji: '👍', css: 'grade-good' }
  return               { label: 'Keep trying!', emoji: '😊', css: 'grade-ok' }
}

function formatDate(value) {
  if (!value) return 'No due date'
  return new Date(value).toLocaleString()
}

function shortId(value) {
  return value ? value.slice(0, 8) : 'unknown'
}

export default function ChildHomePage() {
  const navigate = useNavigate()
  const { session, user } = useAuth()

  const [assignments, setAssignments] = useState([])
  const [results, setResults]         = useState([])
  const [isLoading, setIsLoading]     = useState(true)

  const [solvingAssignment, setSolvingAssignment] = useState(null)
  const [selectedAnswers, setSelectedAnswers]     = useState({})
  const [instantCheckMap, setInstantCheckMap]     = useState({})
  const [partialScore, setPartialScore]           = useState(null)
  const [completion, setCompletion]               = useState(null)
  const [checksRemaining, setChecksRemaining]     = useState(3)
  const [isOpening, setIsOpening]           = useState(false)
  const [isSubmitting, setIsSubmitting]     = useState(false)
  const [isCompleting, setIsCompleting]     = useState(false)
  const [error, setError]                   = useState('')
  const [viewingResult, setViewingResult]   = useState(null)
  const [isLoadingResult, setIsLoadingResult] = useState(false)

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

  const pendingCount = assignments.filter((a) => a.status !== 'Completed').length

  async function handleOpenAssignment(assignmentId) {
    if (!session?.accessToken) return
    setError('')
    setIsOpening(true)
    try {
      const response = await getChildAssignmentForSolving(session.accessToken, assignmentId)
      setSolvingAssignment(response)
      setSelectedAnswers({})
      setInstantCheckMap({})
      setPartialScore(null)
      setCompletion(null)
      setChecksRemaining(3)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsOpening(false)
    }
  }

  function handleSelectAnswer(questionId, answerId) {
    setSelectedAnswers((cur) => ({ ...cur, [questionId]: answerId }))
  }

  function closeSolvingModal() {
    setSolvingAssignment(null)
    setSelectedAnswers({})
    setInstantCheckMap({})
    setPartialScore(null)
    setCompletion(null)
    setChecksRemaining(3)
  }

  async function handleCheckAnswers() {
    if (!session?.accessToken || !solvingAssignment) return

    const answersPayload = Object.entries(selectedAnswers).map(([questionId, answerId]) => ({
      questionId,
      selectedAnswerOptionId: answerId,
      textAnswer: null,
    }))
    if (answersPayload.length === 0) {
      setError('Choose at least one answer before checking.')
      return
    }

    setError('')
    setIsSubmitting(true)
    try {
      const response = await submitChildAssignmentAnswers(session.accessToken, solvingAssignment.assignmentId, {
        answers: answersPayload,
      })
      const checkMap = {}
      response.instantCheck.forEach((item) => { checkMap[item.questionId] = item })
      setInstantCheckMap(checkMap)
      setPartialScore(response.partialScore)
      setChecksRemaining((n) => Math.max(0, n - 1))
      setAssignments((cur) => cur.map((a) =>
        a.id === solvingAssignment.assignmentId ? { ...a, status: 'InProgress' } : a
      ))
      setSolvingAssignment((cur) => cur ? { ...cur, status: 'InProgress' } : cur)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsSubmitting(false)
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

  async function handleCompleteAssignment() {
    if (!session?.accessToken || !solvingAssignment) return
    setError('')
    setIsCompleting(true)
    try {
      const response = await completeChildAssignment(session.accessToken, solvingAssignment.assignmentId)
      setCompletion(response)
      setAssignments((cur) => cur.map((a) =>
        a.id === solvingAssignment.assignmentId ? { ...a, status: 'Completed' } : a
      ))
      setSolvingAssignment((cur) => cur ? { ...cur, status: 'Completed' } : cur)
      getChildResults(session.accessToken).then(setResults).catch(() => {})
    } catch (err) {
      setError(err.message)
    } finally {
      setIsCompleting(false)
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

      <ChildStatsPanel results={results} isLoading={isLoading} />

      <div className="child-missions-card">
        <div className="children-list-header">
          <div>
            <h3>My missions</h3>
            <p>This week — last 7 days.</p>
          </div>
          <span className="badge">{assignments.length} this week</span>
        </div>

        {isLoading ? <p className="children-empty child-empty">Loading your missions...</p> : null}
        {!isLoading && assignments.length === 0 ? (
          <p className="children-empty child-empty">No missions this week. Check back soon!</p>
        ) : null}
        {error ? <div className="alert assignments-alert">{error}</div> : null}

        {!isLoading && assignments.length > 0 ? (
          <div className="children-list">
            {assignments.map((assignment) => {
              const isCompleted = assignment.status === 'Completed'
              const isInProgress = assignment.status === 'InProgress'

              let pillClass = 'status-new'
              let pillLabel = '✨ New'
              if (isInProgress) { pillClass = ''; pillLabel = '⚡ In progress' }
              if (isCompleted)  { pillClass = 'status-success'; pillLabel = '✅ Done' }

              const resultForAssignment = isCompleted ? results.find((r) => r.assignmentId === assignment.id) : null
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
                        <span className={`assignment-status-pill ${pillClass}`}>{pillLabel}</span>
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
                    {isCompleted ? (
                      <button
                        type="button"
                        className="button-secondary child-start-button child-view-button"
                        disabled={isLoadingResult}
                        onClick={() => handleViewResult(assignment.id)}
                      >
                        {isLoadingResult ? '⏳' : '👁 View'}
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="button-secondary child-start-button"
                        disabled={isOpening}
                        onClick={() => handleOpenAssignment(assignment.id)}
                      >
                        {isOpening ? '⏳ Opening...' : '🚀 Start mission'}
                      </button>
                    )}
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

      {solvingAssignment ? (
        <LessonViewModal
          title="Mission time!"
          subtitle={`${solvingAssignment.lessonTitle} · ${solvingAssignment.questions.length} questions`}
          questions={solvingAssignment.questions}
          onClose={closeSolvingModal}
          renderQuestion={(question, index) => (
            <article key={question.questionId} className="assignment-row question-card">
              <div className="assignment-copy">
                <div className="assignment-topline">
                  <div className="child-name">Question {index + 1}</div>
                  {instantCheckMap[question.questionId] ? (
                    <span className={`assignment-status-pill ${instantCheckMap[question.questionId].correct ? 'status-success' : 'status-danger'}`}>
                      {instantCheckMap[question.questionId].correct ? '✅ Nice Job!' : '❌ Try once more'}
                    </span>
                  ) : null}
                </div>
                <div>{question.questionText}</div>
                <div className="question-options">
                  {question.answers.map((answer) => (
                    <label key={answer.answerId} className="question-option">
                      <input
                        type="radio"
                        name={`question-${question.questionId}`}
                        checked={selectedAnswers[question.questionId] === answer.answerId}
                        onChange={() => handleSelectAnswer(question.questionId, answer.answerId)}
                      />
                      <span>{answer.answerText}</span>
                    </label>
                  ))}
                </div>
                {instantCheckMap[question.questionId] ? (
                  <div className="child-meta">Explanation: {instantCheckMap[question.questionId].explanation}</div>
                ) : null}
              </div>
            </article>
          )}
          footer={(
            <>
              {completion ? (() => {
                const grade = completionGrade(completion.score)
                return (
                  <div className={`mission-complete ${grade.css}`}>
                    <div className="mission-complete-emoji" aria-hidden="true">{grade.emoji}</div>
                    <div className="mission-complete-title">{grade.label}</div>
                    <div className="mission-complete-score">
                      {completion.score}% &nbsp;·&nbsp; {completion.correctAnswers}/{completion.totalQuestions} correct
                    </div>
                    {grade.css === 'grade-perfect' ? (
                      <div className="mission-confetti" aria-hidden="true">
                        {['⭐','🌟','✨','💫','⭐','🌟','✨','💫','⭐','🌟'].map((s, i) => (
                          <span key={i} className={`confetti-star s${i + 1}`}>{s}</span>
                        ))}
                      </div>
                    ) : null}
                    <div className="button-row modal-actions">
                      <button
                        type="button"
                        className="button"
                        onClick={() => navigate(`/child/results?resultId=${completion.resultId}`)}
                      >
                        View my results
                      </button>
                      <button type="button" className="button-secondary" onClick={closeSolvingModal}>Close</button>
                    </div>
                  </div>
                )
              })() : (
                <>
                  {partialScore !== null ? (
                    <div className="mission-score-bar">
                      <span className="mission-score-emoji">{scoreEmoji(partialScore)}</span>
                      <span className="mission-score-value">{partialScore}%</span>
                      <span className="mission-score-label">current score</span>
                      <span className="mission-checks-left">
                        {checksRemaining > 0
                          ? `${checksRemaining} check${checksRemaining === 1 ? '' : 's'} left`
                          : 'No checks left'}
                      </span>
                    </div>
                  ) : null}
                  <div className="button-row modal-actions">
                    <button
                      type="button"
                      className="button-secondary"
                      disabled={isSubmitting || isCompleting || checksRemaining === 0}
                      onClick={handleCheckAnswers}
                    >
                      {isSubmitting ? 'Checking...' : checksRemaining === 0 ? 'No checks left' : `Check my answers (${checksRemaining} left)`}
                    </button>
                    <button
                      type="button"
                      className="button"
                      disabled={isSubmitting || isCompleting || solvingAssignment.status === 'Completed'}
                      onClick={handleCompleteAssignment}
                    >
                      {isCompleting ? 'Finishing...' : (solvingAssignment.status === 'Completed' ? 'Completed' : 'Finish mission')}
                    </button>
                  </div>
                </>
              )}
            </>
          )}
        />
      ) : null}
    </section>
  )
}
