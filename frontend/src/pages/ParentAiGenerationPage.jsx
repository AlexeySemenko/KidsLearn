import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AiLessonGenerationModal from '../components/AiLessonGenerationModal'
import { editParentAiLesson } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'

const initialEditForm = {
  command: 'change-difficulty',
  difficulty: 'Hard',
  questionText: '',
  explanation: '',
  answerA: '',
  answerACorrect: true,
  answerB: '',
  answerBCorrect: false,
  removeQuestionId: '',
}

export default function ParentAiGenerationPage() {
  const { session } = useAuth()
  const [result, setResult] = useState(null)
  const [editForm, setEditForm] = useState(initialEditForm)
  const [isEditing, setIsEditing] = useState(false)
  const [editError, setEditError] = useState('')
  const [editStatus, setEditStatus] = useState('')
  const [lastRevision, setLastRevision] = useState(null)
  const [isGenerateModalOpen, setIsGenerateModalOpen] = useState(false)

  const removeQuestionOptions = useMemo(() => {
    if (!result?.lessonDraft?.questions) {
      return []
    }

    return result.lessonDraft.questions.map((question) => ({
      id: question.id,
      label: `Order ${question.order}: ${question.questionText}`,
    }))
  }, [result])

  function handleGenerated(response) {
    setEditError('')
    setEditStatus('')
    setLastRevision(null)
    setEditForm(initialEditForm)
    setResult(response)
  }

  function updateEditField(name, value) {
    setEditForm((current) => ({ ...current, [name]: value }))
  }

  function validateEditForm() {
    const command = editForm.command

    if (command === 'change-difficulty' && !editForm.difficulty.trim()) {
      return 'Difficulty is required for change-difficulty.'
    }

    if (command === 'add-question') {
      if (!editForm.questionText.trim()) {
        return 'Question text is required for add-question.'
      }

      if (!editForm.answerA.trim() || !editForm.answerB.trim()) {
        return 'Provide at least two non-empty answers for add-question.'
      }

      if (!editForm.answerACorrect && !editForm.answerBCorrect) {
        return 'At least one answer must be marked as correct.'
      }
    }

    if (command === 'remove-question' && !editForm.removeQuestionId) {
      return 'Select a question to remove.'
    }

    return null
  }

  function buildEditPayload() {
    const command = editForm.command

    if (command === 'change-difficulty') {
      return {
        command,
        params: {
          difficulty: editForm.difficulty.trim(),
        },
        answers: null,
      }
    }

    if (command === 'add-question') {
      return {
        command,
        params: {
          questionText: editForm.questionText.trim(),
          explanation: editForm.explanation.trim(),
        },
        answers: [
          { answerText: editForm.answerA.trim(), isCorrect: editForm.answerACorrect },
          { answerText: editForm.answerB.trim(), isCorrect: editForm.answerBCorrect },
        ],
      }
    }

    if (command === 'remove-question') {
      return {
        command,
        params: {
          questionId: editForm.removeQuestionId,
        },
        answers: null,
      }
    }

    return {
      command: 'regenerate-explanations',
      params: null,
      answers: null,
    }
  }

  async function handleApplyEdit(event) {
    event.preventDefault()

    if (!session?.accessToken || !result?.createdLessonId) {
      return
    }

    const validationError = validateEditForm()
    if (validationError) {
      setEditError(validationError)
      setEditStatus('')
      return
    }

    setIsEditing(true)
    setEditError('')
    setEditStatus('')

    try {
      const response = await editParentAiLesson(session.accessToken, result.createdLessonId, buildEditPayload())

      setResult((current) => {
        if (!current) {
          return current
        }

        return {
          ...current,
          lessonDraft: response.lessonDraft,
        }
      })

      setLastRevision({
        revisionNumber: response.revisionNumber,
        diffSummary: response.diffSummary,
        revisionId: response.revisionId,
      })
      setEditStatus(`Edit applied. Revision #${response.revisionNumber} created.`)

      if (response.lessonDraft.questions.length > 0 && !response.lessonDraft.questions.some((question) => question.id === editForm.removeQuestionId)) {
        setEditForm((current) => ({
          ...current,
          removeQuestionId: response.lessonDraft.questions[0].id,
        }))
      }
    } catch (requestError) {
      setEditError(requestError.message)
    } finally {
      setIsEditing(false)
    }
  }

  return (
    <section className="panel-grid">
      <article className="hero-card">
        <div className="brand-kicker">Epic 6.1 + 6.2</div>
        <h2>Generate and edit AI lesson drafts</h2>
        <p>
          Create a lesson draft from prompt parameters and review provider metadata,
          fallback status, generated questions, and revision history in one place.
        </p>
        <div className="badge-row">
          <span className="badge">POST /ai/lessons/generate</span>
          <span className="badge">POST /ai/lessons/{'{id}'}/edit</span>
          <span className="badge">422-aware errors</span>
          <span className="badge">Revision metadata</span>
        </div>
      </article>

      <article className="panel-card assignments-form-card">
        <div className="section-heading">
          <span className="section-kicker">Generation popup</span>
          <h3>Open generator</h3>
          <p>Launch the shared AI generation popup used in lessons management.</p>
        </div>

        <div className="button-row">
          <button type="button" className="button ai-launch-button" onClick={() => setIsGenerateModalOpen(true)}>
            <span className="ai-button-icon" aria-hidden="true">AI</span>
            <span>Generate lesson</span>
          </button>
        </div>

        {result?.createdLessonId ? (
          <div className="info-block">
            <strong>Lesson ID</strong>
            <span>{result.createdLessonId}</span>
          </div>
        ) : null}
      </article>

      <article className="panel-card assignments-form-card">
        <div className="section-heading">
          <span className="section-kicker">Editing</span>
          <h3>Apply AI edit command</h3>
          <p>Run supported edit commands on the generated lesson and capture revisions.</p>
        </div>

        {!result ? (
          <p className="children-empty">Generate a lesson first to unlock AI editing commands.</p>
        ) : (
          <form className="auth-form compact-form" onSubmit={handleApplyEdit}>
            <div className="field">
              <label htmlFor="ai-edit-command">Command</label>
              <select
                id="ai-edit-command"
                className="input"
                value={editForm.command}
                onChange={(event) => updateEditField('command', event.target.value)}
              >
                <option value="change-difficulty">change-difficulty</option>
                <option value="add-question">add-question</option>
                <option value="remove-question">remove-question</option>
                <option value="regenerate-explanations">regenerate-explanations</option>
              </select>
            </div>

            {editForm.command === 'change-difficulty' ? (
              <div className="field">
                <label htmlFor="ai-edit-difficulty">Difficulty</label>
                <input
                  id="ai-edit-difficulty"
                  className="input"
                  value={editForm.difficulty}
                  onChange={(event) => updateEditField('difficulty', event.target.value)}
                  placeholder="Hard"
                />
              </div>
            ) : null}

            {editForm.command === 'add-question' ? (
              <>
                <div className="field">
                  <label htmlFor="ai-edit-question-text">Question text</label>
                  <input
                    id="ai-edit-question-text"
                    className="input"
                    value={editForm.questionText}
                    onChange={(event) => updateEditField('questionText', event.target.value)}
                    placeholder="What is 7 x 8?"
                  />
                </div>
                <div className="field">
                  <label htmlFor="ai-edit-explanation">Explanation</label>
                  <input
                    id="ai-edit-explanation"
                    className="input"
                    value={editForm.explanation}
                    onChange={(event) => updateEditField('explanation', event.target.value)}
                    placeholder="Multiply seven by eight to get fifty-six."
                  />
                </div>
                <div className="field">
                  <label htmlFor="ai-edit-answer-a">Answer A</label>
                  <input
                    id="ai-edit-answer-a"
                    className="input"
                    value={editForm.answerA}
                    onChange={(event) => updateEditField('answerA', event.target.value)}
                    placeholder="56"
                  />
                </div>
                <label className="question-option">
                  <input
                    type="checkbox"
                    checked={editForm.answerACorrect}
                    onChange={(event) => updateEditField('answerACorrect', event.target.checked)}
                  />
                  <span>Mark Answer A as correct</span>
                </label>
                <div className="field">
                  <label htmlFor="ai-edit-answer-b">Answer B</label>
                  <input
                    id="ai-edit-answer-b"
                    className="input"
                    value={editForm.answerB}
                    onChange={(event) => updateEditField('answerB', event.target.value)}
                    placeholder="54"
                  />
                </div>
                <label className="question-option">
                  <input
                    type="checkbox"
                    checked={editForm.answerBCorrect}
                    onChange={(event) => updateEditField('answerBCorrect', event.target.checked)}
                  />
                  <span>Mark Answer B as correct</span>
                </label>
              </>
            ) : null}

            {editForm.command === 'remove-question' ? (
              <div className="field">
                <label htmlFor="ai-edit-remove-question">Question to remove</label>
                <select
                  id="ai-edit-remove-question"
                  className="input"
                  value={editForm.removeQuestionId}
                  onChange={(event) => updateEditField('removeQuestionId', event.target.value)}
                >
                  <option value="">Select question</option>
                  {removeQuestionOptions.map((option) => (
                    <option key={option.id} value={option.id}>{option.label}</option>
                  ))}
                </select>
              </div>
            ) : null}

            <button type="submit" className="button" disabled={isEditing}>
              {isEditing ? 'Applying...' : 'Apply edit'}
            </button>
          </form>
        )}

        {editStatus ? (
          <div className="info-block success-block assignments-status-block">
            <strong>Update</strong>
            <span>{editStatus}</span>
          </div>
        ) : null}

        {lastRevision ? (
          <div className="info-block">
            <strong>Latest revision</strong>
            <span>Revision #{lastRevision.revisionNumber}</span>
            <span>{lastRevision.diffSummary}</span>
          </div>
        ) : null}

        {editError ? <div className="alert assignments-alert">{editError}</div> : null}
      </article>

      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>Current draft snapshot</h3>
            <p>Latest generated or edited lesson draft and provider diagnostics.</p>
          </div>
          <span className="badge">{result ? 'Ready' : 'No draft yet'}</span>
        </div>

        {!result ? (
          <p className="children-empty">Run generation to preview the lesson draft.</p>
        ) : (
          <div className="children-list">
            <article className="assignment-row">
              <div className="assignment-copy">
                <div className="assignment-topline">
                  <div className="assignment-lesson-title">{result.lessonDraft.title}</div>
                  <span className={`assignment-status-pill ${result.providerMeta.fallbackUsed ? 'status-danger' : 'status-success'}`}>
                    {result.providerMeta.fallbackUsed ? 'Fallback used' : 'Primary provider'}
                  </span>
                </div>

                <div className="child-meta">
                  {result.lessonDraft.subject} · Grade {result.lessonDraft.grade} · {result.lessonDraft.topic} · {result.lessonDraft.difficulty}
                </div>

                <div className="assignment-timeline">
                  <span className="assignment-meta-chip">Provider: {result.providerMeta.provider}</span>
                  <span className="assignment-meta-chip">Model: {result.providerMeta.model}</span>
                  <span className="assignment-meta-chip">Questions: {result.lessonDraft.questions.length}</span>
                </div>

                {result.providerMeta.note ? (
                  <div className="info-block">
                    <strong>Provider note</strong>
                    <span>{result.providerMeta.note}</span>
                  </div>
                ) : null}

                <div className="button-row">
                  <Link className="button-secondary inline-link" to="/parent/lessons">
                    Open lessons workspace
                  </Link>
                </div>
              </div>
            </article>

            {result.lessonDraft.questions.map((question, index) => (
              <article key={question.id} className="assignment-row question-card">
                <div className="assignment-copy">
                  <div className="assignment-topline">
                    <div className="child-name">Question {index + 1}</div>
                    <span className="assignment-meta-chip">Order {question.order}</span>
                  </div>

                  <div>{question.questionText}</div>
                  <div className="child-meta">Explanation: {question.explanation}</div>

                  <div className="question-options">
                    {question.answers.map((answer) => (
                      <div key={answer.id} className="question-option">
                        <span>{answer.answerText}</span>
                        <span className={`assignment-status-pill ${answer.isCorrect ? 'status-success' : ''}`}>
                          {answer.isCorrect ? 'Correct' : 'Option'}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}
      </article>

      <AiLessonGenerationModal
        isOpen={isGenerateModalOpen}
        onClose={() => setIsGenerateModalOpen(false)}
        onGenerated={handleGenerated}
        idPrefix="ai-page"
        title="Generate AI lesson"
        description="Set topic and constraints for the generated lesson draft."
      />
    </section>
  )
}
