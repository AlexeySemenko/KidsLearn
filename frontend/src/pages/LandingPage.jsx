import { Link, Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

const SUBJECTS = [
  { emoji: '📐', label: 'Math',      example: 'Fractions & Decimals' },
  { emoji: '📖', label: 'English',   example: 'Story Comprehension' },
  { emoji: '🌍', label: 'Science',   example: 'Solar System' },
  { emoji: '✡️', label: 'Hebrew',    example: 'Nikud & Reading' },
  { emoji: '🎨', label: 'Art',       example: 'Famous Artists' },
  { emoji: '🏛️', label: 'History',  example: 'Ancient Egypt' },
  { emoji: '🔬', label: 'Biology',   example: 'Cell Structure' },
  { emoji: '💻', label: 'Coding',    example: 'Logic & Patterns' },
  { emoji: '🌐', label: 'Geography', example: 'World Capitals' },
  { emoji: '🎵', label: 'Music',     example: 'Rhythm & Notes' },
]

function LpNav() {
  return (
    <nav className="lp-nav" aria-label="Main navigation">
      <div className="lp-nav-inner">
        <Link to="/" className="lp-nav-brand" aria-label="KidsLearnAI home">
          <span className="kl-logo-rocket" aria-hidden="true">🚀</span>
          <span className="kl-wm-kids">Kids</span><span className="kl-wm-learn">Learn</span><span className="kl-wm-ai">AI</span>
        </Link>
        <Link to="/login" className="button lp-nav-cta">Get Started</Link>
      </div>
    </nav>
  )
}

function LpHero() {
  return (
    <section className="lp-hero" aria-label="Hero">
      <div className="lp-hero-content">
        <img
          src="/hero.jpg"
          alt="Magical family learning together with KidsLearnAI"
          className="lp-hero-img"
        />
        <p className="lp-hero-kicker">AI-Powered Learning Platform</p>
        <h1 className="lp-hero-headline">
          Learning that <span className="lp-headline-hl">adapts</span>
          <br />to your child
        </h1>
        <p className="lp-hero-sub">
          Any grade. Any subject. Any language.<br />
          AI-generated lessons tailored to your child in seconds.
        </p>
        <div className="lp-hero-actions">
          <Link to="/login" className="button lp-hero-cta">Start Learning Free →</Link>
        </div>
        <p className="lp-hero-hint">No credit card required · 3 AI lessons free every day</p>
      </div>
    </section>
  )
}

function LpHowItWorks() {
  const steps = [
    {
      number: '01',
      icon: '🎯',
      title: 'Pick grade & subject',
      desc: 'Parents choose any grade from 1 to 12 and any subject — math, science, history, and more.',
    },
    {
      number: '02',
      icon: '🤖',
      title: 'AI writes the lesson',
      desc: 'Our AI generates a rich story and comprehension questions tailored to the selected grade level.',
    },
    {
      number: '03',
      icon: '🏆',
      title: 'Child completes the mission',
      desc: 'Children read the story and answer questions interactively, earning stars along the way.',
    },
  ]

  return (
    <section id="how-it-works" className="lp-how">
      <div className="lp-section lp-section-center">
        <p className="lp-section-kicker">How it works</p>
        <h2 className="lp-section-heading">Three steps to a smarter lesson</h2>
        <p className="lp-section-sub">From idea to interactive lesson in under a minute.</p>
        <div className="lp-steps">
          {steps.map((step) => (
            <div key={step.number} className="lp-step-card">
              <div className="lp-step-header">
                <span className="lp-step-number">{step.number}</span>
                <span className="lp-step-icon" aria-hidden="true">{step.icon}</span>
              </div>
              <h3 className="lp-step-title">{step.title}</h3>
              <p className="lp-step-desc">{step.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}

function LpSubjects() {
  return (
    <section className="lp-subjects-section">
      <div className="lp-section lp-section-center">
        <p className="lp-section-kicker">Subjects</p>
        <h2 className="lp-section-heading">Every subject. Every grade.</h2>
        <p className="lp-section-sub">
          From Grade 1 through Grade 12 — generate lessons across every subject your child studies.
        </p>
        <div className="lp-subjects-grid">
          {SUBJECTS.map((subject) => (
            <div key={subject.label} className="lp-subject-tile">
              <span className="lp-subject-emoji" aria-hidden="true">{subject.emoji}</span>
              <p className="lp-subject-label">{subject.label}</p>
              <span className="lp-subject-example">{subject.example}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}

function LpSampleLesson() {
  return (
    <section className="lp-preview-section">
      <div className="lp-section lp-preview">
        <div className="lp-preview-copy">
          <p className="lp-section-kicker">Real example</p>
          <h2 className="lp-section-heading">See what a lesson looks like</h2>
          <p className="lp-section-sub" style={{ margin: '0 0 1.5rem', maxWidth: 'none' }}>
            Every AI-generated lesson includes an age-appropriate story and multiple-choice questions
            that build reading comprehension and subject knowledge simultaneously.
          </p>
          <ul className="lp-preview-bullets">
            <li>✦ Stories written at the right reading level</li>
            <li>✦ Questions test understanding, not just recall</li>
            <li>✦ Instant feedback for every answer</li>
          </ul>
        </div>
        <div className="lp-preview-card" aria-label="Sample lesson preview">
          <span className="lp-preview-grade">🌍 Grade 3 · Science</span>
          <h4 className="lp-preview-title">Captain Nova on Mars</h4>
          <div className="lp-preview-story">
            <p>
              Captain Nova landed her rocket on the red planet Mars. The sky was pink and dusty,
              and the ground was covered in rust-colored rocks. She checked her oxygen tank —
              four hours remaining. Her mission: collect a rock sample and return safely to Earth.
            </p>
            <p>
              Mars is the fourth planet from the Sun. It is much smaller than Earth and has two
              tiny moons named Phobos and Deimos. The air on Mars is mostly carbon dioxide,
              which humans cannot breathe.
            </p>
          </div>
          <div className="lp-preview-question">
            <p className="lp-preview-q-text">Why does Captain Nova need an oxygen tank on Mars?</p>
            <ul className="lp-preview-options">
              <li className="lp-preview-option">It is too cold on Mars</li>
              <li className="lp-preview-option lp-preview-option--correct">
                The air on Mars is mostly carbon dioxide
                <span className="lp-preview-correct-badge">✓ Correct</span>
              </li>
              <li className="lp-preview-option">Mars has no gravity</li>
              <li className="lp-preview-option">Rockets need oxygen to fly</li>
            </ul>
          </div>
        </div>
      </div>
    </section>
  )
}

function LpPricing() {
  const features = [
    '3 AI lesson generations per day',
    'Unlimited lesson assignments',
    'Progress tracking & reports',
    'Multi-child support',
    'Parent dashboard',
  ]

  return (
    <section className="lp-pricing">
      <div className="lp-section lp-section-center">
        <p className="lp-section-kicker">Pricing</p>
        <h2 className="lp-section-heading">Simple, honest pricing</h2>
        <p className="lp-section-sub">Everything you need to support your child's learning.</p>
        <div className="lp-pricing-card-wrap">
          <div className="lp-pricing-card">
            <div className="lp-pricing-price">FREE</div>
            <p className="lp-pricing-price-sub">Forever, for every family</p>
            <ul className="lp-pricing-features">
              {features.map((f) => (
                <li key={f} className="lp-pricing-feature">
                  <span className="lp-pricing-check" aria-hidden="true">✅</span>
                  {f}
                </li>
              ))}
            </ul>
            <span className="lp-pricing-badge">Free forever for basic use</span>
            <Link to="/login" className="button" style={{ width: '100%', textAlign: 'center' }}>
              Get Started — It&apos;s Free
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}

function LpFinalCta() {
  return (
    <section className="lp-final-cta">
      <div className="lp-final-cta-inner">
        <div className="lp-final-cta-rocket" aria-hidden="true">🚀</div>
        <h2 className="lp-final-cta-heading">Ready to make learning exciting?</h2>
        <p className="lp-final-cta-sub">
          Join families who are already using KidsLearnAI to spark curiosity in their children.
        </p>
        <div className="lp-final-cta-actions">
          <Link to="/login" className="button lp-hero-cta">Start for Free Today →</Link>
          <a href="#how-it-works" className="button-secondary">See how it works</a>
        </div>
      </div>
    </section>
  )
}

function LpFooter() {
  return (
    <footer className="lp-footer">
      <p className="lp-footer-brand">🚀 KidsLearnAI</p>
      <p className="lp-footer-tagline">Learn smart. Grow fast. Have fun.</p>
      <p>© {new Date().getFullYear()} KidsLearnAI. All rights reserved.</p>
    </footer>
  )
}

export default function LandingPage() {
  const { isAuthenticated, isBootstrapping, role } = useAuth()

  if (isBootstrapping) return null

  if (isAuthenticated) {
    if (role === 'Child') return <Navigate to="/child" replace />
    if (role === 'Admin') return <Navigate to="/admin/users" replace />
    return <Navigate to="/parent" replace />
  }

  return (
    <div className="lp-root">
      <LpNav />
      <LpHero />
      <LpHowItWorks />
      <LpSubjects />
      <LpSampleLesson />
      <LpPricing />
      <LpFinalCta />
      <LpFooter />
    </div>
  )
}
