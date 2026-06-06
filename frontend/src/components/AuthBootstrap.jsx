import { useAuth } from '../auth/AuthProvider'

export default function AuthBootstrap() {
  const { isBootstrapping } = useAuth()

  if (!isBootstrapping) {
    return null
  }

  return (
    <main className="boot-screen">
      <section className="auth-card boot-card">
        <div className="brand-kicker">KidsLearn</div>
        <h1>Restoring session</h1>
        <p>Checking local session state and refreshing tokens if needed.</p>
      </section>
    </main>
  )
}
