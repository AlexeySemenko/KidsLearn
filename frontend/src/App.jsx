import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthProvider'
import AppShell from './components/AppShell'
import AuthBootstrap from './components/AuthBootstrap'
import ProtectedRoute from './components/ProtectedRoute'
import AdminUsersPage from './pages/AdminUsersPage'
import ChildHomePage from './pages/ChildHomePage'
import LoginPage from './pages/LoginPage'
import ParentGoogleCallbackPage from './pages/ParentGoogleCallbackPage'
import ChildGoogleCallbackPage from './pages/ChildGoogleCallbackPage'
import ParentChildrenPage from './pages/ParentChildrenPage'
import ParentAssignmentsPage from './pages/ParentAssignmentsPage'
import ParentHomePage from './pages/ParentHomePage'
import ParentLessonsPage from './pages/ParentLessonsPage'
import ChildResultsPage from './pages/ChildResultsPage'
import ParentReportsPage from './pages/ParentReportsPage'
import ParentManagePage from './pages/ParentManagePage'

function LandingPage() {
  const { isAuthenticated, role } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (role === 'Child') return <Navigate to="/child" replace />
  if (role === 'Admin') return <Navigate to="/admin/users" replace />
  return <Navigate to="/parent" replace />
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AuthBootstrap />
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/login/parent" element={<Navigate to="/login" replace />} />
          <Route path="/login/child" element={<Navigate to="/login" replace />} />
          <Route path="/login/parent/google/callback" element={<ParentGoogleCallbackPage />} />
          <Route path="/login/child/google/callback" element={<ChildGoogleCallbackPage />} />

          <Route element={<ProtectedRoute allowedRoles={['Parent', 'Admin']} />}>
            <Route path="/parent" element={<AppShell />}>
              <Route index element={<ParentHomePage />} />
              <Route
                path="children"
                element={<ParentChildrenPage />}
              />
              <Route
                path="lessons"
                element={<ParentLessonsPage />}
              />
              <Route
                path="assignments"
                element={<ParentAssignmentsPage />}
              />
              <Route
                path="reports"
                element={<ParentReportsPage />}
              />
              <Route path="manage" element={<ParentManagePage />} />
            </Route>
          </Route>

          <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
            <Route path="/admin" element={<AppShell />}>
              <Route index element={<Navigate to="/admin/users" replace />} />
              <Route path="users" element={<AdminUsersPage />} />
            </Route>
          </Route>

          <Route element={<ProtectedRoute allowedRoles={['Child']} />}>
            <Route path="/child" element={<AppShell />}>
              <Route index element={<ChildHomePage />} />
              <Route path="results" element={<ChildResultsPage />} />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
