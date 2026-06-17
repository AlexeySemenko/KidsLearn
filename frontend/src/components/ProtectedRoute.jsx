import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthProvider'

export default function ProtectedRoute({ allowedRoles }) {
  const { isAuthenticated, isBootstrapping, role } = useAuth()
  const location = useLocation()

  if (isBootstrapping) {
    return null
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  if (allowedRoles.length > 0 && !allowedRoles.includes(role)) {
    const redirectTo = role === 'Child' ? '/child' : '/parent'
    return <Navigate to={redirectTo} replace />
  }

  return <Outlet />
}
