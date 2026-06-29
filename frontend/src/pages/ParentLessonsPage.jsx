import { useEffect, useMemo, useState } from 'react'
import { createAssignment, createLesson, deleteLesson, duplicateLesson, getChildren, getLesson, getLessons, getLessonStoryImage, updateLesson, updateLessonQuestions } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'
import Toast from '../components/Toast'
import AiLessonGenerationModal from '../components/AiLessonGenerationModal'
import LessonViewModal from '../components/LessonViewModal'
import LessonFormModal from '../components/LessonFormModal'
import CreateAssignmentModal from '../components/CreateAssignmentModal'
import { questionsFromDetail } from '../components/QuestionEditor'

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

function validateQuestions(questions) {
  for (let i = 0; i < questions.length; i++) {
    const q = questions[i]
    if (!q.questionText.trim()) return `Question ${i + 1}: question text is required.`
    if (q.answers.length < 2) return `Question ${i + 1}: at least 2 answers required.`
    if (!q.answers.some((a) => a.isCorrect)) return `Question ${i + 1}: at least one correct answer is required.`
    if (q.answerType === 'multiple_choice' && q.answers.some((a) => !a.answerText.trim())) {
      return `Question ${i + 1}: answer text cannot be empty.`
    }
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

// ── Page ─────────────────────────────────────────────────────────────────────

export default function ParentLessonsPage() {
  const { session } = useAuth()
  const [lessons, setLessons] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [toast, setToast] = useState(null)

  function showToast(message, type = 'success') {
    setToast({ message, type })
  }
  const [editingLessonId, setEditingLessonId] = useState(null)
  const [editForm, setEditForm] = useState({ title: '', subject: '', grade: '1', topic: '', difficulty: 'Medium', questions: [] })
  const [isLoadingEditDetail, setIsLoadingEditDetail] = useState(false)
  const [pendingActionId, setPendingActionId] = useState(null)
  const [storedFilters] = useState(() => readStoredLessonsFilters())
  const [searchTerm, setSearchTerm] = useState(storedFilters.searchTerm)
  const [subjectFilter, setSubjectFilter] = useState(storedFilters.subjectFilter)
  const [sortBy, setSortBy] = useState(storedFilters.sortBy)
  const [isAiModalOpen, setIsAiModalOpen] = useState(false)
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false)
  const [isCreatingLesson, setIsCreatingLesson] = useState(false)
  const [createError, setCreateError] = useState('')
  const [viewingLesson, setViewingLesson] = useState(null)
  const [viewingLessonImage, setViewingLessonImage] = useState(null)
  const [isLoadingView, setIsLoadingView] = useState(false)
  const [viewError, setViewError] = useState('')

  const [children, setChildren] = useState([])
  const [showAssignModal, setShowAssignModal] = useState(false)
  const [assignPreselectedLessonId, setAssignPreselectedLessonId] = useState('')
  const [isCreatingAssignment, setIsCreatingAssignment] = useState(false)
  const [assignError, setAssignError] = useState('')
  const [pendingEditAfterAssign, setPendingEditAfterAssign] = useState(null)

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

  async function startEditing(lesson) {
    setError('')
    setEditingLessonId(lesson.id)
    setEditForm({
      title: lesson.title,
      subject: lesson.subject,
      grade: String(lesson.grade),
      topic: lesson.topic,
      difficulty: lesson.difficulty,
      story: '',
      questions: [],
    })
    setIsLoadingEditDetail(true)
    try {
      const detail = await getLesson(session.accessToken, lesson.id)
      setEditForm((prev) => ({ ...prev, story: detail.story ?? '', questions: questionsFromDetail(detail.questions) }))
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoadingEditDetail(false)
    }
  }

  function cancelEditing() {
    setEditingLessonId(null)
    setEditForm({ title: '', subject: '', grade: '1', topic: '', difficulty: 'Medium', story: '', questions: [] })
  }

  async function handleSaveLesson(lessonId, formData) {
    if (!session?.accessToken) return

    const summaryError = validateEditLesson(formData)
    if (summaryError) { setError(summaryError); return }

    const questionsError = validateQuestions(formData.questions)
    if (questionsError) { setError(questionsError); return }

    setPendingActionId(lessonId)
    setError('')

    try {
      const response = await updateLesson(session.accessToken, lessonId, {
        title: formData.title,
        subject: formData.subject,
        grade: Number(formData.grade),
        topic: formData.topic,
        difficulty: formData.difficulty,
        story: formData.story ?? null,
      })

      setLessons((current) => current.map((lesson) => (lesson.id === lessonId ? response : lesson)))

      const questionsPayload = formData.questions.map((q) => ({
        id: q.id || undefined,
        questionText: q.questionText.trim(),
        explanation: q.explanation.trim(),
        answers: q.answerType === 'true_false'
          ? [
              { answerText: 'True', isCorrect: q.answers[0]?.isCorrect ?? true },
              { answerText: 'False', isCorrect: !(q.answers[0]?.isCorrect ?? true) },
            ]
          : q.answers.map((a) => ({ answerText: a.answerText.trim(), isCorrect: a.isCorrect })),
      }))

      await updateLessonQuestions(session.accessToken, lessonId, questionsPayload)

      showToast(`${response.title} was updated.`)
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

    try {
      const response = await duplicateLesson(session.accessToken, lessonId)
      setLessons((current) => [response, ...current])
      showToast(`${response.title} was duplicated.`)
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

    try {
      await deleteLesson(session.accessToken, lessonId)
      setLessons((current) => current.filter((lessonItem) => lessonItem.id !== lessonId))
      showToast(`${lesson.title} was deleted.`)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setPendingActionId(null)
    }
  }

  function closeViewModal() {
    setViewingLesson(null)
    setViewingLessonImage(null)
    setViewError('')
  }

  async function handleViewLesson(lessonId) {
    if (!session?.accessToken) {
      return
    }

    setViewError('')
    setIsLoadingView(true)
    setViewingLesson({ _loading: true })
    setViewingLessonImage(null)

    try {
      const lesson = await getLesson(session.accessToken, lessonId)
      setViewingLesson(lesson)
      if (lesson.hasStoryImage) {
        getLessonStoryImage(session.accessToken, lesson.id)
          .then(result => setViewingLessonImage(result?.storyImageUrl ?? null))
          .catch(() => {})
      }
    } catch (requestError) {
      setViewingLesson(null)
      setViewError(requestError.message)
    } finally {
      setIsLoadingView(false)
    }
  }

  async function handleCreateLesson(formData) {
    if (!session?.accessToken) return

    const summaryError = validateEditLesson(formData)
    if (summaryError) { setCreateError(summaryError); return }

    const questionsError = validateQuestions(formData.questions)
    if (questionsError) { setCreateError(questionsError); return }

    setIsCreatingLesson(true)
    setCreateError('')
    try {
      const newLesson = await createLesson(session.accessToken, {
        title: formData.title,
        subject: formData.subject,
        grade: Number(formData.grade),
        topic: formData.topic,
        difficulty: formData.difficulty,
        story: formData.story ?? null,
        questions: [],
      })

      if (formData.questions.length > 0) {
        const questionsPayload = formData.questions.map((q) => ({
          id: undefined,
          questionText: q.questionText.trim(),
          explanation: q.explanation.trim(),
          answers: q.answerType === 'true_false'
            ? [
                { answerText: 'True', isCorrect: q.answers[0]?.isCorrect ?? true },
                { answerText: 'False', isCorrect: !(q.answers[0]?.isCorrect ?? true) },
              ]
            : q.answers.map((a) => ({ answerText: a.answerText.trim(), isCorrect: a.isCorrect })),
        }))
        await updateLessonQuestions(session.accessToken, newLesson.id, questionsPayload)
        newLesson.questionCount = formData.questions.length
      }

      setLessons((current) => [newLesson, ...current])
      showToast(`Lesson "${newLesson.title}" was created.`)
      setIsCreateModalOpen(false)
    } catch (err) {
      setCreateError(err.message)
    } finally {
      setIsCreatingLesson(false)
    }
  }

  function handleAiGenerated(response) {
    const draft = response.lessonDraft
    const summary = {
      id: response.createdLessonId,
      title: draft.title,
      subject: draft.subject,
      grade: draft.grade,
      topic: draft.topic,
      difficulty: draft.difficulty,
      createdAt: draft.createdAt,
      questionCount: draft.questions.length,
    }

    setLessons((current) => [summary, ...current])
    showToast(`AI lesson "${draft.title}" was generated.`)
    setIsAiModalOpen(false)
    setPendingEditAfterAssign(summary)
    setAssignPreselectedLessonId(response.createdLessonId)
    setAssignError('')
    setShowAssignModal(true)
  }

  async function handleCreateAssignment(payload) {
    setIsCreatingAssignment(true)
    setAssignError('')
    try {
      await createAssignment(session.accessToken, payload)
      setShowAssignModal(false)
      showToast('Assignment created.')
      if (pendingEditAfterAssign) {
        const lessonToEdit = pendingEditAfterAssign
        setPendingEditAfterAssign(null)
        startEditing(lessonToEdit)
      }
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
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <button type="button" className="button create-lesson-button" onClick={() => { setCreateError(''); setIsCreateModalOpen(true) }}>
            + Create
          </button>
          <button type="button" className="button ai-launch-button" onClick={() => setIsAiModalOpen(true)}>
            <span className="ai-button-icon" aria-hidden="true">AI</span>
            <span>Generate</span>
          </button>
        </div>
      </div>

      {toast ? <Toast message={toast.message} type={toast.type} onDismiss={() => setToast(null)} /> : null}

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
          story={viewingLesson.story}
          storyImageUrl={viewingLessonImage}
          questions={viewingLesson.questions}
          onClose={closeViewModal}
        />
      ) : null}

      {isCreateModalOpen ? (
        <LessonFormModal
          mode="create"
          isSaving={isCreatingLesson}
          error={createError}
          onSave={handleCreateLesson}
          onClose={() => setIsCreateModalOpen(false)}
        />
      ) : null}

      {editingLessonId ? (
        <LessonFormModal
          mode="edit"
          initialData={editForm}
          isLoadingQuestions={isLoadingEditDetail}
          isSaving={pendingActionId === editingLessonId}
          error={error}
          onSave={(formData) => handleSaveLesson(editingLessonId, formData)}
          onClose={cancelEditing}
        />
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
          onClose={() => { setShowAssignModal(false); setPendingEditAfterAssign(null) }}
          isSaving={isCreatingAssignment}
          error={assignError}
        />
      ) : null}
    </article>
  )
}
