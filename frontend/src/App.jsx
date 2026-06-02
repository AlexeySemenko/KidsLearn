import { useState, useEffect } from 'react'

const API_BASE = import.meta.env.VITE_API_URL ?? ''

export default function App() {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [input, setInput] = useState('')
  const [sending, setSending] = useState(false)

  const fetchGreeting = async () => {
    try {
      setLoading(true)
      setError(null)
      const res = await fetch(`${API_BASE}/api/hello`)
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      const json = await res.json()
      setData(json)
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }

  const sendGreeting = async () => {
    if (!input.trim()) return
    try {
      setSending(true)
      await fetch(`${API_BASE}/api/hello`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: input.trim() }),
      })
      setInput('')
      await fetchGreeting()
    } catch (e) {
      setError(e.message)
    } finally {
      setSending(false)
    }
  }

  useEffect(() => { fetchGreeting() }, [])

  return (
    <>
      <style>{styles}</style>
      <div className="page">
        <header className="header">
          <div className="header-inner">
            <span className="logo">HW/</span>
            <nav className="nav">
              <a href="https://github.com" className="nav-link">GitHub</a>
              <span className="nav-dot">·</span>
              <a href="#stack" className="nav-link">Stack</a>
            </nav>
          </div>
        </header>

        <main className="main">
          <section className="hero">
            <div className="eyebrow">Full-stack · React + .NET + PostgreSQL</div>
            <h1 className="title">
              {loading ? (
                <span className="skeleton-text">Loading…</span>
              ) : error ? (
                <span className="error-text">Connection error</span>
              ) : (
                <span className="message-text">{data?.message}</span>
              )}
            </h1>
            <p className="subtitle">
              Message served from{' '}
              <strong>{data?.source ?? '…'}</strong>{' '}
              at{' '}
              <code>{data ? new Date(data.timestamp).toLocaleTimeString() : '—'}</code>
            </p>

            <div className="card-row">
              <div className="card">
                <div className="card-label">Frontend</div>
                <div className="card-value">React 18 + Vite</div>
              </div>
              <div className="card">
                <div className="card-label">Backend</div>
                <div className="card-value">.NET 8 Minimal API</div>
              </div>
              <div className="card">
                <div className="card-label">Database</div>
                <div className="card-value">PostgreSQL</div>
              </div>
              <div className="card">
                <div className="card-label">Deploy</div>
                <div className="card-value">Railway</div>
              </div>
            </div>
          </section>

          <section className="form-section" id="stack">
            <h2 className="form-title">Set a new greeting</h2>
            <div className="form-row">
              <input
                className="input"
                placeholder="Type your greeting…"
                value={input}
                onChange={e => setInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && sendGreeting()}
                maxLength={120}
              />
              <button
                className="btn"
                onClick={sendGreeting}
                disabled={sending || !input.trim()}
              >
                {sending ? 'Saving…' : 'Save →'}
              </button>
            </div>
            {error && <div className="error-banner">⚠ {error}</div>}
          </section>
        </main>

        <footer className="footer">
          <span>Built with React · .NET 8 · PostgreSQL · Railway</span>
          <button className="refresh-btn" onClick={fetchGreeting}>↻ Refresh</button>
        </footer>
      </div>
    </>
  )
}

