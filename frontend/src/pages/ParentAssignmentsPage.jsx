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
import DropdownSelect from '../components/DropdownSelect'

const emptyAssignmentForm = {
  childId: '',
  lessonId: '',
  dueDate: '',
}

function validateAssignmentForm(form) {
  if (!form.childId) {
    return 'Select a child.'
  }

  if (!form.lessonId) {
    return 'Select a lesson.'
  }

  return null
}

function formatDate(value) {
  if (!value) {
    return 'No due date'
  }

  return new Date(value).toLocaleString()
}

export default function ParentAssignmentsPage() {
  const { session } = useAuth()
  const [assignments, setAssignments] = useState([])
  const [children, setChildren] = useState([])
  const [lessons, setLessons] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [statusMessage, setStatusMessage] = useState('')
  const [form, setForm] = useState(emptyAssignmentForm)
  const [previewAssignment, setPreviewAssignment] = useState(null)
  const [previewResult, setPreviewResult] = useState(null)
  const [previewError, setPreviewError] = useState('')
  const [isOpeningPreview, setIsOpeningPreview] = useState(false)
  const [isLoadingResultDetail, setIsLoadingResultDetail] = useState(false)
  const [resultLookupId, setResultLookupId] = useState('')

  const childOptions = useMemo(
    () => children.map((child) => ({
      value: child.id,
      label: `${child.name} · Grade ${child.grade}`,
      description: child.accessCode ? `Access code: ${child.accessCode}` : 'Ready for new assignments',
    })),
    [children],
  )

  const lessonOptions = useMemo(
    () => lessons.map((lesson) => ({
      value: lesson.id,
      label: lesson.title,
      description: `${lesson.subject} · Grade ${lesson.grade} · ${lesson.topic}`,
    })),
    [lessons],
  )

  const selectedChild = children.find((child) => child.id === form.childId) ?? null
  const selectedLesson = lessons.find((lesson) => lesson.id === form.lessonId) ?? null

  useEffect(() => {
    let isMounted = true

    async function loadData() {
      if (!session?.accessToken) {
        if (isMounted) {
          setIsLoading(false)
        }
        return
      }

      try {
        setError('')
        setStatusMessage('')
        const [assignmentResponse, childrenResponse, lessonsResponse] = await Promise.all([
          getAssignments(session.accessToken),
          getChildren(session.accessToken),
          getLessons(session.accessToken),
        ])

        if (!isMounted) {
          return
        }

        setAssignments(assignmentResponse)
        setChildren(childrenResponse)
        setLessons(lessonsResponse.items)
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

    loadData()

    return () => {
      isMounted = false
    }
  }, [session?.accessToken])

  useEffect(() => {
    if (!previewAssignment) {
      return undefined
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        closePreviewModal()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [previewAssignment])

  function updateField(name, value) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  function findChildName(childId) {
    return children.find((child) => child.id === childId)?.name ?? childId
  }

  function findLessonTitle(lessonId) {
    return lessons.find((lesson) => lesson.id === lessonId)?.title ?? lessonId
  }

  function getLessonMeta(lessonId) {
    const lesson = lessons.find((item) => item.id === lessonId)
    if (!lesson) {
      return 'Lesson metadata unavailable'
    }

    return `${lesson.subject} · Grade ${lesson.grade} · ${lesson.topic}`
  }

  async function handleCreateAssignment(event) {
    event.preventDefault()
    if (!session?.accessToken) {
      return
    }

    const validationError = validateAssignmentForm(form)
    if (validationError) {
      setError(validationError)
      setStatusMessage('')
      return
    }

    setError('')
    setStatusMessage('')
    setIsSubmitting(true)

    try {
      const response = await createAssignment(session.accessToken, {
        childId: form.childId,
        lessonId: form.lessonId,
        dueDate: form.dueDate ? new Date(form.dueDate).toISOString() : null,
      })

      setAssignments((current) => [response, ...current])
      setForm(emptyAssignmentForm)
      setStatusMessage('Assignment was created successfully.')
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleOpenPreview(assignmentId) {
    if (!session?.accessToken) {
      return
    }

    setPreviewError('')
    setPreviewResult(null)
    setResultLookupId('')
    setIsOpeningPreview(true)

    try {
      const response = await getParentAssignmentForSolving(session.accessToken, assignmentId)
      setPreviewAssignment(response)
    } catch (requestError) {
      setPreviewAssignment(null)
      setPreviewError(requestError.message)
    } finally {
      setIsOpeningPreview(false)
    }
  }

  function closePreviewModal() {
    setPreviewAssignment(null)
    setPreviewResult(null)
    setPreviewError('')
    setResultLookupId('')
  }

  async function handleLoadResultDetail(event) {
    event.preventDefault()

    if (!session?.accessToken) {
      return
    }

    const normalizedResultId = resultLookupId.trim()
    if (!normalizedResultId) {
      setPreviewError('Result ID is required to load result detail.')
      return
    }

    setPreviewError('')
    setIsLoadingResultDetail(true)

    try {
      const response = await getParentResultDetail(session.accessToken, normalizedResultId)
      setPreviewResult(response)
    } catch (requestError) {
      setPreviewResult(null)
      setPreviewError(requestError.message)
    } finally {
      setIsLoadingResultDetail(false)
    }
  }

  return (
    <section className="panel-grid">
      <article className="hero-card assignments-hero">
        <div className="brand-kicker">Epic 3.4</div>
        <h2>Assignments management is now connected to the API.</h2>
        <p>
          This slice lets the parent assign lessons to children and review the assignment list
          from the same authenticated workspace.
        </p>
        <div className="badge-row">
          <span className="badge">GET /assignments</span>
          <span className="badge">POST /assignments</span>
          <span className="badge">Child + lesson mapping</span>
        </div>
      </article>

      <article className="panel-card assignments-form-card">
        <div className="section-heading">
          <span className="section-kicker">Create flow</span>
          <h3>Create assignment</h3>
          <p>Select a child, pair the right lesson, and optionally define a due date.</p>
        </div>

        <form className="auth-form compact-form" onSubmit={handleCreateAssignment}>
          <div className="assignments-form-grid">
            <DropdownSelect
              id="assignment-child"
              label="Child"
              placeholder="Select child"
              value={form.childId}
              options={childOptions}
              onChange={(nextValue) => updateField('childId', nextValue)}
              disabled={children.length === 0}
              helperText="Pick who should receive the lesson."
              showHelperHint={false}
              size="compact"
              searchable
              searchPlaceholder="Search child"
            />

            <DropdownSelect
              id="assignment-lesson"
              label="Lesson"
              placeholder="Select lesson"
              value={form.lessonId}
              options={lessonOptions}
              onChange={(nextValue) => updateField('lessonId', nextValue)}
              disabled={lessons.length === 0}
              helperText="Choose from lessons created in the parent workspace."
              showHelperHint={false}
              size="compact"
              searchable
              searchPlaceholder="Search lesson"
            />
          </div>

          {selectedChild || selectedLesson ? (
            <div className="assignment-preview-card">
              <div className="assignment-preview-column">
                <span className="section-kicker">Selected child</span>
                <strong>{selectedChild ? selectedChild.name : 'No child selected yet'}</strong>
                <span>{selectedChild ? `Grade ${selectedChild.grade}` : 'Choose a child to target the assignment.'}</span>
              </div>
              <div className="assignment-preview-column">
                <span className="section-kicker">Selected lesson</span>
                <strong>{selectedLesson ? selectedLesson.title : 'No lesson selected yet'}</strong>
                <span>{selectedLesson ? `${selectedLesson.subject} · Grade ${selectedLesson.grade} · ${selectedLesson.topic}` : 'Choose a lesson to preview its placement.'}</span>
              </div>
            </div>
          ) : null}

          <div className="field">
            <label htmlFor="assignment-due-date">Due date</label>
            <input id="assignment-due-date" className="input" type="datetime-local" value={form.dueDate} onChange={(event) => updateField('dueDate', event.target.value)} />
            <span className="field-hint">Leave empty if this assignment should stay open-ended.</span>
          </div>

          <div className="assignment-form-footer">
            <div className="assignments-form-note">
              <strong>{children.length}</strong> children and <strong>{lessons.length}</strong> lessons are ready for assignment.
            </div>

            <button type="submit" className="button assignment-submit-button" disabled={isSubmitting || children.length === 0 || lessons.length === 0}>
              {isSubmitting ? 'Creating...' : 'Create assignment'}
            </button>
          </div>
        </form>

        {statusMessage ? (
          <div className="info-block success-block assignments-status-block">
            <strong>Update</strong>
            <span>{statusMessage}</span>
          </div>
        ) : null}

        {error ? <div className="alert assignments-alert">{error}</div> : null}
      </article>

      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>Assignments</h3>
            <p>Current assignments across the parent workspace, with child and lesson context.</p>
          </div>
          <span className="badge">{assignments.length} records</span>
        </div>

        {isLoading ? <p className="children-empty">Loading assignments...</p> : null}
        {!isLoading && assignments.length === 0 ? <p className="children-empty">No assignments yet. Create the first assignment from the panel on the right.</p> : null}

        {!isLoading && assignments.length > 0 ? (
          <div className="children-list">
            {assignments.map((assignment) => (
              <article key={assignment.id} className="assignment-row">
                <div className="assignment-copy">
                  <div className="assignment-topline">
                    <div className="child-name">{findChildName(assignment.childId)}</div>
                    <span className="assignment-status-pill">{assignment.status}</span>
                  </div>
                  <div className="assignment-lesson-title">{findLessonTitle(assignment.lessonId)}</div>
                  <div className="child-meta">{getLessonMeta(assignment.lessonId)}</div>
                  <div className="assignment-timeline">
                    <span className="assignment-meta-chip">Assigned {formatDate(assignment.assignedAt)}</span>
                    <span className="assignment-meta-chip">Due {formatDate(assignment.dueDate)}</span>
                  </div>
                </div>
                <div className="button-row child-actions">
                  <button
                    type="button"
                    className="button-secondary"
                    disabled={isOpeningPreview}
                    onClick={() => handleOpenPreview(assignment.id)}
                  >
                    {isOpeningPreview ? 'Opening...' : 'Review'}
                  </button>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </article>

      {previewAssignment ? (
        <div className="modal-overlay" role="presentation" onClick={closePreviewModal}>
          <section
            className="modal-card lesson-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="parent-review-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="children-list-header modal-header">
              <div>
                <h3 id="parent-review-title">Assignment review</h3>
                <p>{previewAssignment.lessonTitle} · {previewAssignment.questions.length} questions</p>
              </div>
              <button type="button" className="button-secondary" onClick={closePreviewModal}>Close</button>
            </div>

            <div className="children-list">
              {previewAssignment.questions.map((question, index) => (
                <article key={question.questionId} className="assignment-row question-card">
                  <div className="assignment-copy">
                    <div className="assignment-topline">
                      <div className="child-name">Question {index + 1}</div>
                    </div>

                    <div>{question.questionText}</div>
                    <div className="child-meta">Explanation: {question.explanation}</div>

                    <div className="question-options">
                      {question.answers.map((answer) => (
                        <div key={answer.answerId} className="question-option">
                          <span>{answer.answerText}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                </article>
              ))}
            </div>

            <form className="auth-form compact-form" onSubmit={handleLoadResultDetail}>
              <div className="field">
                <label htmlFor="parent-result-id">Result ID</label>
                <input
                  id="parent-result-id"
                  className="input"
                  value={resultLookupId}
                  onChange={(event) => setResultLookupId(event.target.value)}
                  placeholder="Paste result ID to review breakdown"
                />
              </div>

              <button type="submit" className="button-secondary" disabled={isLoadingResultDetail}>
                {isLoadingResultDetail ? 'Loading result...' : 'Load result detail'}
              </button>
            </form>

            {previewResult ? (
              <div className="info-block success-block assignments-status-block">
                <strong>Result summary</strong>
                <span>Score: {previewResult.score}% · Correct: {previewResult.correctAnswers}/{previewResult.totalQuestions}</span>
                <span>Completed: {formatDate(previewResult.completedAt)}</span>
                <div className="children-list">
                  {previewResult.breakdown.map((item, index) => (
                    <div key={`${item.questionId}-${index}`} className="assignment-timeline">
                      <span className="assignment-meta-chip">Q{index + 1}</span>
                      <span className={`assignment-status-pill ${item.correct ? 'status-success' : 'status-danger'}`}>
                        {item.correct ? 'Correct' : 'Incorrect'}
                      </span>
                      <span className="assignment-meta-chip">{item.questionId}</span>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}

            {previewError ? <div className="alert assignments-alert">{previewError}</div> : null}
          </section>
        </div>
      ) : null}
    </section>
  )
}
