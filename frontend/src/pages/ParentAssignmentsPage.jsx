import { useEffect, useMemo, useState } from 'react'
import {
  createAssignment,
  getAssignments,
  getChildren,
  getLessons,
  getParentAssignmentForSolving,
  getParentResultDetail,
} from '../lib/api'
import { useAuth } from '../auth/AuthProvider'
import LessonViewModal from '../components/LessonViewModal'
import CreateAssignmentModal from '../components/CreateAssignmentModal'
import { SUBJECT_EMOJI, scoreEmoji, scoreVariant } from '../components/ChildStatsPanel'

const STATUS_FILTER_OPTIONS = [
  { value: '',           label: 'All statuses' },
  { value: 'Assigned',   label: '✨ Assigned' },
  { value: 'InProgress', label: '⚡ In progress' },
  { value: 'Completed',  label: '✅ Completed' },
]

const STATUS_PILL = {
  Assigned:   { cls: 'status-new',     label: '✨ Assigned' },
  InProgress: { cls: '',               label: '⚡ In progress' },
  Completed:  { cls: 'status-success', label: '✅ Completed' },
}

function formatDate(value) {
  if (!value) return null
  return new Date(value).toLocaleString()
}

export default function ParentAssignmentsPage() {
  const { session } = useAuth()
  const [assignments, setAssignments] = useState([])
  const [children, setChildren]       = useState([])
  const [lessons, setLessons]         = useState([])
  const [isLoading, setIsLoading]     = useState(true)
  const [error, setError]             = useState('')

  const [filterStatus, setFilterStatus] = useState('')
  const [filterChild, setFilterChild]   = useState('')

  const [showCreateModal, setShowCreateModal] = useState(false)
  const [isCreating, setIsCreating]           = useState(false)
  const [createError, setCreateError]         = useState('')

  const [reviewAssignment, setReviewAssignment] = useState(null)
  const [reviewResult, setReviewResult]         = useState(null)
  const [isLoadingReview, setIsLoadingReview]   = useState(false)

  useEffect(() => {
    let mounted = true
    async function load() {
      if (!session?.accessToken) { setIsLoading(false); return }
      try {
        const [a, ch, ls] = await Promise.all([
          getAssignments(session.accessToken),
          getChildren(session.accessToken),
          getLessons(session.accessToken),
        ])
        if (mounted) {
          setAssignments(a)
          setChildren(ch)
          setLessons(ls.items)
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

  const childFilterOptions = useMemo(() => [
    { value: '', label: 'All children' },
    ...children.map((c) => ({ value: c.id, label: c.name })),
  ], [children])

  async function handleCreate(payload) {
    setIsCreating(true)
    setCreateError('')
    try {
      const result = await createAssignment(session.accessToken, payload)
      setAssignments((cur) => [result, ...cur])
      setShowCreateModal(false)
    } catch (err) {
      setCreateError(err.message)
    } finally {
      setIsCreating(false)
    }
  }

  async function handleReview(assignment) {
    if (!session?.accessToken) return
    setIsLoadingReview(true)
    setError('')
    try {
      if (assignment.resultId) {
        const result = await getParentResultDetail(session.accessToken, assignment.resultId)
        setReviewResult(result)
      } else {
        const detail = await getParentAssignmentForSolving(session.accessToken, assignment.id)
        setReviewAssignment(detail)
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoadingReview(false)
    }
  }

  const filtered = useMemo(() => assignments.filter((a) => {
    if (filterStatus && a.status !== filterStatus) return false
    if (filterChild && a.childId !== filterChild) return false
    return true
  }), [assignments, filterStatus, filterChild])

  return (
    <section className="assignments-page">
      <div className="children-list-header">
        <div>
          <h2>Assignments</h2>
          <p>Active assignments + completed last 7 days.</p>
        </div>
        <button
          type="button"
          className="button"
          onClick={() => { setShowCreateModal(true); setCreateError('') }}
        >
          + Create assignment
        </button>
      </div>

      {error ? <div className="alert">{error}</div> : null}

      <div className="admin-filter-bar">
        <select
          className="admin-filter-input"
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value)}
          aria-label="Filter by status"
          style={{ flex: '0 1 180px' }}
        >
          {STATUS_FILTER_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
        <select
          className="admin-filter-input"
          value={filterChild}
          onChange={(e) => setFilterChild(e.target.value)}
          aria-label="Filter by child"
          style={{ flex: '0 1 180px' }}
        >
          {childFilterOptions.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
        <span className="badge" style={{ alignSelf: 'center', marginLeft: 'auto' }}>
          {filtered.length} assignment{filtered.length !== 1 ? 's' : ''}
        </span>
      </div>

      {isLoading ? (
        <p className="children-empty">Loading assignments...</p>
      ) : null}

      {!isLoading && filtered.length === 0 ? (
        <p className="children-empty">
          {assignments.length === 0
            ? 'No assignments yet. Create the first one!'
            : 'No assignments match the current filters.'}
        </p>
      ) : null}

      {!isLoading && filtered.length > 0 ? (
        <div className="children-list">
          {filtered.map((assignment) => {
            const pill = STATUS_PILL[assignment.status] ?? { cls: '', label: assignment.status }
            const subjectIcon = SUBJECT_EMOJI[assignment.lessonSubject] || '📚'
            const childName = assignment.childName
              || children.find((c) => c.id === assignment.childId)?.name
              || '—'
            const assigned = formatDate(assignment.assignedAt)
            const due = assignment.dueDate ? formatDate(assignment.dueDate) : null

            return (
              <article key={assignment.id} className="assignment-row">
                <div className="assignment-copy">
                  <div className="assignment-topline">
                    <div className="child-name">{childName}</div>
                    {assignment.score != null ? (
                      <span className={`assignment-status-pill ${scoreVariant(assignment.score)}`}>
                        {scoreEmoji(assignment.score)} {assignment.score}%
                      </span>
                    ) : (
                      <span className={`assignment-status-pill ${pill.cls}`}>{pill.label}</span>
                    )}
                  </div>
                  <div className="assignment-lesson-title">
                    <span style={{ marginRight: '0.35em' }}>{subjectIcon}</span>
                    {assignment.lessonTitle}
                  </div>
                  <div className="assignment-timeline">
                    {assigned ? <span className="assignment-meta-chip">Assigned {assigned}</span> : null}
                    {due ? <span className="assignment-meta-chip">Due {due}</span> : null}
                  </div>
                </div>
                <div className="button-row child-actions">
                  <button
                    type="button"
                    className="button-secondary child-start-button"
                    style={{ fontSize: '0.8rem', padding: '0.35rem 0.75rem' }}
                    disabled={isLoadingReview}
                    onClick={() => handleReview(assignment)}
                  >
                    {isLoadingReview ? '⏳' : '🔍 Review'}
                  </button>
                </div>
              </article>
            )
          })}
        </div>
      ) : null}

      {showCreateModal ? (
        <CreateAssignmentModal
          children={children}
          lessons={lessons}
          onSave={handleCreate}
          onClose={() => setShowCreateModal(false)}
          isSaving={isCreating}
          error={createError}
        />
      ) : null}

      {reviewAssignment ? (
        <LessonViewModal
          title={`Review: ${reviewAssignment.lessonTitle}`}
          subtitle={`${reviewAssignment.questions.length} question${reviewAssignment.questions.length !== 1 ? 's' : ''}`}
          questions={reviewAssignment.questions}
          onClose={() => setReviewAssignment(null)}
          renderQuestion={(question, index) => (
            <article key={question.questionId} className="assignment-row question-card">
              <div className="assignment-copy">
                <div className="assignment-topline">
                  <div className="child-name">Question {index + 1}</div>
                </div>
                <div>{question.questionText}</div>
                {question.explanation ? (
                  <div className="child-meta">Explanation: {question.explanation}</div>
                ) : null}
                <div className="question-options">
                  {question.answers.map((answer) => (
                    <div
                      key={answer.answerId}
                      className={`question-option${answer.isCorrect ? ' correct-answer' : ''}`}
                    >
                      <span>{answer.answerText}</span>
                      {answer.isCorrect ? <span className="answer-correct-badge">✓ Correct</span> : null}
                    </div>
                  ))}
                </div>
              </div>
            </article>
          )}
          footer={(
            <div className="button-row modal-actions">
              <button type="button" className="button-secondary" onClick={() => setReviewAssignment(null)}>Close</button>
            </div>
          )}
        />
      ) : null}

      {reviewResult ? (
        <LessonViewModal
          title={reviewResult.lessonTitle}
          subtitle={`${scoreEmoji(reviewResult.score)} ${reviewResult.score}% · ${reviewResult.correctAnswers}/${reviewResult.totalQuestions} correct`}
          questions={reviewResult.breakdown}
          onClose={() => setReviewResult(null)}
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
                        {wasSelected && !isCorrect ? <span className="answer-wrong-badge">✗ Child's answer</span> : null}
                        {wasSelected && isCorrect ? <span className="answer-correct-badge">✓ Child's answer</span> : null}
                      </div>
                    )
                  })}
                </div>
              </div>
            </article>
          )}
          footer={(
            <div className="result-summary-footer">
              <div className={`mission-complete ${reviewResult.score >= 90 ? 'grade-perfect' : reviewResult.score >= 70 ? 'grade-great' : reviewResult.score >= 50 ? 'grade-good' : 'grade-ok'}`}
                style={{ paddingTop: '1rem', paddingBottom: '0.5rem' }}>
                <div className="mission-complete-emoji" aria-hidden="true">
                  {reviewResult.score >= 90 ? '🌟' : reviewResult.score >= 70 ? '🎊' : reviewResult.score >= 50 ? '👍' : '😊'}
                </div>
                <div className="mission-complete-title">
                  {reviewResult.score >= 90 ? 'Perfect!' : reviewResult.score >= 70 ? 'Great job!' : reviewResult.score >= 50 ? 'Good job!' : 'Keep trying!'}
                </div>
                <div className="mission-complete-score">
                  {reviewResult.score}% &nbsp;·&nbsp; {reviewResult.correctAnswers}/{reviewResult.totalQuestions} correct
                </div>
              </div>
              <div className="button-row modal-actions">
                <button type="button" className="button-secondary" onClick={() => setReviewResult(null)}>Close</button>
              </div>
            </div>
          )}
        />
      ) : null}
    </section>
  )
}
