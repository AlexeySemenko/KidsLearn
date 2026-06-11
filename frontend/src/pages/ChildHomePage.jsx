import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import {
  completeChildAssignment,
  getChildAssignmentForSolving,
  getChildAssignments,
  submitChildAssignmentAnswers,
} from '../lib/api'

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
  const navigate = useNavigate()
  const { session } = useAuth()
  const [assignments, setAssignments] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [solvingAssignment, setSolvingAssignment] = useState(null)
  const [selectedAnswers, setSelectedAnswers] = useState({})
  const [instantCheckMap, setInstantCheckMap] = useState({})
  const [partialScore, setPartialScore] = useState(null)
  const [completion, setCompletion] = useState(null)
  const [isOpening, setIsOpening] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isCompleting, setIsCompleting] = useState(false)

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

  useEffect(() => {
    if (!solvingAssignment) {
      return undefined
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        closeSolvingModal()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [solvingAssignment])

  async function handleOpenAssignment(assignmentId) {
    if (!session?.accessToken) {
      return
    }

    setError('')
    setIsOpening(true)

    try {
      const response = await getChildAssignmentForSolving(session.accessToken, assignmentId)
      setSolvingAssignment(response)
      setSelectedAnswers({})
      setInstantCheckMap({})
      setPartialScore(null)
      setCompletion(null)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsOpening(false)
    }
  }

  function handleSelectAnswer(questionId, answerId) {
    setSelectedAnswers((current) => ({ ...current, [questionId]: answerId }))
  }

  function closeSolvingModal() {
    setSolvingAssignment(null)
    setSelectedAnswers({})
    setInstantCheckMap({})
    setPartialScore(null)
    setCompletion(null)
  }

  async function handleCheckAnswers() {
    if (!session?.accessToken || !solvingAssignment) {
      return
    }

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
      response.instantCheck.forEach((item) => {
        checkMap[item.questionId] = item
      })

      setInstantCheckMap(checkMap)
      setPartialScore(response.partialScore)
      setAssignments((current) => current.map((item) => (
        item.id === solvingAssignment.assignmentId ? { ...item, status: 'InProgress' } : item
      )))
      setSolvingAssignment((current) => (current ? { ...current, status: 'InProgress' } : current))
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleCompleteAssignment() {
    if (!session?.accessToken || !solvingAssignment) {
      return
    }

    setError('')
    setIsCompleting(true)

    try {
      const response = await completeChildAssignment(session.accessToken, solvingAssignment.assignmentId)
      setCompletion(response)
      setAssignments((current) => current.map((item) => (
        item.id === solvingAssignment.assignmentId ? { ...item, status: 'Completed' } : item
      )))
      setSolvingAssignment((current) => (current ? { ...current, status: 'Completed' } : current))
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsCompleting(false)
    }
  }

  return (
    <section className="panel-grid child-panel-grid">
      <div className="child-dashboard-bubbles" aria-hidden="true">
        <span className="child-dash-bubble d1">⭐</span>
        <span className="child-dash-bubble d2">🎈</span>
        <span className="child-dash-bubble d3">🌟</span>
        <span className="child-dash-bubble d4">✨</span>
        <span className="child-dash-bubble d5">🚀</span>
      </div>

      <article className="hero-card assignments-hero child-assignments-hero">
        <div className="child-hero-topline">
          <div>
            <div className="brand-kicker">Mission board</div>
            <h2>Ready for today&apos;s learning adventure?</h2>
            <p>
              Your assignments are waiting. Open one mission, solve the questions,
              and earn your score.
            </p>
          </div>

          <div className="child-hero-mascot" aria-hidden="true">
            <svg viewBox="0 0 120 120" fill="none" xmlns="http://www.w3.org/2000/svg">
              <circle cx="60" cy="48" r="26" fill="#f4d35e" />
              <ellipse cx="60" cy="82" rx="28" ry="20" fill="#f4d35e" />
              <circle cx="51" cy="44" r="5" fill="#0f2745" />
              <circle cx="69" cy="44" r="5" fill="#0f2745" />
              <circle cx="53" cy="42" r="2" fill="white" />
              <circle cx="71" cy="42" r="2" fill="white" />
              <path d="M50 56 Q60 65 70 56" stroke="#0f2745" strokeWidth="2.5" strokeLinecap="round" fill="none" />
              <rect x="38" y="22" width="44" height="7" rx="3.5" fill="#1a3a5c" />
              <polygon points="60,13 82,25 38,25" fill="#1a3a5c" />
              <line x1="82" y1="25" x2="86" y2="36" stroke="#1a3a5c" strokeWidth="2" />
              <circle cx="86" cy="38" r="3.5" fill="#ffb703" />
            </svg>
          </div>
        </div>

        <div className="badge-row child-sticker-row">
          <span className="badge child-sticker">Math + Reading</span>
          <span className="badge child-sticker">Try your best</span>
          <span className="badge child-sticker">Level up daily</span>
        </div>
      </article>

      <article className="panel-card assignments-form-card child-metric-card">
        <h3>Your mission counter</h3>
        <p>See how many assignments you can finish today.</p>
        <div className="metric">{assignments.length}</div>
        <div className="metric-copy">Missions in your queue</div>
      </article>

      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>My missions</h3>
            <p>Pick one and start solving.</p>
          </div>
          <span className="badge">{assignments.length} records</span>
        </div>

        {isLoading ? <p className="children-empty child-empty">Loading your missions...</p> : null}
        {!isLoading && assignments.length === 0 ? <p className="children-empty child-empty">No missions yet. Check back soon.</p> : null}
        {error ? <div className="alert assignments-alert">{error}</div> : null}

        {!isLoading && assignments.length > 0 ? (
          <div className="children-list">
            {assignments.map((assignment) => (
              <article key={assignment.id} className="assignment-row">
                <div className="assignment-copy">
                  <div className="assignment-topline">
                    <div className="child-name">{assignment.lessonTitle || `Lesson ${shortId(assignment.lessonId)}`}</div>
                    <span className="assignment-status-pill">{assignment.status}</span>
                  </div>

                  <div className="child-meta">Assignment {shortId(assignment.id)}</div>

                  <div className="assignment-timeline">
                    <span className="assignment-meta-chip">Assigned {formatDate(assignment.assignedAt)}</span>
                    <span className="assignment-meta-chip">Due {formatDate(assignment.dueDate)}</span>
                  </div>
                </div>

                <div className="button-row child-actions">
                  <button
                    type="button"
                    className="button-secondary child-start-button"
                    disabled={isOpening}
                    onClick={() => handleOpenAssignment(assignment.id)}
                  >
                    {isOpening ? '⏳ Opening...' : '🚀 Start mission'}
                  </button>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </article>

      {solvingAssignment ? (
        <div className="modal-overlay" role="presentation" onClick={closeSolvingModal}>
          <section className="modal-card lesson-modal" role="dialog" aria-modal="true" aria-labelledby="solve-assignment-title" onClick={(event) => event.stopPropagation()}>
            <div className="children-list-header modal-header">
              <div>
                <h3 id="solve-assignment-title">Mission time!</h3>
                <p>{solvingAssignment.lessonTitle} · {solvingAssignment.questions.length} questions</p>
              </div>
              <button type="button" className="button-secondary" onClick={closeSolvingModal}>Back</button>
            </div>

            <div className="children-list">
              {solvingAssignment.questions.map((question, index) => (
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
              ))}
            </div>

            {partialScore !== null ? (
              <div className="info-block success-block assignments-status-block">
                <strong>Your score right now</strong>
                <span>{partialScore}%</span>
              </div>
            ) : null}

            {completion ? (
              <div className="info-block success-block assignments-status-block">
                <strong>Mission completed</strong>
                <span>Score: {completion.score}% · Correct: {completion.correctAnswers}/{completion.totalQuestions}</span>
                <div className="button-row">
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={() => navigate(`/child/results?resultId=${completion.resultId}`)}
                  >
                    View my results
                  </button>
                </div>
              </div>
            ) : null}

            <div className="button-row modal-actions">
              <button type="button" className="button-secondary" disabled={isSubmitting || isCompleting} onClick={handleCheckAnswers}>
                {isSubmitting ? 'Checking...' : 'Check my answers'}
              </button>
              <button type="button" className="button" disabled={isSubmitting || isCompleting || solvingAssignment.status === 'Completed'} onClick={handleCompleteAssignment}>
                {isCompleting ? 'Finishing...' : (solvingAssignment.status === 'Completed' ? 'Completed' : 'Finish mission')}
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </section>
  )
}
