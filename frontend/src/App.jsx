import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthProvider'
import AppShell from './components/AppShell'
import AuthBootstrap from './components/AuthBootstrap'
import ProtectedRoute from './components/ProtectedRoute'
import ChildHomePage from './pages/ChildHomePage'
import LoginPage from './pages/LoginPage'
import ParentChildrenPage from './pages/ParentChildrenPage'
import ParentAssignmentsPage from './pages/ParentAssignmentsPage'
import ParentHomePage from './pages/ParentHomePage'
import ParentLessonsPage from './pages/ParentLessonsPage'
import ChildResultsPage from './pages/ChildResultsPage'
import ParentAiGenerationPage from './pages/ParentAiGenerationPage'
import ParentReportsPage from './pages/ParentReportsPage'

function LandingPage() {
  const { isAuthenticated, role } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login/parent" replace />
  }

  return <Navigate to={role === 'Child' ? '/child' : '/parent'} replace />
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AuthBootstrap />
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/login/parent" element={<LoginPage variant="parent" />} />
          <Route path="/login/child" element={<LoginPage variant="child" />} />

          <Route element={<ProtectedRoute allowedRoles={['Parent']} />}>
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
              <Route path="ai" element={<ParentAiGenerationPage />} />
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
