import { useEffect, useMemo, useState } from 'react'
import { createLesson, deleteLesson, duplicateLesson, getLessons, updateLesson } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'
import AiLessonGenerationModal from '../components/AiLessonGenerationModal'

const LESSONS_FILTERS_STORAGE_KEY = 'kidslearn.parent.lessons.filters.v1'

const emptyCreateForm = {
  title: '',
  subject: '',
  grade: '1',
  topic: '',
  difficulty: 'Medium',
  questionText: '',
  explanation: '',
  answerA: '',
  answerB: '',
  correctAnswer: 'A',
}

function validateCreateLesson(form) {
  if (!form.title.trim() || !form.subject.trim() || !form.topic.trim()) {
    return 'Title, subject and topic are required.'
  }

  const grade = Number(form.grade)
  if (!Number.isInteger(grade) || grade < 1 || grade > 12) {
    return 'Grade must be a whole number between 1 and 12.'
  }

  if (!form.questionText.trim()) {
    return 'At least one question is required.'
  }

  if (!form.answerA.trim() || !form.answerB.trim()) {
    return 'Provide at least two answers.'
  }

  return null
}

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
    return { searchTerm: '', gradeFilter: 'all', difficultyFilter: 'all', sortBy: 'newest' }
  }

  try {
    const rawValue = window.localStorage.getItem(LESSONS_FILTERS_STORAGE_KEY)
    if (!rawValue) {
      return { searchTerm: '', gradeFilter: 'all', difficultyFilter: 'all', sortBy: 'newest' }
    }

    const parsed = JSON.parse(rawValue)
    return {
      searchTerm: typeof parsed.searchTerm === 'string' ? parsed.searchTerm : '',
      gradeFilter: typeof parsed.gradeFilter === 'string' ? parsed.gradeFilter : 'all',
      difficultyFilter: typeof parsed.difficultyFilter === 'string' ? parsed.difficultyFilter : 'all',
      sortBy: typeof parsed.sortBy === 'string' ? parsed.sortBy : 'newest',
    }
  } catch {
    return { searchTerm: '', gradeFilter: 'all', difficultyFilter: 'all', sortBy: 'newest' }
  }
}

