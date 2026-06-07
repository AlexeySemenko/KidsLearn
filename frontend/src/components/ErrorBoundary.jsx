import { Component } from 'react'

export default class ErrorBoundary extends Component {
  constructor(props) {
    super(props)
    this.state = { hasError: false, errorMessage: '' }
  }

  static getDerivedStateFromError(error) {
    return {
      hasError: true,
      errorMessage: error instanceof Error ? error.message : 'Unexpected application error.',
    }
  }

  componentDidCatch(error, errorInfo) {
    // Keep diagnostics in console for local debugging and telemetry pickup.
    console.error('Unhandled UI error:', error, errorInfo)
  }

  handleReload = () => {
    window.location.reload()
  }

  handleGoHome = () => {
    window.location.href = '/'
  }

  render() {
    if (this.state.hasError) {
      return (
        <section className="auth-root">
          <article className="auth-card error-boundary-card">
            <div className="brand-kicker">Stability guard</div>
            <h1>Something went wrong in the interface.</h1>
            <p>
              The app hit an unexpected state. You can safely reload the page,
              or return to the home route to continue working.
            </p>

            <div className="info-block error-boundary-details">
              <strong>Error details</strong>
              <span>{this.state.errorMessage}</span>
            </div>

            <div className="button-row">
              <button type="button" className="button" onClick={this.handleReload}>
                Reload app
              </button>
              <button type="button" className="button-secondary" onClick={this.handleGoHome}>
                Go to home
              </button>
            </div>
          </article>
        </section>
      )
    }

    return this.props.children
  }
}