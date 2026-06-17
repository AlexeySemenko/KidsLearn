import { useEffect, useMemo, useState } from 'react'
import { createAssignment, deleteLesson, duplicateLesson, getChildren, getLesson, getLessons, updateLesson } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'
import AiLessonGenerationModal from '../components/AiLessonGenerationModal'
import LessonViewModal from '../components/LessonViewModal'
import CreateAssignmentModal from '../components/CreateAssignmentModal'

const LESSONS_FILTERS_STORAGE_KEY = 'kidslearn.parent.lessons.filters.v1'

function validateEditLesson(form) {
  if (!form.title.trim() || !form.subject.trim() || !form.topic.trim()) {
    return 'Title, subject and topic cannot be empty.'
  }

  const grade = Number(form.grade)
  if (!Number.isInteger(grade) || grade < 1 || grade > 12) {
    return 'Grade must be a whole number between 1 and 12.'
  }

  if (!form.difficulty.trim()) {
    return 'Difficulty cannot be empty.'
  }

  return null
}

function readStoredLessonsFilters() {
  if (typeof window === 'undefined') {
    return { searchTerm: '', subjectFilter: 'all', sortBy: 'newest' }
  }

  try {
    const rawValue = window.localStorage.getItem(LESSONS_FILTERS_STORAGE_KEY)
    if (!rawValue) {
      return { searchTerm: '', subjectFilter: 'all', sortBy: 'newest' }
    }

    const parsed = JSON.parse(rawValue)
    return {
      searchTerm: typeof parsed.searchTerm === 'string' ? parsed.searchTerm : '',
      subjectFilter: typeof parsed.subjectFilter === 'string' ? parsed.subjectFilter : 'all',
      sortBy: typeof parsed.sortBy === 'string' ? parsed.sortBy : 'newest',
    }
  } catch {
    return { searchTerm: '', subjectFilter: 'all', sortBy: 'newest' }
  }
}

