import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getChildResultDetail } from '../lib/api'

function formatDate(value) {
  if (!value) {
    return 'Unknown date'
  }

  return new Date(value).toLocaleString()
}

function scoreVariant(score) {
  if (score >= 85) {
    return 'status-success'
  }

  if (score >= 60) {
    return ''
  }

  return 'status-danger'
}

export default function ChildResultsPage() {
  const { session } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const initialResultId = searchParams.get('resultId') ?? ''
  const [resultIdInput, setResultIdInput] = useState(initialResultId)
  const [result, setResult] = useState(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const breakdown = useMemo(() => result?.breakdown ?? [], [result])

  async function handleLoadResult(event) {
    event.preventDefault()

    if (!session?.accessToken) {
      return
    }

    const normalizedResultId = resultIdInput.trim()
    if (!normalizedResultId) {
      setError('Result ID is required.')
      setResult(null)
      return
    }

    setIsLoading(true)
    setError('')

    try {
      const response = await getChildResultDetail(session.accessToken, normalizedResultId)
      setResult(response)
      setSearchParams({ resultId: normalizedResultId })
    } catch (requestError) {
      setResult(null)
      setError(requestError.message)
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <section className="panel-grid">
      <article className="hero-card">
        <div className="brand-kicker">Epic 4.3</div>
        <h2>Child result details</h2>
        <p>
          Load a completed assignment result to review score and per-question correctness.
          This screen calls the protected child result endpoint.
        </p>
        <div className="badge-row">
          <span className="badge">GET /child/results/{'{resultId}'}</span>
          <span className="badge">Child-only access</span>
          <span className="badge">Score + breakdown</span>
        </div>
      </article>

      <article className="panel-card assignments-form-card">
        <h3>Open result</h3>
        <p>Enter a result ID or use the View result details button after completing an assignment.</p>

        <form className="auth-form compact-form" onSubmit={handleLoadResult}>
          <div className="field">
            <label htmlFor="result-id-input">Result ID</label>
            <input
              id="result-id-input"
              className="input"
              value={resultIdInput}
              onChange={(event) => setResultIdInput(event.target.value)}
              placeholder="00000000-0000-0000-0000-000000000000"
              required
            />
          </div>

          <button type="submit" className="button" disabled={isLoading}>
            {isLoading ? 'Loading...' : 'Load result'}
          </button>
        </form>

        {error ? <div className="alert assignments-alert">{error}</div> : null}
      </article>

      <article className="assignments-list-card">
        <div className="children-list-header">
          <div>
            <h3>Result detail</h3>
            <p>Current result payload from the backend.</p>
          </div>
          <span className="badge">{result ? 'Loaded' : 'Not loaded'}</span>
        </div>

        {!result ? (
          <p className="children-empty">Load a result to see score and correctness breakdown.</p>
        ) : (
          <div className="children-list">
            <article className="assignment-row">
              <div className="assignment-copy">
                <div className="assignment-topline">
                  <div className="child-name">Result {result.resultId}</div>
                  <span className={`assignment-status-pill ${scoreVariant(result.score)}`}>Score {result.score}%</span>
                </div>

                <div className="child-meta">Assignment {result.assignmentId}</div>
                <div className="assignment-timeline">
                  <span className="assignment-meta-chip">Completed {formatDate(result.completedAt)}</span>
                  <span className="assignment-meta-chip">Correct {result.correctAnswers}/{result.totalQuestions}</span>
                </div>
              </div>
            </article>

            {breakdown.length > 0 ? (
              <article className="assignment-row">
                <div className="assignment-copy">
                  <div className="child-name">Question breakdown</div>
                  <div className="children-list">
                    {breakdown.map((item, index) => (
                      <div key={`${item.questionId}-${index}`} className="assignment-timeline">
                        <span className="assignment-meta-chip">Q{index + 1}</span>
                        <span className={`assignment-status-pill ${item.correct ? 'status-success' : 'status-danger'}`}>
                          {item.correct ? 'Correct' : 'Incorrect'}
                        </span>
                        <span className="assignment-meta-chip">{item.questionId}</span>
                      </div>
                    ))}
                  </div>
                </div>
              </article>
            ) : null}
          </div>
        )}
      </article>
    </section>
  )
}
