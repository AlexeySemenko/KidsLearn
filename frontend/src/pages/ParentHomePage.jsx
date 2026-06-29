import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'
import { getAssignments, getChildren, getLesson, getLessons, getParentChildResults, getParentAssignmentForSolving, getParentResultDetail } from '../lib/api'
import ChildStatsPanel, { SUBJECT_EMOJI, scoreEmoji, scoreVariant } from '../components/ChildStatsPanel'
import LessonViewModal from '../components/LessonViewModal'

const PILLAR_COLORS = ['#60a5fa', '#34d399', '#a78bfa', '#f87171', '#fbbf24', '#fb923c']
const PILLAR_EMOJIS = ['🦊', '🐻', '🐼', '🦁', '🐸', '🦋']

const STATUS_PILL = {
  Assigned:   { cls: 'status-new', label: '✨ Assigned' },
  InProgress: { cls: '',           label: '⚡ In progress' },
  Completed:  { cls: 'status-success', label: '✅ Done' },
}

function formatRelative(iso) {
  if (!iso) return ''
  const diff = Date.now() - new Date(iso).getTime()
  const mins  = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days  = Math.floor(diff / 86400000)
  if (mins < 2)   return 'just now'
  if (mins < 60)  return `${mins}m ago`
  if (hours < 24) return `${hours}h ago`
  if (days < 7)   return `${days}d ago`
  return new Date(iso).toLocaleDateString()
}

