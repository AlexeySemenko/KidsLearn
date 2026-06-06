export default function App() {
  return (
    <main className="shell">
      <style>{styles}</style>
      <section className="panel">
        <div className="eyebrow">KidsLearn</div>
        <h1>Frontend implementation starts here.</h1>
        <p>
          The old hello-world starter has been removed. Use the backlog files
          in the project root as the source of truth for the next frontend
          implementation steps.
        </p>
      </section>
    </main>
  )
}

const styles = `
  *, *::before, *::after { box-sizing: border-box; }

  :root {
    --bg: #0b1020;
    --panel: #111831;
    --border: rgba(255, 255, 255, 0.12);
    --text: #eef2ff;
    --muted: rgba(238, 242, 255, 0.72);
    --accent: #8bd3ff;
  }

  html, body, #root { min-height: 100%; }

  body {
    margin: 0;
    background:
      radial-gradient(circle at top, rgba(139, 211, 255, 0.14), transparent 30%),
      linear-gradient(180deg, #060816 0%, var(--bg) 100%);
    color: var(--text);
    font-family: Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  }

  .shell {
    min-height: 100vh;
    display: grid;
    place-items: center;
    padding: 2rem;
  }

  .panel {
    width: min(720px, 100%);
    padding: clamp(2rem, 6vw, 4rem);
    border: 1px solid var(--border);
    border-radius: 24px;
    background: linear-gradient(180deg, rgba(17, 24, 49, 0.92), rgba(17, 24, 49, 0.76));
    backdrop-filter: blur(18px);
    box-shadow: 0 24px 80px rgba(0, 0, 0, 0.35);
  }

  .eyebrow {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    margin-bottom: 1rem;
    color: var(--accent);
    text-transform: uppercase;
    letter-spacing: 0.18em;
    font-size: 0.75rem;
    font-weight: 700;
  }

  h1 {
    margin: 0;
    font-size: clamp(2.2rem, 6vw, 4.5rem);
    line-height: 1.02;
    letter-spacing: -0.05em;
  }

  p {
    margin: 1rem 0 0;
    max-width: 56ch;
    color: var(--muted);
    font-size: 1rem;
    line-height: 1.7;
  }
`
