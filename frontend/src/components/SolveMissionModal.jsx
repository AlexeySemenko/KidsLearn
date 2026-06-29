import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  completeChildAssignment,
  getChildAssignmentForSolving,
  getChildAssignmentStoryImage,
  submitChildAssignmentAnswers,
} from '../lib/api'
import { scoreEmoji } from './ChildStatsPanel'
import LessonViewModal from './LessonViewModal'

function completionGrade(score) {
  if (score >= 90) return { label: 'Perfect!',    emoji: '🌟', css: 'grade-perfect' }
  if (score >= 70) return { label: 'Great job!',  emoji: '🎊', css: 'grade-great' }
  if (score >= 50) return { label: 'Good job!',   emoji: '👍', css: 'grade-good' }
  return               { label: 'Keep trying!', emoji: '😊', css: 'grade-ok' }
}

/**
 * Self-contained mission solving modal.
 *
 * Props:
 *  - assignmentId: string | null  — pass null / undefined to hide the modal
 *  - accessToken: string
 *  - onClose: () => void
 *  - onAssignmentStatusChange?: (assignmentId: string, status: string) => void
 *    called when the assignment transitions to InProgress or Completed
 */
export default function SolveMissionModal({ assignmentId, accessToken, onClose, onAssignmentStatusChange }) {
  const navigate = useNavigate()

  const [assignment, setAssignment]         = useState(null)
  const [storyImage, setStoryImage]         = useState(null)
  const [isLoading, setIsLoading]           = useState(false)
  const [selectedAnswers, setSelectedAnswers]   = useState({})
  const [instantCheckMap, setInstantCheckMap]   = useState({})
  const [partialScore, setPartialScore]         = useState(null)
  const [completion, setCompletion]             = useState(null)
  const [checksRemaining, setChecksRemaining]   = useState(3)
  const [isSubmitting, setIsSubmitting]         = useState(false)
  const [isCompleting, setIsCompleting]         = useState(false)
  const [error, setError]                       = useState('')

  useEffect(() => {
    if (!assignmentId || !accessToken) return
    let mounted = true
    setIsLoading(true)
    setAssignment(null)
    setStoryImage(null)
    setSelectedAnswers({})
    setInstantCheckMap({})
    setPartialScore(null)
    setCompletion(null)
    setChecksRemaining(3)
    setError('')

    getChildAssignmentForSolving(accessToken, assignmentId)
      .then((data) => {
        if (!mounted) return
        setAssignment(data)
        setIsLoading(false)
        if (data.hasLessonStoryImage) {
          getChildAssignmentStoryImage(accessToken, assignmentId)
            .then((img) => { if (mounted) setStoryImage(img?.storyImageUrl ?? null) })
            .catch(() => {})
        }
      })
      .catch((err) => { if (mounted) { setError(err.message); setIsLoading(false) } })

    return () => { mounted = false }
  }, [assignmentId, accessToken])

  if (!assignmentId) return null

  function handleSelectAnswer(questionId, answerId) {
    setSelectedAnswers((cur) => ({ ...cur, [questionId]: answerId }))
  }

  async function handleCheckAnswers() {
    if (!accessToken || !assignment) return
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
      const response = await submitChildAssignmentAnswers(accessToken, assignment.assignmentId, { answers: answersPayload })
      const checkMap = {}
      response.instantCheck.forEach((item) => { checkMap[item.questionId] = item })
      setInstantCheckMap(checkMap)
      setPartialScore(response.partialScore)
      setChecksRemaining((n) => Math.max(0, n - 1))
      onAssignmentStatusChange?.(assignment.assignmentId, 'InProgress')
      setAssignment((cur) => cur ? { ...cur, status: 'InProgress' } : cur)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleCompleteAssignment() {
    if (!accessToken || !assignment) return
    setError('')
    setIsCompleting(true)
    try {
      // Always submit the current answer selections before scoring so the
      // backend has up-to-date answers even if the child skipped "Check".
      const answersPayload = Object.entries(selectedAnswers).map(([questionId, answerId]) => ({
        questionId,
        selectedAnswerOptionId: answerId,
        textAnswer: null,
      }))
      if (answersPayload.length > 0) {
        await submitChildAssignmentAnswers(accessToken, assignment.assignmentId, { answers: answersPayload })
      }
      const response = await completeChildAssignment(accessToken, assignment.assignmentId)
      setCompletion(response)
      onAssignmentStatusChange?.(assignment.assignmentId, 'Completed')
      setAssignment((cur) => cur ? { ...cur, status: 'Completed' } : cur)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsCompleting(false)
    }
  }

  // Loading / error state — show modal shell while fetching
  if (isLoading || !assignment) {
    return (
      <LessonViewModal
        title="Mission time!"
        subtitle={isLoading ? 'Loading…' : (error || 'Something went wrong.')}
        questions={[]}
        onClose={onClose}
        footer={
          <div className="button-row modal-actions">
            <button type="button" className="button-secondary" onClick={onClose}>Cancel</button>
          </div>
        }
      />
    )
  }

  return (
    <LessonViewModal
      title="Mission time!"
      subtitle={`${assignment.lessonTitle} · ${assignment.questions.length} questions`}
      story={assignment.lessonStory}
      storyImageUrl={storyImage}
      questions={assignment.questions}
      onClose={onClose}
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
          {error ? <div className="alert" style={{ margin: '0 0 0.5rem' }}>{error}</div> : null}
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
                  <button type="button" className="button-secondary" onClick={onClose}>Close</button>
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
                  disabled={isSubmitting || isCompleting || assignment.status === 'Completed'}
                  onClick={handleCompleteAssignment}
                >
                  {isCompleting ? 'Finishing...' : assignment.status === 'Completed' ? 'Completed' : 'Finish mission'}
                </button>
              </div>
            </>
          )}
        </>
      )}
    />
  )
}
