import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import QuestionEditor from './QuestionEditor'

const SUBJECTS = ['Math', 'English', 'Hebrew', 'Science', 'History']
const DIFFICULTIES = ['Easy', 'Medium', 'Hard']

export default function LessonFormModal({
  mode = 'create',
  initialData,
  isLoadingQuestions = false,
  isSaving = false,
  error = '',
  onSave,
  onClose,
}) {
  const [form, setForm] = useState(() => ({
    title: initialData?.title ?? '',
    subject: SUBJECTS.includes(initialData?.subject) ? initialData.subject : (initialData?.subject ? '__other__' : 'Math'),
    subjectCustom: SUBJECTS.includes(initialData?.subject) ? '' : (initialData?.subject ?? ''),
    grade: String(initialData?.grade ?? '2'),
    topic: initialData?.topic ?? '',
    difficulty: initialData?.difficulty ?? 'Medium',
    story: initialData?.story ?? '',
    questions: initialData?.questions ?? [],
  }))

  useEffect(() => {
    if (initialData?.questions) {
      setForm((prev) => ({ ...prev, questions: initialData.questions, story: initialData.story ?? prev.story }))
    }
  }, [initialData?.questions])

  useEffect(() => {
    function handleKeyDown(e) {
      if (e.key === 'Escape' && !isSaving) onClose()
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isSaving, onClose])

  function setField(name, value) {
    setForm((prev) => ({ ...prev, [name]: value }))
  }

  function resolvedSubject() {
    return form.subject === '__other__' ? form.subjectCustom.trim() : form.subject
  }

  function handleSave() {
    onSave({
      title: form.title.trim(),
      subject: resolvedSubject(),
      grade: form.grade,
      topic: form.topic.trim(),
      difficulty: form.difficulty,
      story: form.story.trim() || null,
      questions: form.questions,
    })
  }

  const title = mode === 'create' ? 'Create lesson' : 'Edit lesson'
  const description = mode === 'create' ? 'Add a new lesson with questions.' : 'Update summary fields and questions.'
  const saveLabel = isSaving ? 'Saving…' : (mode === 'create' ? 'Create' : 'Save')

  return createPortal(
    <div className="modal-overlay" role="presentation">
      <section className="modal-card lesson-edit-modal" role="dialog" aria-modal="true" aria-labelledby="lesson-form-title">
        <div className="children-list-header modal-header">
          <div>
            <h3 id="lesson-form-title">{title}</h3>
            <p>{description}</p>
          </div>
          <button type="button" className="button-secondary" onClick={onClose} disabled={isSaving}>Close</button>
        </div>

        <div className="lesson-edit-grid">
          <div className="field">
            <label htmlFor="lf-title">Title</label>
            <input
              id="lf-title"
              className="input"
              value={form.title}
              onChange={(e) => setField('title', e.target.value)}
              autoFocus={mode === 'create'}
              placeholder="e.g. Addition and Subtraction"
            />
          </div>

          <div className="field">
            <label htmlFor="lf-subject">Subject</label>
            <select
              id="lf-subject"
              className="input"
              value={form.subject}
              onChange={(e) => setField('subject', e.target.value)}
            >
              {SUBJECTS.map((s) => <option key={s} value={s}>{s}</option>)}
              <option value="__other__">Other…</option>
            </select>
          </div>

          {form.subject === '__other__' ? (
            <div className="field">
              <label htmlFor="lf-subject-custom">Custom subject</label>
              <input
                id="lf-subject-custom"
                className="input"
                value={form.subjectCustom}
                onChange={(e) => setField('subjectCustom', e.target.value)}
                placeholder="Enter subject name"
                autoFocus
              />
            </div>
          ) : null}

          <div className="field">
            <label htmlFor="lf-grade">Grade</label>
            <input
              id="lf-grade"
              className="input"
              type="number"
              min="1"
              max="12"
              value={form.grade}
              onChange={(e) => setField('grade', e.target.value)}
            />
          </div>

          <div className="field">
            <label htmlFor="lf-topic">Topic</label>
            <input
              id="lf-topic"
              className="input"
              value={form.topic}
              onChange={(e) => setField('topic', e.target.value)}
              placeholder="e.g. Two-digit numbers"
            />
          </div>

          <div className="field">
            <label htmlFor="lf-difficulty">Difficulty</label>
            <select
              id="lf-difficulty"
              className="input"
              value={form.difficulty}
              onChange={(e) => setField('difficulty', e.target.value)}
            >
              {DIFFICULTIES.map((d) => <option key={d} value={d}>{d}</option>)}
            </select>
          </div>
        </div>

        <div className="lesson-story-section">
          <h4 className="lesson-edit-questions-title">Story <span className="lesson-story-optional">(optional)</span></h4>
          <p className="lesson-story-hint">Write a short story or passage for the lesson. Students will read it before answering questions.</p>
          <textarea
            id="lf-story"
            className="input lesson-story-textarea"
            value={form.story}
            onChange={(e) => setField('story', e.target.value)}
            rows={5}
            placeholder="Once upon a time… (leave empty if not needed)"
          />
        </div>

        <div className="lesson-edit-questions-section">
          <h4 className="lesson-edit-questions-title">Questions</h4>
          {isLoadingQuestions ? (
            <p className="children-empty" style={{ padding: '1rem 0' }}>Loading questions…</p>
          ) : (
            <QuestionEditor
              questions={form.questions}
              onChange={(qs) => setField('questions', qs)}
            />
          )}
        </div>

        {error ? <div className="alert" role="alert" style={{ marginTop: '0.75rem' }}>{error}</div> : null}

        <div className="button-row modal-actions">
          <button
            type="button"
            className="button"
            disabled={isSaving || isLoadingQuestions}
            onClick={handleSave}
          >
            {saveLabel}
          </button>
          <button type="button" className="button-secondary" onClick={onClose} disabled={isSaving}>Cancel</button>
        </div>
      </section>
    </div>,
    document.body
  )
}
