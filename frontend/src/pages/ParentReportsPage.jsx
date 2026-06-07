import { useEffect, useMemo, useState } from 'react'
import { getChildren, exportParentChildReportCsv, getParentChildReportSummary } from '../lib/api'
import { useAuth } from '../auth/AuthProvider'
import DropdownSelect from '../components/DropdownSelect'

function toIsoOrNull(value, boundary) {
  if (!value) {
    return null
  }

  return boundary === 'end' ? new Date(`${value}T23:59:59.999`).toISOString() : new Date(`${value}T00:00:00.000`).toISOString()
}

function clampPercent(value) {
  if (!Number.isFinite(value)) {
    return 0
  }

  return Math.max(0, Math.min(100, value * 100))
}

export default function ParentReportsPage() {
  const { session } = useAuth()
  const [children, setChildren] = useState([])
  const [summary, setSummary] = useState(null)
  const [isLoadingChildren, setIsLoadingChildren] = useState(true)
  const [isLoadingSummary, setIsLoadingSummary] = useState(false)
  const [isExporting, setIsExporting] = useState(false)
  const [error, setError] = useState('')
  const [statusMessage, setStatusMessage] = useState('')
  const [filters, setFilters] = useState({ childId: '', from: '', to: '' })

  const childOptions = useMemo(
    () => children.map((child) => ({
      value: child.id,
      label: child.name,
      description: `Grade ${child.grade}`,
    })),
    [children],
  )

  useEffect(() => {
    let isMounted = true

    async function loadChildren() {
      if (!session?.accessToken) {
        if (isMounted) {
          setIsLoadingChildren(false)
        }
        return
      }

      try {
        setError('')
        const response = await getChildren(session.accessToken)
        if (!isMounted) {
          return
        }

        setChildren(response)
        if (response.length > 0) {
          setFilters((current) => ({ ...current, childId: current.childId || response[0].id }))
        }
      } catch (requestError) {
        if (isMounted) {
          setError(requestError.message)
        }
      } finally {
        if (isMounted) {
          setIsLoadingChildren(false)
        }
      }
    }

    loadChildren()

    return () => {
      isMounted = false
    }
  }, [session?.accessToken])

  function updateFilter(name, value) {
    setFilters((current) => ({ ...current, [name]: value }))
  }

  function resolveRange() {
    if (!filters.from && !filters.to) {
      return null
    }

    const fromIso = toIsoOrNull(filters.from, 'start')
    const toIso = toIsoOrNull(filters.to, 'end')

    if (fromIso && toIso && new Date(fromIso) > new Date(toIso)) {
      return { error: 'From date must be before To date.' }
    }

    return {
      value: {
        from: fromIso,
        to: toIso,
      },
    }
  }

  async function handleLoadSummary(event) {
    event.preventDefault()
    if (!session?.accessToken) {
      return
    }

    if (!filters.childId) {
      setError('Select a child to load report summary.')
      setStatusMessage('')
      return
    }

    const range = resolveRange()
    if (range?.error) {
      setError(range.error)
      setStatusMessage('')
      return
    }

    setError('')
    setStatusMessage('')
    setIsLoadingSummary(true)

    try {
      const response = await getParentChildReportSummary(session.accessToken, filters.childId, range?.value)
      setSummary(response)
      setStatusMessage('Summary loaded successfully.')
    } catch (requestError) {
      setSummary(null)
      setError(requestError.message)
    } finally {
      setIsLoadingSummary(false)
    }
  }

  async function handleExportCsv() {
    if (!session?.accessToken) {
      return
    }

    if (!filters.childId) {
      setError('Select a child before exporting CSV.')
      setStatusMessage('')
      return
    }

    const range = resolveRange()
    if (range?.error) {
      setError(range.error)
      setStatusMessage('')
      return
    }

    setError('')
    setStatusMessage('')
    setIsExporting(true)

    try {
      const response = await exportParentChildReportCsv(session.accessToken, filters.childId, range?.value)
      const downloadUrl = URL.createObjectURL(response.fileBlob)
      const anchor = document.createElement('a')
      anchor.href = downloadUrl
      anchor.download = response.fileName
      document.body.appendChild(anchor)
      anchor.click()
      document.body.removeChild(anchor)
      URL.revokeObjectURL(downloadUrl)
      setStatusMessage(`CSV exported: ${response.fileName}`)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setIsExporting(false)
    }
  }

  const completionPercent = summary ? clampPercent(Number(summary.completionRate)) : 0
  const scorePercent = summary ? clampPercent(Number(summary.averageScore)) : 0

  return (
    <section className="panel-grid">
      <article className="hero-card reports-hero">
        <div className="brand-kicker">Epic 5</div>
        <h2>Child reports and export are now connected to the API.</h2>
        <p>
          Select a child, apply an optional date range, review summary metrics,
          and export CSV for external analysis.
        </p>
        <div className="badge-row">
          <span className="badge">GET /reports/children/{'{id}'}</span>
          <span className="badge">GET /reports/children/{'{id}'}/export?format=csv</span>
          <span className="badge">Date range filters</span>
        </div>
      </article>

      <article className="panel-card reports-filter-card">
        <div className="section-heading">
          <span className="section-kicker">Filters</span>
          <h3>Child report query</h3>
          <p>Use date bounds only when you want a focused reporting window.</p>
        </div>

        <form className="auth-form compact-form" onSubmit={handleLoadSummary}>
          <DropdownSelect
            id="report-child"
            label="Child"
            placeholder={isLoadingChildren ? 'Loading children...' : 'Select child'}
            value={filters.childId}
            options={childOptions}
            onChange={(nextValue) => updateFilter('childId', nextValue)}
            disabled={isLoadingChildren || children.length === 0}
            helperText="Pick a child profile to build the summary."
            showHelperHint={false}
            searchable
            searchPlaceholder="Search child"
            size="compact"
          />

          <div className="reports-filter-grid">
            <div className="field">
              <label htmlFor="report-from">From</label>
              <input
                id="report-from"
                type="date"
                className="input"
                value={filters.from}
                onChange={(event) => updateFilter('from', event.target.value)}
              />
            </div>

            <div className="field">
              <label htmlFor="report-to">To</label>
              <input
                id="report-to"
                type="date"
                className="input"
                value={filters.to}
                onChange={(event) => updateFilter('to', event.target.value)}
              />
            </div>
          </div>

          <div className="button-row reports-action-row">
            <button
              type="submit"
              className="button"
              disabled={isLoadingChildren || children.length === 0 || isLoadingSummary}
            >
              {isLoadingSummary ? 'Loading...' : 'Load summary'}
            </button>
            <button
              type="button"
              className="button-secondary"
              disabled={isLoadingChildren || children.length === 0 || isExporting}
              onClick={handleExportCsv}
            >
              {isExporting ? 'Exporting...' : 'Export CSV'}
            </button>
          </div>
        </form>

        {statusMessage ? (
          <div className="info-block success-block reports-status-block" role="status" aria-live="polite">
            <strong>Update</strong>
            <span>{statusMessage}</span>
          </div>
        ) : null}

        {error ? <div className="alert reports-alert" role="alert" aria-live="assertive">{error}</div> : null}
      </article>

      <article className="reports-summary-card">
        <div className="children-list-header">
          <div>
            <h3>Summary</h3>
            <p>Latest child learning performance metrics from report endpoints.</p>
          </div>
          <span className="badge">{summary ? 'Live data' : 'Awaiting query'}</span>
        </div>

        {!summary ? (
          <p className="children-empty">No summary loaded yet. Choose filters and click Load summary.</p>
        ) : (
          <div className="reports-metrics-grid">
            <article className="report-metric-card">
              <span className="section-kicker">Completion</span>
              <strong>{completionPercent.toFixed(1)}%</strong>
              <div className="report-meter" role="img" aria-label={`Completion ${completionPercent.toFixed(1)} percent`}>
                <span style={{ width: `${completionPercent}%` }} />
              </div>
            </article>

            <article className="report-metric-card">
              <span className="section-kicker">Average score</span>
              <strong>{scorePercent.toFixed(1)}%</strong>
              <div className="report-meter" role="img" aria-label={`Average score ${scorePercent.toFixed(1)} percent`}>
                <span style={{ width: `${scorePercent}%` }} />
              </div>
            </article>

            <article className="report-metric-card">
              <span className="section-kicker">Solved assignments</span>
              <strong>{summary.solvedCount}</strong>
              <p>Total solved assignments in selected period.</p>
            </article>

            <article className="report-metric-card">
              <span className="section-kicker">Current streak</span>
              <strong>{summary.streakDays} days</strong>
              <p>Consecutive active learning days.</p>
            </article>
          </div>
        )}
      </article>
    </section>
  )
}