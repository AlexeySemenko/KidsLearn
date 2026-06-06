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
                    className="button-secondary"
                    disabled={isOpening}
                    onClick={() => handleOpenAssignment(assignment.id)}
                  >
                    {isOpening ? 'Opening...' : 'Solve'}
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
                <h3 id="solve-assignment-title">Solve assignment</h3>
                <p>{solvingAssignment.lessonTitle} · {solvingAssignment.questions.length} questions</p>
              </div>
              <button type="button" className="button-secondary" onClick={closeSolvingModal}>Close</button>
            </div>

            <div className="children-list">
              {solvingAssignment.questions.map((question, index) => (
                <article key={question.questionId} className="assignment-row question-card">
                  <div className="assignment-copy">
                    <div className="assignment-topline">
                      <div className="child-name">Question {index + 1}</div>
                      {instantCheckMap[question.questionId] ? (
                        <span className={`assignment-status-pill ${instantCheckMap[question.questionId].correct ? 'status-success' : 'status-danger'}`}>
                          {instantCheckMap[question.questionId].correct ? 'Correct' : 'Try again'}
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
                <strong>Current score</strong>
                <span>{partialScore}%</span>
              </div>
            ) : null}

            {completion ? (
              <div className="info-block success-block assignments-status-block">
                <strong>Assignment completed</strong>
                <span>Score: {completion.score}% · Correct: {completion.correctAnswers}/{completion.totalQuestions}</span>
                <div className="button-row">
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={() => navigate(`/child/results?resultId=${completion.resultId}`)}
                  >
                    View result details
                  </button>
                </div>
              </div>
            ) : null}

            <div className="button-row modal-actions">
              <button type="button" className="button-secondary" disabled={isSubmitting || isCompleting} onClick={handleCheckAnswers}>
                {isSubmitting ? 'Checking...' : 'Check answers'}
              </button>
              <button type="button" className="button" disabled={isSubmitting || isCompleting || solvingAssignment.status === 'Completed'} onClick={handleCompleteAssignment}>
                {isCompleting ? 'Completing...' : (solvingAssignment.status === 'Completed' ? 'Completed' : 'Complete assignment')}
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </section>
  )
}