export default function ParentHomePage() {
  const { session } = useAuth()
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [children, setChildren] = useState([])
  const [lessons, setLessons] = useState([])
  const [assignments, setAssignments] = useState([])

  const [selectedChildId, setSelectedChildId] = useState(null)
  const [childResults, setChildResults] = useState([])
  const [childResultsLoading, setChildResultsLoading] = useState(false)

  const [reviewAssignment, setReviewAssignment] = useState(null)
  const [reviewResult, setReviewResult] = useState(null)
  const [reviewLesson, setReviewLesson] = useState(null)
  const [isLoadingReview, setIsLoadingReview] = useState(false)

  useEffect(() => {
    let isMounted = true

    async function loadDashboard() {
      if (!session?.accessToken) {
        if (isMounted) setIsLoading(false)
        return
      }

      try {
        setError('')
        const [childrenResponse, lessonsResponse, assignmentsResponse] = await Promise.all([
          getChildren(session.accessToken),
          getLessons(session.accessToken),
          getAssignments(session.accessToken),
        ])

        if (!isMounted) return

        setChildren(childrenResponse)
        setLessons(lessonsResponse.items ?? [])
        setAssignments(assignmentsResponse)
      } catch (requestError) {
        if (isMounted) setError(requestError.message)
      } finally {
        if (isMounted) setIsLoading(false)
      }
    }

    loadDashboard()
    return () => { isMounted = false }
  }, [session?.accessToken])

  async function handlePillarClick(childId) {
    if (selectedChildId === childId) {
      setSelectedChildId(null)
      setChildResults([])
      return
    }

    setSelectedChildId(childId)
    setChildResultsLoading(true)
    try {
      const results = await getParentChildResults(session.accessToken, childId)
      setChildResults(results)
    } catch (err) {
      setError(err.message)
      setChildResults([])
    } finally {
      setChildResultsLoading(false)
    }
  }

  async function handleLessonReview(lesson) {
    if (!session?.accessToken) return
    setIsLoadingReview(true)
    try {
      const detail = await getLesson(session.accessToken, lesson.id)
      setReviewLesson(detail)
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoadingReview(false)
    }
  }

  async function handleReview(assignment) {
    if (!session?.accessToken) return
    setIsLoadingReview(true)
    try {
      if (assignment.resultId) {
        const result = await getParentResultDetail(session.accessToken, assignment.resultId)
        setReviewResult(result)
      } else {
        const detail = await getParentAssignmentForSolving(session.accessToken, assignment.id)
        setReviewAssignment(detail)
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setIsLoadingReview(false)
    }
  }

  const selectedChild = children.find((c) => c.id === selectedChildId)
  const selectedChildAssignments = selectedChildId
    ? assignments.filter((a) => a.childId === selectedChildId)
    : []
  const selectedChildPendingCount = selectedChildAssignments.filter((a) => a.status !== 'Completed').length
  const childPendingAssignments = selectedChildAssignments.filter((a) => a.status !== 'Completed')
  const childDoneAssignments = [...selectedChildAssignments]
    .filter((a) => a.status === 'Completed')
    .sort((a, b) => new Date(b.assignedAt) - new Date(a.assignedAt))
  const completedAssignments = assignments.filter((a) => a.status === 'Completed').length
  const overdueAssignments = assignments.filter(
    (a) => a.dueDate && a.status !== 'Completed' && new Date(a.dueDate).getTime() < Date.now()
  ).length
  const completionRate = assignments.length === 0
    ? 0
    : Math.round((completedAssignments / assignments.length) * 100)

  const recentLessons = useMemo(() =>
    [...lessons].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt)).slice(0, 3),
    [lessons])

  const recentAssigned = useMemo(() =>
    [...assignments]
      .filter((a) => a.status !== 'Completed')
      .sort((a, b) => new Date(b.assignedAt) - new Date(a.assignedAt))
      .slice(0, 3),
    [assignments])

  const recentSolved = useMemo(() =>
    [...assignments]
      .filter((a) => a.status === 'Completed')
      .sort((a, b) => new Date(b.assignedAt) - new Date(a.assignedAt))
      .slice(0, 3),
    [assignments])

  return (
    <section className="parent-dash-page">
      {error ? <div className="alert" style={{ marginBottom: '1rem' }}>{error}</div> : null}

      {/* ── Children pillars ── */}
      {!isLoading && children.length > 0 ? (
        <div className="parent-dash-pillars-wrap">
          <div className="parent-dash-pillars-label">My children</div>
          <div className="parent-dash-pillars">
            {children.map((child, i) => (
              <button
                key={child.id}
                type="button"
                className={`parent-child-pillar${selectedChildId === child.id ? ' is-active' : ''}`}
                style={{ '--pillar-color': PILLAR_COLORS[i % PILLAR_COLORS.length] }}
                onClick={() => handlePillarClick(child.id)}
              >
                <span className="pillar-avatar">{PILLAR_EMOJIS[i % PILLAR_EMOJIS.length]}</span>
                <span className="pillar-name">{child.name}</span>
                <span className="pillar-grade">Grade {child.grade}</span>
                {selectedChildId === child.id ? (
                  <span className="pillar-active-hint">tap to close</span>
                ) : null}
              </button>
            ))}
          </div>
        </div>
      ) : null}

      {/* ── Selected child stats ── */}
      {selectedChildId ? (
        <div className="parent-child-stats-view">
          <div className="parent-child-stats-header">
            <h3>
              {PILLAR_EMOJIS[children.findIndex((c) => c.id === selectedChildId) % PILLAR_EMOJIS.length]}{' '}
              {selectedChild?.name ?? 'Child'}'s progress
            </h3>
            <span className="badge">Grade {selectedChild?.grade}</span>
          </div>
          <ChildStatsPanel results={childResults} isLoading={childResultsLoading} pendingCount={selectedChildPendingCount} />

          <div className="parent-dash-recent-grid parent-dash-recent-grid--2col" style={{ marginTop: '1.5rem' }}>
            <div className="parent-dash-recent-card">
              <div className="parent-dash-recent-header">
                <h4>⏳ Lessons waiting</h4>
              </div>
              {childPendingAssignments.length === 0 ? (
                <p className="parent-dash-recent-empty">No pending lessons.</p>
              ) : childPendingAssignments.map((a) => (
                <button
                  key={a.id}
                  type="button"
                  className="parent-dash-recent-item parent-dash-recent-item--clickable"
                  disabled={isLoadingReview}
                  onClick={() => handleReview(a)}
                >
                  <div className="parent-dash-recent-main">
                    <span className="parent-dash-recent-title">
                      {SUBJECT_EMOJI[a.lessonSubject] || '📚'} {a.lessonTitle}
                    </span>
                    <span className="parent-dash-recent-meta">{a.lessonSubject} · {formatRelative(a.assignedAt)}</span>
                  </div>
                  <span className={`assignment-status-pill ${STATUS_PILL[a.status]?.cls ?? ''}`} style={{ fontSize: '0.7rem', padding: '0.15rem 0.5rem' }}>
                    {STATUS_PILL[a.status]?.label ?? a.status}
                  </span>
                </button>
              ))}
            </div>

            <div className="parent-dash-recent-card">
              <div className="parent-dash-recent-header">
                <h4>✅ Lessons done</h4>
              </div>
              {childDoneAssignments.length === 0 ? (
                <p className="parent-dash-recent-empty">No completed lessons yet.</p>
              ) : childDoneAssignments.map((a) => (
                <button
                  key={a.id}
                  type="button"
                  className="parent-dash-recent-item parent-dash-recent-item--clickable"
                  disabled={isLoadingReview}
                  onClick={() => handleReview(a)}
                >
                  <div className="parent-dash-recent-main">
                    <span className="parent-dash-recent-title">
                      {SUBJECT_EMOJI[a.lessonSubject] || '📚'} {a.lessonTitle}
                    </span>
                    <span className="parent-dash-recent-meta">{a.lessonSubject} · {formatRelative(a.assignedAt)}</span>
                  </div>
                  {a.score != null ? (
                    <span className={`assignment-status-pill ${scoreVariant(a.score)}`} style={{ fontSize: '0.7rem', padding: '0.15rem 0.5rem' }}>
                      {scoreEmoji(a.score)} {a.score}%
                    </span>
                  ) : null}
                </button>
              ))}
            </div>
          </div>
        </div>
      ) : (
        <>
          {isLoading ? (
            <p className="children-empty" style={{ padding: '2rem 0' }}>Loading dashboard...</p>
          ) : (
            <>
              {/* ── Overview stats ── */}
              <div className="parent-overview-grid">
                <Link to="/parent/children" className="parent-overview-card parent-overview-card--link" data-accent="blue">
                  <span className="parent-ov-icon">👦</span>
                  <span className="parent-ov-value">{children.length}</span>
                  <span className="parent-ov-label">Children</span>
                </Link>
                <Link to="/parent/lessons" className="parent-overview-card parent-overview-card--link" data-accent="green">
                  <span className="parent-ov-icon">📚</span>
                  <span className="parent-ov-value">{lessons.length}</span>
                  <span className="parent-ov-label">Lessons</span>
                </Link>
                <div className="parent-overview-card" data-accent="yellow">
                  <span className="parent-ov-icon">✅</span>
                  <span className="parent-ov-value">{completionRate}%</span>
                  <span className="parent-ov-label">Completion rate</span>
                </div>
                <div className="parent-overview-card" data-accent="red">
                  <span className="parent-ov-icon">⏰</span>
                  <span className="parent-ov-value">{overdueAssignments}</span>
                  <span className="parent-ov-label">Overdue</span>
                </div>
              </div>

              {/* ── Recent activity ── */}
              <div className="parent-dash-recent-grid">
                {/* Last created lessons */}
                <div className="parent-dash-recent-card">
                  <div className="parent-dash-recent-header">
                    <h4>📚 Recent lessons</h4>
                    <Link to="/parent/lessons" className="parent-dash-recent-link">View all →</Link>
                  </div>
                  {recentLessons.length === 0 ? (
                    <p className="parent-dash-recent-empty">No lessons yet.</p>
                  ) : recentLessons.map((lesson) => (
                    <button
                      key={lesson.id}
                      type="button"
                      className="parent-dash-recent-item parent-dash-recent-item--clickable"
                      disabled={isLoadingReview}
                      onClick={() => handleLessonReview(lesson)}
                    >
                      <div className="parent-dash-recent-main">
                        <span className="parent-dash-recent-title">
                          {SUBJECT_EMOJI[lesson.subject] || '📚'} {lesson.title}
                        </span>
                        <span className="parent-dash-recent-meta">Grade {lesson.grade} · {lesson.subject}</span>
                      </div>
                      <span className="parent-dash-recent-time">{formatRelative(lesson.createdAt)}</span>
                    </button>
                  ))}
                </div>

                {/* Last assigned */}
                <div className="parent-dash-recent-card">
                  <div className="parent-dash-recent-header">
                    <h4>📋 Recently assigned</h4>
                    <Link to="/parent/assignments" className="parent-dash-recent-link">View all →</Link>
                  </div>
                  {recentAssigned.length === 0 ? (
                    <p className="parent-dash-recent-empty">No active assignments.</p>
                  ) : recentAssigned.map((a) => (
                    <button
                      key={a.id}
                      type="button"
                      className="parent-dash-recent-item parent-dash-recent-item--clickable"
                      disabled={isLoadingReview}
                      onClick={() => handleReview(a)}
                    >
                      <div className="parent-dash-recent-main">
                        <span className="parent-dash-recent-title">{a.childName}</span>
                        <span className="parent-dash-recent-meta">
                          {SUBJECT_EMOJI[a.lessonSubject] || '📚'} {a.lessonTitle}
                        </span>
                      </div>
                      <span className={`assignment-status-pill ${STATUS_PILL[a.status]?.cls ?? ''}`} style={{ fontSize: '0.7rem', padding: '0.15rem 0.5rem' }}>
                        {STATUS_PILL[a.status]?.label ?? a.status}
                      </span>
                    </button>
                  ))}
                </div>

                {/* Last solved */}
                <div className="parent-dash-recent-card">
                  <div className="parent-dash-recent-header">
                    <h4>🏆 Recently solved</h4>
                    <Link to="/parent/assignments" className="parent-dash-recent-link">View all →</Link>
                  </div>
                  {recentSolved.length === 0 ? (
                    <p className="parent-dash-recent-empty">No completed assignments yet.</p>
                  ) : recentSolved.map((a) => (
                    <button
                      key={a.id}
                      type="button"
                      className="parent-dash-recent-item parent-dash-recent-item--clickable"
                      disabled={isLoadingReview}
                      onClick={() => handleReview(a)}
                    >
                      <div className="parent-dash-recent-main">
                        <span className="parent-dash-recent-title">{a.childName}</span>
                        <span className="parent-dash-recent-meta">
                          {SUBJECT_EMOJI[a.lessonSubject] || '📚'} {a.lessonTitle}
                        </span>
                      </div>
                      {a.score != null ? (
                        <span className={`assignment-status-pill ${scoreVariant(a.score)}`} style={{ fontSize: '0.7rem', padding: '0.15rem 0.5rem' }}>
                          {scoreEmoji(a.score)} {a.score}%
                        </span>
                      ) : null}
                    </button>
                  ))}
                </div>
              </div>
            </>
          )}
        </>
      )}
      {reviewLesson ? (
        <LessonViewModal
          title={reviewLesson.title}
          subtitle={`${reviewLesson.questions.length} question${reviewLesson.questions.length !== 1 ? 's' : ''} · Grade ${reviewLesson.grade}`}
          story={reviewLesson.story}
          storyImageUrl={reviewLesson.storyImageUrl}
          questions={reviewLesson.questions}
          onClose={() => setReviewLesson(null)}
          renderQuestion={(question, index) => (
            <article key={question.id} className="assignment-row question-card">
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
                      {answer.isCorrect ? <span className="answer-correct-badge">✓ Correct</span> : null}
                    </div>
                  ))}
                </div>
              </div>
            </article>
          )}
          footer={(
            <div className="button-row modal-actions">
              <button type="button" className="button-secondary" onClick={() => setReviewLesson(null)}>Close</button>
            </div>
          )}
        />
      ) : null}

      {reviewAssignment ? (
        <LessonViewModal
          title={`Review: ${reviewAssignment.lessonTitle}`}
          subtitle={`${reviewAssignment.questions.length} question${reviewAssignment.questions.length !== 1 ? 's' : ''}`}
          story={reviewAssignment.lessonStory}
          storyImageUrl={reviewAssignment.lessonStoryImageUrl}
          questions={reviewAssignment.questions}
          onClose={() => setReviewAssignment(null)}
          renderQuestion={(question, index) => (
            <article key={question.questionId} className="assignment-row question-card">
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
                      key={answer.answerId}
                      className={`question-option${answer.isCorrect ? ' correct-answer' : ''}`}
                    >
                      <span>{answer.answerText}</span>
                      {answer.isCorrect ? <span className="answer-correct-badge">✓ Correct</span> : null}
                    </div>
                  ))}
                </div>
              </div>
            </article>
          )}
          footer={(
            <div className="button-row modal-actions">
              <button type="button" className="button-secondary" onClick={() => setReviewAssignment(null)}>Close</button>
            </div>
          )}
        />
      ) : null}

      {reviewResult ? (
        <LessonViewModal
          title={reviewResult.lessonTitle}
          subtitle={`${scoreEmoji(reviewResult.score)} ${reviewResult.score}% · ${reviewResult.correctAnswers}/${reviewResult.totalQuestions} correct`}
          questions={reviewResult.breakdown}
          onClose={() => setReviewResult(null)}
          renderQuestion={(item, index) => (
            <article key={item.questionId} className="assignment-row question-card">
              <div className="assignment-copy">
                <div className="assignment-topline">
                  <div className="child-name">Question {index + 1}</div>
                  <span className={`assignment-status-pill ${item.correct ? 'status-success' : 'status-danger'}`}>
                    {item.correct ? '✅ Correct' : '❌ Incorrect'}
                  </span>
                </div>
                <div>{item.questionText}</div>
                <div className="question-options">
                  {item.answers.map((answer) => {
                    const wasSelected = answer.answerId === item.selectedAnswerOptionId
                    const isCorrect = answer.isCorrect
                    let cls = 'question-option'
                    if (isCorrect) cls += ' correct-answer'
                    if (wasSelected && !isCorrect) cls += ' wrong-selected'
                    return (
                      <div key={answer.answerId} className={cls}>
                        <span>{answer.answerText}</span>
                        {isCorrect ? <span className="answer-correct-badge">✓ Correct</span> : null}
                        {wasSelected && !isCorrect ? <span className="answer-wrong-badge">✗ Child's answer</span> : null}
                        {wasSelected && isCorrect ? <span className="answer-correct-badge">✓ Child's answer</span> : null}
                      </div>
                    )
                  })}
                </div>
              </div>
            </article>
          )}
          footer={(
            <div className="result-summary-footer">
              <div className={`mission-complete ${reviewResult.score >= 90 ? 'grade-perfect' : reviewResult.score >= 70 ? 'grade-great' : reviewResult.score >= 50 ? 'grade-good' : 'grade-ok'}`}
                style={{ paddingTop: '1rem', paddingBottom: '0.5rem' }}>
                <div className="mission-complete-emoji" aria-hidden="true">
                  {reviewResult.score >= 90 ? '🌟' : reviewResult.score >= 70 ? '🎊' : reviewResult.score >= 50 ? '👍' : '😊'}
                </div>
                <div className="mission-complete-title">
                  {reviewResult.score >= 90 ? 'Perfect!' : reviewResult.score >= 70 ? 'Great job!' : reviewResult.score >= 50 ? 'Good job!' : 'Keep trying!'}
                </div>
                <div className="mission-complete-score">
                  {reviewResult.score}% &nbsp;·&nbsp; {reviewResult.correctAnswers}/{reviewResult.totalQuestions} correct
                </div>
              </div>
              <div className="button-row modal-actions">
                <button type="button" className="button-secondary" onClick={() => setReviewResult(null)}>Close</button>
              </div>
            </div>
          )}
        />
      ) : null}
    </section>
  )
}