export default function ParentLessonsPage() {
  const { session } = useAuth()
  const [lessons, setLessons] = useState([])
  const [isLoading, setIsLoading] = useState(true)
  const [isCreating, setIsCreating] = useState(false)
  const [error, setError] = useState('')
  const [statusMessage, setStatusMessage] = useState('')
  const [createForm, setCreateForm] = useState(emptyCreateForm)
  const [editingLessonId, setEditingLessonId] = useState(null)
  const [editForm, setEditForm] = useState({ title: '', subject: '', grade: '1', topic: '', difficulty: 'Medium' })
  const [pendingActionId, setPendingActionId] = useState(null)
  const [storedFilters] = useState(() => readStoredLessonsFilters())
  const [searchTerm, setSearchTerm] = useState(storedFilters.searchTerm)
  const [gradeFilter, setGradeFilter] = useState(storedFilters.gradeFilter)
  const [difficultyFilter, setDifficultyFilter] = useState(storedFilters.difficultyFilter)
  const [sortBy, setSortBy] = useState(storedFilters.sortBy)
  const [isAiModalOpen, setIsAiModalOpen] = useState(false)

  const availableGrades = useMemo(() => {
    return [...new Set(lessons.map((lesson) => String(lesson.grade)))].sort((a, b) => Number(a) - Number(b))
  }, [lessons])

  const availableDifficulties = useMemo(() => {
    return [...new Set(lessons.map((lesson) => lesson.difficulty).filter(Boolean))].sort((a, b) => a.localeCompare(b))
  }, [lessons])

  const filteredLessons = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase()

    return lessons.filter((lesson) => {
      const matchesSearch = !normalizedSearch
        || lesson.title.toLowerCase().includes(normalizedSearch)
        || lesson.subject.toLowerCase().includes(normalizedSearch)
        || lesson.topic.toLowerCase().includes(normalizedSearch)

      const matchesGrade = gradeFilter === 'all' || String(lesson.grade) === gradeFilter
      const matchesDifficulty = difficultyFilter === 'all' || lesson.difficulty === difficultyFilter

      return matchesSearch && matchesGrade && matchesDifficulty
    })
  }, [difficultyFilter, gradeFilter, lessons, searchTerm])

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
      JSON.stringify({ searchTerm, gradeFilter, difficultyFilter, sortBy }),
    )
  }, [difficultyFilter, gradeFilter, searchTerm, sortBy])

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
        const response = await getLessons(session.accessToken)
        if (isMounted) {
          setLessons(response.items)
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

  function updateCreateField(name, value) {
    setCreateForm((current) => ({ ...current, [name]: value }))
  }

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

  async function handleCreateLesson(event) {
    event.preventDefault()
    if (!session?.accessToken) {
      return
    }

    const validationError = validateCreateLesson(createForm)
    if (validationError) {
      setError(validationError)
      setStatusMessage('')
      return
    }

    setError('')
    setStatusMessage('')
    setIsCreating(true)

    try {
      const response = await createLesson(session.accessToken, {
        title: createForm.title.trim(),
        subject: createForm.subject.trim(),
        grade: Number(createForm.grade),
        topic: createForm.topic.trim(),
        difficulty: createForm.difficulty.trim(),
        questions: [
          {
            questionText: createForm.questionText.trim(),
            explanation: createForm.explanation.trim() || null,
            order: 1,
            answers: [
              { answerText: createForm.answerA.trim(), isCorrect: createForm.correctAnswer === 'A', order: 1 },
              { answerText: createForm.answerB.trim(), isCorrect: createForm.correctAnswer === 'B', order: 2 },
            ],
          },
        ],
      })

      setLessons((current) => [response, ...current])
      setCreateForm(emptyCreateForm)
      setStatusMessage(`${response.title} was created successfully.`)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsCreating(false)
    }
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

  function openAiModal() {
    setIsAiModalOpen(true)
  }

  function closeAiModal() {
    setIsAiModalOpen(false)
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
    setStatusMessage(`AI lesson \"${draft.title}\" was generated.`)
  }

  return (
    <section className="panel-grid">
      <article className="hero-card lessons-hero">
        <div className="brand-kicker">Epic 3.3</div>
        <h2>Lessons management is now live.</h2>
        <p>
          This slice adds real lesson list loading, lesson creation, modal summary editing,
          duplication, and deletion on top of the authenticated parent workspace.
        </p>
        <div className="badge-row">
          <span className="badge">GET /lessons</span>
          <span className="badge">POST /lessons</span>
          <span className="badge">PATCH /lessons/{'{id}'}</span>
          <span className="badge">POST /lessons/{'{id}'}/duplicate</span>
          <span className="badge">DELETE /lessons/{'{id}'}</span>
        </div>
      </article>

      <article className="panel-card lessons-form-card">
        <h3>Create lesson</h3>
        <p>Start with one question and two answers. Detailed editing can layer on top later.</p>

        <form className="auth-form compact-form" onSubmit={handleCreateLesson}>
          <div className="field">
            <label htmlFor="lesson-title">Title</label>
            <input id="lesson-title" className="input" value={createForm.title} onChange={(event) => updateCreateField('title', event.target.value)} placeholder="Fractions basics" required />
          </div>

          <div className="lessons-form-grid">
            <div className="field">
              <label htmlFor="lesson-subject">Subject</label>
              <input id="lesson-subject" className="input" value={createForm.subject} onChange={(event) => updateCreateField('subject', event.target.value)} placeholder="Math" required />
            </div>
            <div className="field">
              <label htmlFor="lesson-grade">Grade</label>
              <input id="lesson-grade" className="input" type="number" min="1" max="12" value={createForm.grade} onChange={(event) => updateCreateField('grade', event.target.value)} required />
            </div>
          </div>

          <div className="lessons-form-grid">
            <div className="field">
              <label htmlFor="lesson-topic">Topic</label>
              <input id="lesson-topic" className="input" value={createForm.topic} onChange={(event) => updateCreateField('topic', event.target.value)} placeholder="Equivalent fractions" required />
            </div>
            <div className="field">
              <label htmlFor="lesson-difficulty">Difficulty</label>
              <input id="lesson-difficulty" className="input" value={createForm.difficulty} onChange={(event) => updateCreateField('difficulty', event.target.value)} placeholder="Medium" />
            </div>
          </div>

          <div className="field">
            <label htmlFor="lesson-question">Question</label>
            <input id="lesson-question" className="input" value={createForm.questionText} onChange={(event) => updateCreateField('questionText', event.target.value)} placeholder="Which fraction is equal to 1/2?" required />
          </div>

          <div className="field">
            <label htmlFor="lesson-explanation">Explanation</label>
            <input id="lesson-explanation" className="input" value={createForm.explanation} onChange={(event) => updateCreateField('explanation', event.target.value)} placeholder="Two fourths simplify to one half." />
          </div>

          <div className="lessons-form-grid">
            <div className="field">
              <label htmlFor="answer-a">Answer A</label>
              <input id="answer-a" className="input" value={createForm.answerA} onChange={(event) => updateCreateField('answerA', event.target.value)} placeholder="2/4" required />
            </div>
            <div className="field">
              <label htmlFor="answer-b">Answer B</label>
              <input id="answer-b" className="input" value={createForm.answerB} onChange={(event) => updateCreateField('answerB', event.target.value)} placeholder="3/4" required />
            </div>
          </div>

          <div className="field">
            <label htmlFor="correct-answer">Correct answer</label>
            <select id="correct-answer" className="input" value={createForm.correctAnswer} onChange={(event) => updateCreateField('correctAnswer', event.target.value)}>
              <option value="A">Answer A</option>
              <option value="B">Answer B</option>
            </select>
          </div>

          <div className="button-row">
            <button type="submit" className="button" disabled={isCreating}>
              {isCreating ? 'Creating...' : 'Create lesson'}
            </button>
            <button type="button" className="button ai-launch-button" onClick={openAiModal}>
              <span className="ai-button-icon" aria-hidden="true">AI</span>
              <span>Generate lesson</span>
            </button>
          </div>
        </form>

        {statusMessage ? (
          <div className="info-block success-block lessons-status-block">
            <strong>Update</strong>
            <span>{statusMessage}</span>
          </div>
        ) : null}

        {error ? <div className="alert lessons-alert">{error}</div> : null}
      </article>

      <article className="lessons-list-card">
        <div className="children-list-header">
          <div>
            <h3>Lessons</h3>
            <p>Summary-level management for the parent lesson library.</p>
          </div>
          <span className="badge">{visibleLessons.length} / {lessons.length} records</span>
        </div>

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
              <label htmlFor="lessons-grade-filter">Grade</label>
              <select id="lessons-grade-filter" className="input" value={gradeFilter} onChange={(event) => setGradeFilter(event.target.value)}>
                <option value="all">All grades</option>
                {availableGrades.map((grade) => (
                  <option key={grade} value={grade}>Grade {grade}</option>
                ))}
              </select>
            </div>

            <div className="field">
              <label htmlFor="lessons-difficulty-filter">Difficulty</label>
              <select id="lessons-difficulty-filter" className="input" value={difficultyFilter} onChange={(event) => setDifficultyFilter(event.target.value)}>
                <option value="all">All levels</option>
                {availableDifficulties.map((difficulty) => (
                  <option key={difficulty} value={difficulty}>{difficulty}</option>
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
                setGradeFilter('all')
                setDifficultyFilter('all')
                setSortBy('newest')
              }}
              disabled={!searchTerm && gradeFilter === 'all' && difficultyFilter === 'all' && sortBy === 'newest'}
            >
              Reset filters
            </button>
          </div>
        </div>

        {isLoading ? <p className="children-empty">Loading lessons...</p> : null}
        {!isLoading && lessons.length === 0 ? <p className="children-empty">No lessons yet. Create the first lesson from the panel on the right.</p> : null}
        {!isLoading && lessons.length > 0 && visibleLessons.length === 0 ? <p className="children-empty">No lessons match current filters.</p> : null}

        {!isLoading && visibleLessons.length > 0 ? (
          <div className="children-list">
            {visibleLessons.map((lesson) => (
              <article key={lesson.id} className="lesson-row">
                <div>
                  <div className="child-name">{lesson.title}</div>
                  <div className="child-meta">
                    {lesson.subject} · Grade {lesson.grade} · {lesson.topic} · {lesson.difficulty} · {lesson.questionCount} questions
                  </div>
                </div>
                <div className="button-row child-actions">
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
      </article>

      {editingLessonId ? (
        <div className="modal-overlay" role="presentation" onClick={cancelEditing}>
          <section className="modal-card lesson-modal" role="dialog" aria-modal="true" aria-labelledby="lesson-edit-title" onClick={(event) => event.stopPropagation()}>
            <div className="children-list-header modal-header">
              <div>
                <h3 id="lesson-edit-title">Edit lesson</h3>
                <p>Update summary fields for the selected lesson in a separate popup.</p>
              </div>
              <button type="button" className="button-secondary" onClick={cancelEditing}>Close</button>
            </div>

            <div className="lesson-edit-grid">
              <div className="field">
                <label htmlFor="modal-lesson-title">Title</label>
                <input id="modal-lesson-title" className="input" value={editForm.title} onChange={(event) => updateEditField('title', event.target.value)} />
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
        onClose={closeAiModal}
        onGenerated={handleAiGenerated}
        idPrefix="lessons-ai"
        title="Generate AI lesson"
        description="Create a lesson draft from prompt settings without leaving lessons."
      />
    </section>
  )
}
