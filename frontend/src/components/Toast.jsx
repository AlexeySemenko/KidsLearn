import { useEffect } from 'react'
import { createPortal } from 'react-dom'

export default function Toast({ message, type = 'success', duration = 5000, onDismiss }) {
  useEffect(() => {
    const id = setTimeout(onDismiss, duration)
    return () => clearTimeout(id)
  }, [onDismiss, duration])

  return createPortal(
    <div className={`admin-toast admin-toast--${type}`} role="status">
      {message}
      <button type="button" className="admin-toast-close" onClick={onDismiss} aria-label="Dismiss">✕</button>
    </div>,
    document.body
  )
}
