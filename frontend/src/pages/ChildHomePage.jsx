export default function ChildHomePage() {
  return (
    <section className="panel-grid">
      <article className="hero-card">
        <div className="brand-kicker">Phase 1</div>
        <h2>Child access path is now protected and persistent.</h2>
        <p>
          Child login now lands inside a dedicated route tree instead of a placeholder page.
          The next slice can replace this content with assigned work, solving, and result detail.
        </p>
        <div className="badge-row">
          <span className="badge">Child login connected</span>
          <span className="badge">Session restore ready</span>
          <span className="badge">Protected child routes</span>
        </div>
      </article>

      <article className="panel-card">
        <h3>Assignments list</h3>
        <p>The first real child data screen will pull assigned work into this area.</p>
        <div className="metric">Epic 4.1</div>
      </article>

      <article className="panel-card">
        <h3>Solving flow</h3>
        <p>Instant check and completion UI will build on top of this child route tree.</p>
        <div className="metric">Epic 4.2</div>
      </article>

      <article className="panel-card">
        <h3>Result detail</h3>
        <p>Score breakdown and result review will follow after solving screens exist.</p>
        <div className="metric">Epic 4.3</div>
      </article>
    </section>
  )
}
