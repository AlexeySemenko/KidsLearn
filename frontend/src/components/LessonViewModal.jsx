import { useEffect } from 'react'
import { createPortal } from 'react-dom'

function DefaultQuestion({ question, index }) {
  return (
    <article className="assignment-row question-card">
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
              key={answer.id}
              className={`question-option${answer.isCorrect ? ' correct-answer' : ''}`}
            >
              <span>{answer.answerText}</span>
              {answer.isCorrect ? <span className="answer-correct-badge">Correct</span> : null}
            </div>
          ))}
        </div>
      </div>
    </article>
  )
}

export default function LessonViewModal({
  title,
  subtitle,
  story,
  storyImageUrl,
  questions,
  onClose,
  renderQuestion,
  footer,
}) {
  useEffect(() => {
    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onClose])

  return createPortal(
    <div className="modal-overlay lesson-view-overlay" role="presentation">
      <section
        className="modal-card lesson-modal lesson-view-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="lesson-view-modal-title"
      >
        <div className="children-list-header modal-header">
          <div>
            <h3 id="lesson-view-modal-title">{title}</h3>
            {subtitle ? <p>{subtitle}</p> : null}
          </div>
          <button type="button" className="button-secondary" onClick={onClose}>Close</button>
        </div>

        {story ? (
          <div className="lesson-story-block">
            <div className="lesson-story-label">📖 Story</div>
            <div className="story-section">
              {storyImageUrl ? (
                <img className="story-image" src={storyImageUrl} alt="Story illustration" />
              ) : null}
              <p className="story-text">{story}</p>
            </div>
          </div>
        ) : null}

        <div className="children-list">
          {questions.map((question, index) =>
            renderQuestion
              ? renderQuestion(question, index)
              : <DefaultQuestion key={question.id} question={question} index={index} />
          )}
        </div>

        {footer ?? (
          <div className="button-row modal-actions">
            <button type="button" className="button-secondary" onClick={onClose}>Close</button>
          </div>
        )}
      </section>
    </div>,
    document.body
  )
}