const styles = `
  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

  :root {
    --bg: #0a0a0f;
    --surface: #13131a;
    --border: #1f1f2e;
    --accent: #e8ff47;
    --accent2: #47b8ff;
    --text: #f0f0f5;
    --muted: #6b6b80;
    --font-display: 'Syne', sans-serif;
    --font-mono: 'Space Mono', monospace;
    --r: 6px;
  }

  body {
    background: var(--bg);
    color: var(--text);
    font-family: var(--font-display);
    min-height: 100dvh;
    -webkit-font-smoothing: antialiased;
  }

  .page {
    min-height: 100dvh;
    display: flex;
    flex-direction: column;
  }

  /* ── Header ─────────────────────────────────────────────── */
  .header {
    border-bottom: 1px solid var(--border);
    padding: 0 clamp(1rem, 5vw, 3rem);
  }
  .header-inner {
    max-width: 960px;
    margin: 0 auto;
    height: 56px;
    display: flex;
    align-items: center;
    justify-content: space-between;
  }
  .logo {
    font-family: var(--font-mono);
    font-size: 1.1rem;
    font-weight: 700;
    color: var(--accent);
    letter-spacing: -0.03em;
  }
  .nav { display: flex; align-items: center; gap: .5rem; }
  .nav-link {
    font-family: var(--font-mono);
    font-size: .75rem;
    color: var(--muted);
    text-decoration: none;
    transition: color .2s;
  }
  .nav-link:hover { color: var(--text); }
  .nav-dot { color: var(--border); font-size: .75rem; }

  /* ── Main ───────────────────────────────────────────────── */
  .main {
    flex: 1;
    max-width: 960px;
    margin: 0 auto;
    width: 100%;
    padding: clamp(2.5rem, 8vw, 6rem) clamp(1rem, 5vw, 3rem);
  }

  /* ── Hero ───────────────────────────────────────────────── */
  .hero { margin-bottom: clamp(2rem, 6vw, 4rem); }

  .eyebrow {
    font-family: var(--font-mono);
    font-size: clamp(.65rem, 1.5vw, .78rem);
    color: var(--muted);
    letter-spacing: .12em;
    text-transform: uppercase;
    margin-bottom: 1.25rem;
  }

  .title {
    font-size: clamp(2.2rem, 7vw, 5rem);
    font-weight: 800;
    line-height: 1.05;
    letter-spacing: -0.03em;
    margin-bottom: 1rem;
    min-height: 1.1em;
  }

  .skeleton-text { color: var(--muted); animation: pulse 1.4s ease-in-out infinite; }
  .error-text { color: #ff6b6b; }
  .message-text {
    background: linear-gradient(135deg, var(--accent) 0%, var(--accent2) 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }

  @keyframes pulse { 0%,100% { opacity:.4 } 50% { opacity: 1 } }

  .subtitle {
    font-size: clamp(.85rem, 2vw, 1rem);
    color: var(--muted);
    margin-bottom: 2.5rem;
    line-height: 1.6;
  }
  .subtitle strong { color: var(--text); }
  .subtitle code {
    font-family: var(--font-mono);
    font-size: .85em;
    background: var(--surface);
    border: 1px solid var(--border);
    padding: .1em .4em;
    border-radius: 3px;
  }

  /* ── Cards ──────────────────────────────────────────────── */
  .card-row {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
    gap: 1px;
    background: var(--border);
    border: 1px solid var(--border);
    border-radius: var(--r);
    overflow: hidden;
  }
  .card {
    background: var(--surface);
    padding: 1.25rem 1.5rem;
    transition: background .2s;
  }
  .card:hover { background: #1a1a24; }
  .card-label {
    font-family: var(--font-mono);
    font-size: .65rem;
    letter-spacing: .1em;
    text-transform: uppercase;
    color: var(--muted);
    margin-bottom: .4rem;
  }
  .card-value {
    font-size: .95rem;
    font-weight: 700;
    color: var(--text);
  }

  /* ── Form section ───────────────────────────────────────── */
  .form-section {
    border-top: 1px solid var(--border);
    padding-top: clamp(2rem, 5vw, 3rem);
  }
  .form-title {
    font-size: clamp(1rem, 3vw, 1.4rem);
    font-weight: 700;
    margin-bottom: 1.25rem;
    color: var(--text);
  }
  .form-row {
    display: flex;
    gap: .75rem;
    flex-wrap: wrap;
  }
  .input {
    flex: 1 1 240px;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: var(--r);
    color: var(--text);
    font-family: var(--font-display);
    font-size: 1rem;
    padding: .75rem 1rem;
    outline: none;
    transition: border-color .2s, box-shadow .2s;
  }
  .input:focus {
    border-color: var(--accent);
    box-shadow: 0 0 0 3px rgba(232,255,71,.12);
  }
  .input::placeholder { color: var(--muted); }
  .btn {
    background: var(--accent);
    color: #0a0a0f;
    border: none;
    border-radius: var(--r);
    font-family: var(--font-mono);
    font-size: .85rem;
    font-weight: 700;
    padding: .75rem 1.5rem;
    cursor: pointer;
    white-space: nowrap;
    transition: opacity .2s, transform .1s;
  }
  .btn:hover:not(:disabled) { opacity: .88; transform: translateY(-1px); }
  .btn:active:not(:disabled) { transform: translateY(0); }
  .btn:disabled { opacity: .4; cursor: not-allowed; }

  .error-banner {
    margin-top: 1rem;
    background: rgba(255,107,107,.1);
    border: 1px solid rgba(255,107,107,.3);
    border-radius: var(--r);
    color: #ff9a9a;
    font-family: var(--font-mono);
    font-size: .8rem;
    padding: .6rem 1rem;
  }

  /* ── Footer ─────────────────────────────────────────────── */
  .footer {
    border-top: 1px solid var(--border);
    padding: 1rem clamp(1rem, 5vw, 3rem);
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: .5rem;
    font-family: var(--font-mono);
    font-size: .7rem;
    color: var(--muted);
  }
  .refresh-btn {
    background: none;
    border: 1px solid var(--border);
    border-radius: var(--r);
    color: var(--muted);
    cursor: pointer;
    font-family: var(--font-mono);
    font-size: .7rem;
    padding: .3rem .7rem;
    transition: color .2s, border-color .2s;
  }
  .refresh-btn:hover { color: var(--text); border-color: var(--muted); }

  /* ── Responsive ─────────────────────────────────────────── */
  @media (max-width: 480px) {
    .card-row { grid-template-columns: 1fr 1fr; }
    .form-row { flex-direction: column; }
    .btn { width: 100%; }
  }
`