export default function ParentLessonsPage() {
  const { session } = useAuth()
  const [lessons, setLessons] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [statusMessage, setStatusMessage] = useState('')
  const [editingLessonId, setEditingLessonId] = useState(null)
  const [editForm, setEditForm] = useState({ title: '', subject: '', grade: '1', topic: '', difficulty: 'Medium' })
  const [pendingActionId, setPendingActionId] = useState(null)
  const [storedFilters] = useState(() => readStoredLessonsFilters())
  const [searchTerm, setSearchTerm] = useState(storedFilters.searchTerm)
  const [subjectFilter, setSubjectFilter] = useState(storedFilters.subjectFilter)
  const [sortBy, setSortBy] = useState(storedFilters.sortBy)
  const [isAiModalOpen, setIsAiModalOpen] = useState(false)
  const [viewingLesson, setViewingLesson] = useState(null)
  const [isLoadingView, setIsLoadingView] = useState(false)
  const [viewError, setViewError] = useState('')

  const [children, setChildren] = useState([])
  const [showAssignModal, setShowAssignModal] = useState(false)
  const [assignPreselectedLessonId, setAssignPreselectedLessonId] = useState('')
  const [isCreatingAssignment, setIsCreatingAssignment] = useState(false)
  const [assignError, setAssignError] = useState('')

  const availableSubjects = useMemo(() => {
    return [...new Set(lessons.map((lesson) => lesson.subject).filter(Boolean))].sort((a, b) => a.localeCompare(b))
  }, [lessons])

  const filteredLessons = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase()

    return lessons.filter((lesson) => {
      const matchesSearch = !normalizedSearch
        || lesson.title.toLowerCase().includes(normalizedSearch)
        || lesson.subject.toLowerCase().includes(normalizedSearch)
        || lesson.topic.toLowerCase().includes(normalizedSearch)

      const matchesSubject = subjectFilter === 'all' || lesson.subject === subjectFilter

      return matchesSearch && matchesSubject
    })
  }, [subjectFilter, lessons, searchTerm])

  const visibleLessons = useMemo(() => {
    const items = [...filteredLessons]

    switch (sortBy) {
      case 'title-asc':
        items.sort((a, b) => a.title.localeCompare(b.title))
        break
      case 'grade-asc':
        items.sort((a, b) => a.grade - b.grade || a.title.localeCompare(b.title))
        break
      case 'difficulty-asc':
        items.sort((a, b) => a.difficulty.localeCompare(b.difficulty) || a.title.localeCompare(b.title))
        break
      case 'questions-desc':
        items.sort((a, b) => b.questionCount - a.questionCount || a.title.localeCompare(b.title))
        break
      case 'newest':
      default:
        break
    }

    return items
  }, [filteredLessons, sortBy])

  useEffect(() => {
    if (typeof window === 'undefined') {
      return
    }

    window.localStorage.setItem(
      LESSONS_FILTERS_STORAGE_KEY,
      JSON.stringify({ searchTerm, subjectFilter, sortBy }),
    )
  }, [subjectFilter, searchTerm, sortBy])

  useEffect(() => {
    let isMounted = true

    async function loadLessons() {
      if (!session?.accessToken) {
        if (isMounted) {
          setIsLoading(false)
        }
        return
      }

      try {
        setError('')
        setStatusMessage('')
        const [lessonsResponse, childrenData] = await Promise.all([
          getLessons(session.accessToken),
          getChildren(session.accessToken),
        ])
        if (isMounted) {
          setLessons(lessonsResponse.items)
          setChildren(childrenData)
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

    loadLessons()

    return () => {
      isMounted = false
    }
  }, [session?.accessToken])

  useEffect(() => {
    if (!editingLessonId) {
      return undefined
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        cancelEditing()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [editingLessonId])


  function updateEditField(name, value) {
    setEditForm((current) => ({ ...current, [name]: value }))
  }

  function startEditing(lesson) {
    setError('')
    setStatusMessage('')
    setEditingLessonId(lesson.id)
    setEditForm({
      title: lesson.title,
      subject: lesson.subject,
      grade: String(lesson.grade),
      topic: lesson.topic,
      difficulty: lesson.difficulty,
    })
  }

  function cancelEditing() {
    setEditingLessonId(null)
    setEditForm({ title: '', subject: '', grade: '1', topic: '', difficulty: 'Medium' })
  }

  async function handleSaveLesson(lessonId) {
    if (!session?.accessToken) {
      return
    }

    const validationError = validateEditLesson(editForm)
    if (validationError) {
      setError(validationError)
      setStatusMessage('')
      return
    }

    setPendingActionId(lessonId)
    setError('')
    setStatusMessage('')

    try {
      const response = await updateLesson(session.accessToken, lessonId, {
        title: editForm.title.trim(),
        subject: editForm.subject.trim(),
        grade: Number(editForm.grade),
        topic: editForm.topic.trim(),
        difficulty: editForm.difficulty.trim(),
      })

      setLessons((current) => current.map((lesson) => (lesson.id === lessonId ? response : lesson)))
      setStatusMessage(`${response.title} was updated.`)
      cancelEditing()
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setPendingActionId(null)
    }
  }

  async function handleDuplicateLesson(lessonId) {
    if (!session?.accessToken) {
      return
    }

    setPendingActionId(lessonId)
    setError('')
    setStatusMessage('')

    try {
      const response = await duplicateLesson(session.accessToken, lessonId)
      setLessons((current) => [response, ...current])
      setStatusMessage(`${response.title} was duplicated.`)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setPendingActionId(null)
    }
  }

  async function handleDeleteLesson(lessonId) {
    if (!session?.accessToken) {
      return
    }

    const lesson = lessons.find((item) => item.id === lessonId)
    if (!lesson) {
      return
    }

    if (!window.confirm(`Delete lesson ${lesson.title}?`)) {
      return
    }

    setPendingActionId(lessonId)
    setError('')
    setStatusMessage('')

    try {
      await deleteLesson(session.accessToken, lessonId)
      setLessons((current) => current.filter((lessonItem) => lessonItem.id !== lessonId))
      setStatusMessage(`${lesson.title} was deleted.`)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setPendingActionId(null)
    }
  }

  function closeViewModal() {
    setViewingLesson(null)
    setViewError('')
  }

  async function handleViewLesson(lessonId) {
    if (!session?.accessToken) {
      return
    }

    setViewError('')
    setIsLoadingView(true)
    setViewingLesson({ _loading: true })

    try {
      const lesson = await getLesson(session.accessToken, lessonId)
      setViewingLesson(lesson)
    } catch (requestError) {
      setViewingLesson(null)
      setViewError(requestError.message)
    } finally {
      setIsLoadingView(false)
    }
  }

  function handleAiGenerated(response) {
    const draft = response.lessonDraft
    const summary = {
      id: draft.id,
      title: draft.title,
      subject: draft.subject,
      grade: draft.grade,
      topic: draft.topic,
      difficulty: draft.difficulty,
      createdAt: draft.createdAt,
      questionCount: draft.questions.length,
    }

    setLessons((current) => [summary, ...current])
    setStatusMessage(`AI lesson "${draft.title}" was generated.`)
    setIsAiModalOpen(false)
    setViewingLesson(draft)
    setAssignPreselectedLessonId(draft.id)
    setAssignError('')
    setShowAssignModal(true)
  }

  async function handleCreateAssignment(payload) {
    setIsCreatingAssignment(true)
    setAssignError('')
    try {
      await createAssignment(session.accessToken, payload)
      setShowAssignModal(false)
      setStatusMessage('Assignment created.')
    } catch (err) {
      setAssignError(err.message)
    } finally {
      setIsCreatingAssignment(false)
    }
  }

  return (
    <article className="lessons-list-card">
      <div className="children-list-header">
        <div>
          <h3>Lessons</h3>
          <p>{visibleLessons.length} of {lessons.length} lessons</p>
        </div>
        <button type="button" className="button ai-launch-button" onClick={() => setIsAiModalOpen(true)}>
          <span className="ai-button-icon" aria-hidden="true">AI</span>
          <span>Generate</span>
        </button>
      </div>

      {statusMessage ? (
        <div className="info-block success-block lessons-status-block" role="status" aria-live="polite">
          <strong>Update</strong>
          <span>{statusMessage}</span>
        </div>
      ) : null}

      {error ? <div className="alert lessons-alert" role="alert" aria-live="assertive">{error}</div> : null}
      {viewError ? <div className="alert lessons-alert" role="alert" aria-live="assertive">{viewError}</div> : null}

      <div className="lessons-toolbar">
        <div className="lessons-filter-grid">
          <div className="field">
            <label htmlFor="lessons-search">Search lessons</label>
            <input
              id="lessons-search"
              className="input"
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder="Search by title, subject, or topic"
            />
          </div>

          <div className="field">
            <label htmlFor="lessons-subject-filter">Subject</label>
            <select id="lessons-subject-filter" className="input" value={subjectFilter} onChange={(event) => setSubjectFilter(event.target.value)}>
              <option value="all">All subjects</option>
              {availableSubjects.map((subject) => (
                <option key={subject} value={subject}>{subject}</option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="lessons-sort">Sort by</label>
            <select id="lessons-sort" className="input" value={sortBy} onChange={(event) => setSortBy(event.target.value)}>
              <option value="newest">Newest</option>
              <option value="title-asc">Title (A-Z)</option>
              <option value="grade-asc">Grade (low-high)</option>
              <option value="difficulty-asc">Difficulty (A-Z)</option>
              <option value="questions-desc">Questions (most first)</option>
            </select>
          </div>
        </div>

        <div className="button-row">
          <button
            type="button"
            className="button-secondary"
            onClick={() => {
              setSearchTerm('')
              setSubjectFilter('all')
              setSortBy('newest')
            }}
            disabled={!searchTerm && subjectFilter === 'all' && sortBy === 'newest'}
          >
            Reset filters
          </button>
        </div>
      </div>

      {isLoading ? <p className="children-empty">Loading lessons...</p> : null}
      {!isLoading && lessons.length === 0 ? <p className="children-empty">No lessons yet. Use Generate to create your first AI lesson.</p> : null}
      {!isLoading && lessons.length > 0 && visibleLessons.length === 0 ? <p className="children-empty">No lessons match current filters.</p> : null}

      {!isLoading && visibleLessons.length > 0 ? (
        <div className="children-list">
          {visibleLessons.map((lesson) => (
            <article key={lesson.id} className="lesson-row">
              <div>
                <div className="child-name">{lesson.title}</div>
                <div className="child-meta">
                  {lesson.subject} · Grade {lesson.grade} · {lesson.topic} · {lesson.difficulty} · {lesson.questionCount} questions
                  {lesson.createdByName ? <span> · by {lesson.createdByName}</span> : null}
                </div>
              </div>
              <div className="button-row child-actions">
                <button type="button" className="button-secondary" disabled={pendingActionId === lesson.id || isLoadingView} onClick={() => handleViewLesson(lesson.id)}>View</button>
                <button type="button" className="button-secondary" disabled={pendingActionId === lesson.id} onClick={() => startEditing(lesson)}>Edit</button>
                <button type="button" className="button-secondary" disabled={pendingActionId === lesson.id} onClick={() => handleDuplicateLesson(lesson.id)}>
                  {pendingActionId === lesson.id ? 'Working...' : 'Duplicate'}
                </button>
                <button type="button" className="button-secondary danger-button" disabled={pendingActionId === lesson.id} onClick={() => handleDeleteLesson(lesson.id)}>
                  {pendingActionId === lesson.id ? 'Deleting...' : 'Delete'}
                </button>
              </div>
            </article>
          ))}
        </div>
      ) : null}

      {viewingLesson && !viewingLesson._loading ? (
        <LessonViewModal
          title={viewingLesson.title}
          subtitle={`${viewingLesson.subject} · Grade ${viewingLesson.grade} · ${viewingLesson.topic} · ${viewingLesson.difficulty} · ${viewingLesson.questions.length} questions`}
          questions={viewingLesson.questions}
          onClose={closeViewModal}
        />
      ) : null}

      {editingLessonId ? (
        <div className="modal-overlay" role="presentation">
          <section className="modal-card lesson-modal" role="dialog" aria-modal="true" aria-labelledby="lesson-edit-title">
            <div className="children-list-header modal-header">
              <div>
                <h3 id="lesson-edit-title">Edit lesson</h3>
                <p>Update summary fields for the selected lesson.</p>
              </div>
              <button type="button" className="button-secondary" onClick={cancelEditing}>Close</button>
            </div>

            <div className="lesson-edit-grid">
              <div className="field">
                <label htmlFor="modal-lesson-title">Title</label>
                <input id="modal-lesson-title" className="input" value={editForm.title} onChange={(event) => updateEditField('title', event.target.value)} autoFocus />
              </div>
              <div className="field">
                <label htmlFor="modal-lesson-subject">Subject</label>
                <input id="modal-lesson-subject" className="input" value={editForm.subject} onChange={(event) => updateEditField('subject', event.target.value)} />
              </div>
              <div className="field">
                <label htmlFor="modal-lesson-grade">Grade</label>
                <input id="modal-lesson-grade" className="input" type="number" min="1" max="12" value={editForm.grade} onChange={(event) => updateEditField('grade', event.target.value)} />
              </div>
              <div className="field">
                <label htmlFor="modal-lesson-topic">Topic</label>
                <input id="modal-lesson-topic" className="input" value={editForm.topic} onChange={(event) => updateEditField('topic', event.target.value)} />
              </div>
              <div className="field">
                <label htmlFor="modal-lesson-difficulty">Difficulty</label>
                <input id="modal-lesson-difficulty" className="input" value={editForm.difficulty} onChange={(event) => updateEditField('difficulty', event.target.value)} />
              </div>
            </div>

            <div className="button-row modal-actions">
              <button type="button" className="button" disabled={pendingActionId === editingLessonId} onClick={() => handleSaveLesson(editingLessonId)}>
                {pendingActionId === editingLessonId ? 'Saving...' : 'Save'}
              </button>
              <button type="button" className="button-secondary" onClick={cancelEditing}>Cancel</button>
            </div>
          </section>
        </div>
      ) : null}

      <AiLessonGenerationModal
        isOpen={isAiModalOpen}
        onClose={() => setIsAiModalOpen(false)}
        onGenerated={handleAiGenerated}
        idPrefix="lessons-ai"
        title="Generate AI lesson"
        description="Create a lesson draft from prompt settings without leaving lessons."
      />

      {showAssignModal ? (
        <CreateAssignmentModal
          children={children}
          lessons={lessons}
          preselectedLessonId={assignPreselectedLessonId}
          onSave={handleCreateAssignment}
          onClose={() => setShowAssignModal(false)}
          isSaving={isCreatingAssignment}
          error={assignError}
        />
      ) : null}
    </article>
  )
}
