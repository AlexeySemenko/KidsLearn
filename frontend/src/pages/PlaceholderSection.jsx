export default function PlaceholderSection({ title, copy, epic }) {
  return (
    <section className="panel-grid">
      <article className="hero-card">
        <div className="brand-kicker">Planned section</div>
        <h2>{title}</h2>
        <p>{copy}</p>
      </article>

      <article className="panel-card">
        <h3>Backlog reference</h3>
        <p>This route is intentionally scaffolded so the next implementation slice has a stable target.</p>
        <div className="metric">{epic}</div>
      </article>
    </section>
  )
}
