import { useState } from 'react'
import { Link } from 'react-router-dom'
import AiLessonGenerationModal from '../components/AiLessonGenerationModal'

export default function ParentAiGenerationPage() {
  const [result, setResult] = useState(null)
  const [isGenerateModalOpen, setIsGenerateModalOpen] = useState(false)

  function handleGenerated(response) {
    setResult(response)
  }

  return (
    <section className="panel-grid">
      <article className="hero-card">
        <div className="brand-kicker">Epic 6.1</div>
        <h2>Generate AI lesson drafts</h2>
        <p>
          Create a lesson draft from prompt parameters and review provider metadata,
          fallback status, and generated questions in one place.
        </p>
        <div className="badge-row">
          <span className="badge">POST /ai/lessons/generate</span>
          <span className="badge">422-aware errors</span>
          <span className="badge">Provider fallback signal</span>
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
      </article>

      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>Generated draft</h3>
            <p>Latest generated lesson draft and provider diagnostics.</p>
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
