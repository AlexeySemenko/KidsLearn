import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { useAuth } from '../auth/AuthProvider'
import { generateParentAiLesson, generateStory } from '../lib/api'

const SUBJECTS = ['Math', 'English', 'Hebrew', 'Science', 'History', 'Biology', 'Art', 'Coding', 'Geography', 'Music']
const DIFFICULTIES = ['Easy', 'Medium', 'Hard']
const LANGUAGES = ['English', 'Hebrew', 'Russian']
const QUESTION_TYPE_OPTIONS = [
  { value: 'mixed',       label: 'Mixed' },
  { value: 'multiple-4',  label: 'Multiple choice (4 options)' },
  { value: 'multiple-2',  label: 'Multiple choice (2 options)' },
]

const emptyForm = {
  subject: 'Math',
  subjectCustom: '',
  grade: '2',
  topic: '',
  questionCount: '10',
  difficulty: 'Medium',
  language: 'English',
  questionTypes: 'mixed',
  includeStory: false,
}

function resolveSubject(form) {
  return form.subject === '__other__' ? form.subjectCustom.trim() : form.subject
}

function validateAiForm(form) {
  if (!resolveSubject(form)) {
    return 'Subject is required.'
  }

  if (!form.topic.trim()) {
    return 'Topic is required.'
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

function resolveQuestionTypes(value) {
  if (value === 'multiple-4') return ['multiple-choice']
  if (value === 'multiple-2') return ['true-false']
  return ['mixed']
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
  const [loadingPhase, setLoadingPhase] = useState(null) // null | 'story' | 'story-preview' | 'questions'
  const [storyPreview, setStoryPreview] = useState(null) // { story, storyImageUrl }
  const [error, setError] = useState('')
  const [providerMeta, setProviderMeta] = useState(null)

  useEffect(() => {
    if (!isOpen) {
      return undefined
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape' && !isGenerating) {
        handleClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, isGenerating])

  if (!isOpen) {
    return null
  }

  function updateField(name, value) {
    setForm((current) => ({ ...current, [name]: value }))
  }

  function handleClose() {
    setForm(emptyForm)
    setIsGenerating(false)
    setLoadingPhase(null)
    setStoryPreview(null)
    setError('')
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
      return
    }

    setError('')
    setProviderMeta(null)
    setStoryPreview(null)
    setIsGenerating(true)

    const subject = resolveSubject(form)
    const grade = Number(form.grade)
    const topic = form.topic.trim()

    try {
      let preGeneratedStory = null
      let preGeneratedStoryImageUrl = null

      if (form.includeStory) {
        setLoadingPhase('story')
        const storyResult = await generateStory(session.accessToken, {
          subject,
          grade,
          topic,
          difficulty: form.difficulty,
          language: form.language,
        })
        preGeneratedStory = storyResult.story
        preGeneratedStoryImageUrl = storyResult.storyImageUrl || null
        setStoryPreview({ story: preGeneratedStory, storyImageUrl: preGeneratedStoryImageUrl })
        setLoadingPhase('story-preview')
      } else {
        setLoadingPhase('questions')
      }

      const response = await generateParentAiLesson(session.accessToken, {
        subject,
        grade,
        topic,
        questionCount: Number(form.questionCount),
        difficulty: form.difficulty,
        language: form.language,
        questionTypes: resolveQuestionTypes(form.questionTypes),
        includeStory: form.includeStory || null,
        preGeneratedStory,
        preGeneratedStoryImageUrl,
      })

      setProviderMeta(response.providerMeta)

      if (typeof onGenerated === 'function') {
        onGenerated(response)
      }

      handleClose()
    } catch (requestError) {
      if (requestError.status === 422) {
        setError(`AI validation failed: ${requestError.message}`)
      } else {
        setError(requestError.message)
      }
    } finally {
      setIsGenerating(false)
      setLoadingPhase(null)
      setStoryPreview(null)
    }
  }

  return createPortal(
    <div className="modal-overlay" role="presentation">
      <section
        className="modal-card lesson-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={`${idPrefix}-title`}
      >
        <div className="children-list-header modal-header">
          <div>
            <h3 id={`${idPrefix}-title`}>{title}</h3>
            <p>{description}</p>
          </div>
          <button type="button" className="button-secondary" onClick={handleClose} disabled={isGenerating}>Close</button>
        </div>

        {isGenerating ? (
          <div className="ai-generating">
            {loadingPhase === 'story-preview' && storyPreview ? (
              <div className="story-preview-block">
                {storyPreview.storyImageUrl ? (
                  <img
                    className="story-preview-image"
                    src={storyPreview.storyImageUrl}
                    alt="Story illustration"
                  />
                ) : null}
                <p className="story-preview-text">{storyPreview.story}</p>
              </div>
            ) : (
              <div className="ai-generating-rings">
                <div className="ai-ring" />
                <div className="ai-ring" />
                <div className="ai-ring" />
                <div className="ai-orb">AI</div>
              </div>
            )}
            <p className="ai-generating-label">
              {loadingPhase === 'story' && 'Generating story...'}
              {loadingPhase === 'story-preview' && 'Story ready! Generating questions...'}
              {loadingPhase === 'questions' && 'Generating questions...'}
              {!loadingPhase && 'Generating AI lesson'}
            </p>
            {loadingPhase !== 'story-preview' ? (
              <>
                <p className="ai-generating-hint" style={{ marginTop: 0 }}>AI quality checks may take a few moments...</p>
                <div className="ai-generating-dots">
                  <span /><span /><span />
                </div>
              </>
            ) : (
              <div className="ai-generating-dots">
                <span /><span /><span />
              </div>
            )}
            {form.topic && loadingPhase !== 'story-preview' ? (
              <p className="ai-generating-hint">
                {resolveSubject(form)} · Grade {form.grade}<br />{form.topic}
              </p>
            ) : null}
          </div>
        ) : (
          <form className="auth-form compact-form" onSubmit={handleGenerate}>
            <div className="lessons-form-grid">
              <div className="field">
                <label htmlFor={`${idPrefix}-subject`}>Subject</label>
                <select
                  id={`${idPrefix}-subject`}
                  className="input"
                  value={form.subject}
                  onChange={(event) => updateField('subject', event.target.value)}
                >
                  {SUBJECTS.map((s) => <option key={s} value={s}>{s}</option>)}
                  <option value="__other__">Other…</option>
                </select>
                {form.subject === '__other__' ? (
                  <input
                    className="input"
                    style={{ marginTop: '0.4rem' }}
                    value={form.subjectCustom}
                    onChange={(event) => updateField('subjectCustom', event.target.value)}
                    placeholder="Enter subject"
                    autoFocus
                  />
                ) : null}
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
                  autoFocus
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
                placeholder="e.g. Equivalent fractions"
                required
              />
            </div>

            <div className="lessons-form-grid">
              <div className="field">
                <label htmlFor={`${idPrefix}-question-count`}>Questions</label>
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
                <select
                  id={`${idPrefix}-difficulty`}
                  className="input"
                  value={form.difficulty}
                  onChange={(event) => updateField('difficulty', event.target.value)}
                >
                  {DIFFICULTIES.map((d) => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
            </div>

            <div className="lessons-form-grid">
              <div className="field">
                <label htmlFor={`${idPrefix}-language`}>Language</label>
                <select
                  id={`${idPrefix}-language`}
                  className="input"
                  value={form.language}
                  onChange={(event) => updateField('language', event.target.value)}
                >
                  {LANGUAGES.map((l) => <option key={l} value={l}>{l}</option>)}
                </select>
              </div>

              <div className="field">
                <label htmlFor={`${idPrefix}-question-types`}>Question type</label>
                <select
                  id={`${idPrefix}-question-types`}
                  className="input"
                  value={form.questionTypes}
                  onChange={(event) => updateField('questionTypes', event.target.value)}
                >
                  {QUESTION_TYPE_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                  ))}
                </select>
              </div>
            </div>

            <div className="field">
              <label className="ai-checkbox-label">
                <input
                  type="checkbox"
                  checked={form.includeStory}
                  onChange={(e) => updateField('includeStory', e.target.checked)}
                />
                <span>
                  <strong>Include lesson story</strong>
                  <span className="ai-checkbox-hint"> — AI will write a short narrative and base questions on it</span>
                </span>
              </label>
            </div>

            <div className="button-row modal-actions">
              <button type="submit" className="button">Generate lesson</button>
              <button type="button" className="button-secondary" onClick={handleClose}>Cancel</button>
            </div>
          </form>
        )}

        {error ? <div className="alert lessons-alert" role="alert" aria-live="assertive">{error}</div> : null}
      </section>
    </div>,
    document.body
  )
}
