export function detectAnswerType(answers) {
  if (answers.length === 2) {
    const texts = answers.map((a) => a.answerText.toLowerCase())
    if (texts.includes('true') && texts.includes('false')) return 'true_false'
  }
  return 'multiple_choice'
}

export function questionsFromDetail(questions) {
  return questions.map((q) => ({
    id: q.id,
    questionText: q.questionText,
    explanation: q.explanation || '',
    answerType: detectAnswerType(q.answers),
    answers: q.answers.map((a) => ({ answerText: a.answerText, isCorrect: a.isCorrect })),
    mcAnswers: null, // populated when switching to true_false
  }))
}

export default function QuestionEditor({ questions, onChange }) {
  function updateQuestion(index, patch) {
    onChange(questions.map((q, i) => (i === index ? { ...q, ...patch } : q)))
  }

  function removeQuestion(index) {
    onChange(questions.filter((_, i) => i !== index))
  }

  function addQuestion() {
    onChange([
      ...questions,
      {
        id: null,
        questionText: '',
        explanation: '',
        answerType: 'multiple_choice',
        mcAnswers: null,
        answers: [
          { answerText: '', isCorrect: true },
          { answerText: '', isCorrect: false },
        ],
      },
    ])
  }

  function updateAnswer(qIndex, aIndex, patch) {
    const q = questions[qIndex]
    updateQuestion(qIndex, { answers: q.answers.map((a, i) => (i === aIndex ? { ...a, ...patch } : a)) })
  }

  function removeAnswer(qIndex, aIndex) {
    const q = questions[qIndex]
    updateQuestion(qIndex, { answers: q.answers.filter((_, i) => i !== aIndex) })
  }

  function addAnswer(qIndex) {
    const q = questions[qIndex]
    updateQuestion(qIndex, { answers: [...q.answers, { answerText: '', isCorrect: false }] })
  }

  function changeAnswerType(qIndex, type) {
    const q = questions[qIndex]
    if (type === 'true_false') {
      // Stash current MC answers so we can restore them if user switches back
      const trueIsCorrect = q.answers.some((a) => a.isCorrect)
      updateQuestion(qIndex, {
        answerType: 'true_false',
        mcAnswers: q.answers,
        answers: [
          { answerText: 'True', isCorrect: trueIsCorrect },
          { answerText: 'False', isCorrect: !trueIsCorrect },
        ],
      })
    } else {
      // Restore stashed MC answers, or keep a blank pair if none saved
      const restored = q.mcAnswers && q.mcAnswers.length >= 2
        ? q.mcAnswers
        : [{ answerText: '', isCorrect: true }, { answerText: '', isCorrect: false }]
      updateQuestion(qIndex, {
        answerType: 'multiple_choice',
        mcAnswers: null,
        answers: restored,
      })
    }
  }

  return (
    <div className="question-editor">
      {questions.map((q, qi) => (
        <div key={qi} className="question-editor-item">
          <div className="question-editor-item-header">
            <span className="question-editor-num">Q{qi + 1}</span>
            <button
              type="button"
              className="question-editor-remove-btn"
              onClick={() => removeQuestion(qi)}
              title="Remove question"
              aria-label={`Remove question ${qi + 1}`}
            >
              ×
            </button>
          </div>

          <div className="field">
            <label htmlFor={`qe-${qi}-text`}>Question</label>
            <textarea
              id={`qe-${qi}-text`}
              className="input"
              rows={2}
              value={q.questionText}
              onChange={(e) => updateQuestion(qi, { questionText: e.target.value })}
              placeholder="Enter question text…"
            />
          </div>

          <div className="field">
            <label htmlFor={`qe-${qi}-expl`}>Explanation</label>
            <textarea
              id={`qe-${qi}-expl`}
              className="input"
              rows={2}
              value={q.explanation}
              onChange={(e) => updateQuestion(qi, { explanation: e.target.value })}
              placeholder="Explain the correct answer (optional)…"
            />
          </div>

          <div className="field">
            <label>Answer type</label>
            <div className="answer-type-toggle">
              <button
                type="button"
                className={`answer-type-btn${q.answerType === 'multiple_choice' ? ' active' : ''}`}
                onClick={() => changeAnswerType(qi, 'multiple_choice')}
              >
                Multiple choice
              </button>
              <button
                type="button"
                className={`answer-type-btn${q.answerType === 'true_false' ? ' active' : ''}`}
                onClick={() => changeAnswerType(qi, 'true_false')}
              >
                True / False
              </button>
            </div>
          </div>

          {q.answerType === 'true_false' ? (
            <div className="answer-list">
              {['True', 'False'].map((label, ai) => (
                <div key={label} className="answer-tf-row">
                  <input
                    type="radio"
                    id={`qe-${qi}-tf-${ai}`}
                    name={`qe-${qi}-tf`}
                    checked={q.answers[ai]?.isCorrect ?? ai === 0}
                    onChange={() =>
                      updateQuestion(qi, {
                        answers: [
                          { answerText: 'True', isCorrect: ai === 0 },
                          { answerText: 'False', isCorrect: ai === 1 },
                        ],
                      })
                    }
                  />
                  <label htmlFor={`qe-${qi}-tf-${ai}`}>{label}</label>
                </div>
              ))}
            </div>
          ) : (
            <div className="answer-list">
              {q.answers.map((answer, ai) => (
                <div key={ai} className="answer-mc-row">
                  <input
                    type="checkbox"
                    id={`qe-${qi}-a${ai}-correct`}
                    checked={answer.isCorrect}
                    onChange={(e) => updateAnswer(qi, ai, { isCorrect: e.target.checked })}
                    title="Mark as correct"
                  />
                  <input
                    className="input answer-text-input"
                    value={answer.answerText}
                    onChange={(e) => updateAnswer(qi, ai, { answerText: e.target.value })}
                    placeholder={`Answer ${ai + 1}`}
                  />
                  <button
                    type="button"
                    className="answer-remove-btn"
                    onClick={() => removeAnswer(qi, ai)}
                    title="Remove answer"
                    aria-label={`Remove answer ${ai + 1}`}
                    disabled={q.answers.length <= 2}
                  >
                    ×
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="button-secondary answer-add-btn"
                onClick={() => addAnswer(qi)}
              >
                + Add answer
              </button>
            </div>
          )}
        </div>
      ))}

      <button type="button" className="button-secondary question-add-btn" onClick={addQuestion}>
        + Add question
      </button>
    </div>
  )
}
