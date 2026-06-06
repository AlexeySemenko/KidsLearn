export default function ParentHomePage() {
  return (
    <section className="panel-grid">
      <article className="hero-card">
        <div className="brand-kicker">Phase 1</div>
        <h2>Parent frontend foundation is live.</h2>
        <p>
          The app now supports parent authentication, session restore, protected routes,
          and a shared shell. The next implementation slice can fill these panels with real
          children, lessons, assignments, and report data.
        </p>
        <div className="badge-row">
          <span className="badge">Auth flow connected</span>
          <span className="badge">Session refresh ready</span>
          <span className="badge">Protected parent routes</span>
        </div>
      </article>

      <article className="side-card panel-card">
        <h3>Children workspace</h3>
        <p>Reserved for list, create, edit, reset access code, and delete child actions.</p>
        <div className="metric">Epic 3.2</div>
        <div className="metric-copy">Children management is the next concrete parent slice.</div>
      </article>

      <article className="panel-card">
        <h3>Lessons and AI</h3>
        <p>Lesson CRUD and AI lesson generation/editing will layer onto the same shell.</p>
        <div className="metric">Epic 3.3 / 6</div>
      </article>

      <article className="panel-card">
        <h3>Assignments</h3>
        <p>Assignment creation and result review will fit into this protected parent area.</p>
        <div className="metric">Epic 3.4</div>
      </article>
    </section>
  )
}
