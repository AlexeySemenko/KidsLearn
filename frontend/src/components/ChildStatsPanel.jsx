import { useMemo } from 'react'

export const SUBJECT_EMOJI = { Math: '🔢', English: '📖', Hebrew: '✡️' }
export const SUBJECT_COLOR = { Math: '#60a5fa', English: '#34d399', Hebrew: '#a78bfa' }

export function scoreEmoji(score) {
  if (score >= 90) return '🌟'
  if (score >= 70) return '😊'
  if (score >= 50) return '👍'
  return '😅'
}

export function scoreVariant(score) {
  if (score >= 85) return 'status-success'
  if (score >= 60) return ''
  return 'status-danger'
}

function localDate(isoString) {
  const d = new Date(isoString)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function computeStats(results) {
  if (!results.length) return null

  const avgScore = Math.round(results.reduce((s, r) => s + r.score, 0) / results.length)

  const subjectMap = {}
  results.forEach((r) => {
    const key = r.subject || 'Other'
    if (!subjectMap[key]) subjectMap[key] = { total: 0, count: 0, topics: {} }
    subjectMap[key].total += r.score
    subjectMap[key].count++
    const topic = r.topic?.trim()
    if (topic) {
      subjectMap[key].topics[topic] = (subjectMap[key].topics[topic] || 0) + 1
    }
  })
  const subjectList = Object.entries(subjectMap)
    .map(([name, { total, count, topics }]) => ({
      name,
      avg: Math.round(total / count),
      count,
      topTopics: Object.entries(topics)
        .sort((a, b) => b[1] - a[1])
        .slice(0, 3)
        .map(([topic, times]) => ({ topic, times })),
    }))
    .sort((a, b) => b.count - a.count)

  const bestSubject = [...subjectList].sort((a, b) => b.avg - a.avg)[0] ?? null

  const today = new Date()
  const weekDays = []
  for (let i = 6; i >= 0; i--) {
    const d = new Date(today)
    d.setDate(d.getDate() - i)
    const ds = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
    const count = results.filter((r) => localDate(r.completedAt) === ds).length
    weekDays.push({
      label: d.toLocaleDateString('en', { weekday: 'short' }),
      count,
      isToday: i === 0,
    })
  }

  const completedDaySet = new Set(results.map((r) => localDate(r.completedAt)))
  let streak = 0
  const cursor = new Date()
  while (completedDaySet.has(localDate(cursor.toISOString()))) {
    streak++
    cursor.setDate(cursor.getDate() - 1)
  }

  return { avgScore, subjectList, bestSubject, weekDays, streak, total: results.length }
}

export default function ChildStatsPanel({ results, isLoading, pendingCount = 0 }) {
  const stats = useMemo(() => computeStats(results), [results])
  const maxWeekCount = stats ? Math.max(...stats.weekDays.map((d) => d.count), 1) : 1

  if (isLoading) {
    return <p className="children-empty" style={{ padding: '2rem 0' }}>Loading stats...</p>
  }

  if (!stats) {
    return (
      <div className="child-empty-stats">
        <span className="child-empty-stats-icon">🎯</span>
        <p>No completed missions yet.</p>
      </div>
    )
  }

  return (
    <div className="child-stats-panel">
      <div className="child-stats-grid">
        <div className="child-stat-card child-stat-card--split" data-color="blue">
          <div className="child-stat-split-half">
            <span className="child-stat-icon">⏳</span>
            <span className="child-stat-value">{pendingCount}</span>
            <span className="child-stat-label">Lessons waiting</span>
          </div>
          <div className="child-stat-split-divider" />
          <div className="child-stat-split-half">
            <span className="child-stat-icon">🚀</span>
            <span className="child-stat-value">{stats.total}</span>
            <span className="child-stat-label">Lessons done</span>
          </div>
        </div>
        <div className="child-stat-card" data-color="yellow">
          <span className="child-stat-icon">{scoreEmoji(stats.avgScore)}</span>
          <span className="child-stat-value">{stats.avgScore}%</span>
          <span className="child-stat-label">Average score</span>
        </div>
        <div className="child-stat-card" data-color="orange">
          <span className="child-stat-icon">🔥</span>
          <span className="child-stat-value">{stats.streak}</span>
          <span className="child-stat-label">Day streak</span>
        </div>
        <div className="child-stat-card" data-color="purple">
          <span className="child-stat-icon">{SUBJECT_EMOJI[stats.bestSubject?.name] ?? '🏆'}</span>
          <span className="child-stat-value child-stat-value--sm">{stats.bestSubject?.name ?? '—'}</span>
          <span className="child-stat-label">Best subject</span>
        </div>
      </div>

      <div className="child-charts-row">
        <div className="child-chart-card">
          <div className="child-chart-header">
            <h4>📅 This week</h4>
            <span className="child-chart-sub">Missions per day</span>
          </div>
          <div className="child-week-bars">
            {stats.weekDays.map((day) => {
              const pct = maxWeekCount > 0 ? day.count / maxWeekCount : 0
              const h = Math.max(pct * 72, day.count > 0 ? 6 : 0)
              return (
                <div key={day.label} className={`child-week-col${day.isToday ? ' is-today' : ''}`}>
                  {day.count > 0 ? <span className="child-week-count">{day.count}</span> : null}
                  <div className="child-week-bar-track">
                    <div className="child-week-bar" style={{ height: `${h}px` }} />
                  </div>
                  <span className="child-week-label">{day.label}</span>
                </div>
              )
            })}
          </div>
        </div>

        <div className="child-chart-card">
          <div className="child-chart-header">
            <h4>📚 Subjects</h4>
            <span className="child-chart-sub">Average score</span>
          </div>
          <div className="child-subjects-list">
            {stats.subjectList.map((subj) => (
              <div key={subj.name} className="child-subject-row">
                <div className="child-subject-meta">
                  <span>{SUBJECT_EMOJI[subj.name] ?? '📚'} {subj.name}</span>
                  <span className="child-subject-score">{subj.avg}%</span>
                </div>
                <div className="child-subject-track">
                  <div
                    className="child-subject-fill"
                    style={{
                      width: `${subj.avg}%`,
                      background: SUBJECT_COLOR[subj.name] ?? '#fbbf24',
                    }}
                  />
                </div>
                <div className="child-subject-footer">
                  <span className="child-subject-count">{subj.count} lesson{subj.count !== 1 ? 's' : ''}</span>
                  {subj.topTopics.length > 0 ? (
                    <div className="child-subject-topics">
                      {subj.topTopics.map(({ topic, times }) => (
                        <span key={topic} className="child-topic-chip">
                          {topic}
                          {times > 1 ? <span className="child-topic-times">×{times}</span> : null}
                        </span>
                      ))}
                    </div>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
