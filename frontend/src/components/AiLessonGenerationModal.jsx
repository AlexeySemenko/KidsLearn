import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthProvider'
import { generateParentAiLesson } from '../lib/api'

const emptyForm = {
  subject: '',
  grade: '3',
  topic: '',
  questionCount: '5',
  difficulty: 'Medium',
  language: 'English',
  questionTypes: '',
}

function validateAiForm(form) {
  if (!form.subject.trim() || !form.topic.trim()) {
    return 'Subject and topic are required.'
  }

  const grade = Number(form.grade)
  if (!Number.isInteger(grade) || grade < 1 || grade > 12) {
    return 'Grade must be a whole number between 1 and 12.'
  }

  const questionCount = Number(form.questionCount)
  if (!Number.isInteger(questionCount) || questionCount < 1 || questionCount > 25) {
    return 'Question count must be between 1 and 25.'
  }

  return null
}

function parseQuestionTypes(value) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

export default function AiLessonGenerationModal({
  isOpen,
  onClose,
  onGenerated,
  idPrefix = 'ai-generate',
  title = 'Generate AI lesson',
  description = 'Create a lesson draft from prompt settings.',
}) {
  const { session } = useAuth()
  const [form, setForm] = useState(emptyForm)
  const [isGenerating, setIsGenerating] = useState(false)
  const [error, setError] = useState('')
  const [status, setStatus] = useState('')
  const [providerMeta, setProviderMeta] = useState(null)

  useEffect(() => {
    if (!isOpen) {
      return undefined
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        handleClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isOpen])

  if (!isOpen) {
    return null
  }

  function updateField(name, value) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  function handleClose() {
    setForm(emptyForm)
    setIsGenerating(false)
    setError('')
    setStatus('')
    setProviderMeta(null)
    onClose()
  }

  async function handleGenerate(event) {
    event.preventDefault()

    if (!session?.accessToken) {
      return
    }

    const validationError = validateAiForm(form)
    if (validationError) {
      setError(validationError)
      setStatus('')
      return
    }

    setError('')
    setStatus('')
    setProviderMeta(null)
    setIsGenerating(true)

    try {
      const questionTypes = parseQuestionTypes(form.questionTypes)
      const response = await generateParentAiLesson(session.accessToken, {
        subject: form.subject.trim(),
        grade: Number(form.grade),
        topic: form.topic.trim(),
        questionCount: Number(form.questionCount),
        difficulty: form.difficulty.trim() || null,
        language: form.language.trim() || null,
        questionTypes: questionTypes.length > 0 ? questionTypes : null,
      })

      setProviderMeta(response.providerMeta)
      if (response.providerMeta.fallbackUsed) {
        setStatus('Lesson generated with fallback mode.')
      } else {
        setStatus('Lesson generated successfully.')
      }

      if (typeof onGenerated === 'function') {
        onGenerated(response)
      }
    } catch (requestError) {
      if (requestError.status === 422) {
        setError(`AI schema validation failed: ${requestError.message}`)
      } else {
        setError(requestError.message)
      }
    } finally {
      setIsGenerating(false)
    }
  }

  return (
    <div className="modal-overlay" role="presentation" onClick={handleClose}>
      <section
        className="modal-card lesson-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={`${idPrefix}-title`}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="children-list-header modal-header">
          <div>
            <h3 id={`${idPrefix}-title`}>{title}</h3>
            <p>{description}</p>
          </div>
          <button type="button" className="button-secondary" onClick={handleClose}>Close</button>
        </div>

        <form className="auth-form compact-form" onSubmit={handleGenerate}>
          <div className="lessons-form-grid">
            <div className="field">
              <label htmlFor={`${idPrefix}-subject`}>Subject</label>
              <input
                id={`${idPrefix}-subject`}
                className="input"
                value={form.subject}
                onChange={(event) => updateField('subject', event.target.value)}
                placeholder="Math"
                autoFocus
                required
              />
            </div>

            <div className="field">
              <label htmlFor={`${idPrefix}-grade`}>Grade</label>
              <input
                id={`${idPrefix}-grade`}
                className="input"
                type="number"
                min="1"
                max="12"
                value={form.grade}
                onChange={(event) => updateField('grade', event.target.value)}
                required
              />
            </div>
          </div>

          <div className="field">
            <label htmlFor={`${idPrefix}-topic`}>Topic</label>
            <input
              id={`${idPrefix}-topic`}
              className="input"
              value={form.topic}
              onChange={(event) => updateField('topic', event.target.value)}
              placeholder="Equivalent fractions"
              required
            />
          </div>

          <div className="lessons-form-grid">
            <div className="field">
              <label htmlFor={`${idPrefix}-question-count`}>Question count</label>
              <input
                id={`${idPrefix}-question-count`}
                className="input"
                type="number"
                min="1"
                max="25"
                value={form.questionCount}
                onChange={(event) => updateField('questionCount', event.target.value)}
                required
              />
            </div>

            <div className="field">
              <label htmlFor={`${idPrefix}-difficulty`}>Difficulty</label>
              <input
                id={`${idPrefix}-difficulty`}
                className="input"
                value={form.difficulty}
                onChange={(event) => updateField('difficulty', event.target.value)}
                placeholder="Medium"
              />
            </div>
          </div>

          <div className="lessons-form-grid">
            <div className="field">
              <label htmlFor={`${idPrefix}-language`}>Language</label>
              <input
                id={`${idPrefix}-language`}
                className="input"
                value={form.language}
                onChange={(event) => updateField('language', event.target.value)}
                placeholder="English"
              />
            </div>

            <div className="field">
              <label htmlFor={`${idPrefix}-question-types`}>Question types</label>
              <input
                id={`${idPrefix}-question-types`}
                className="input"
                value={form.questionTypes}
                onChange={(event) => updateField('questionTypes', event.target.value)}
                placeholder="multiple-choice, true-false"
              />
            </div>
          </div>

          <div className="button-row modal-actions">
            <button type="submit" className="button" disabled={isGenerating}>
              {isGenerating ? 'Generating...' : 'Generate lesson'}
            </button>
            <button type="button" className="button-secondary" onClick={handleClose}>Cancel</button>
          </div>
        </form>

        {status ? (
          <div className="info-block success-block lessons-status-block" role="status" aria-live="polite">
            <strong>Update</strong>
            <span>{status}</span>
            {providerMeta?.note ? <span>{providerMeta.note}</span> : null}
          </div>
        ) : null}

        {error ? <div className="alert lessons-alert" role="alert" aria-live="assertive">{error}</div> : null}
      </section>
    </div>
  )
}
