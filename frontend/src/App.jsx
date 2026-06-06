import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthProvider'
import AppShell from './components/AppShell'
import AuthBootstrap from './components/AuthBootstrap'
import ProtectedRoute from './components/ProtectedRoute'
import ChildHomePage from './pages/ChildHomePage'
import LoginPage from './pages/LoginPage'
import ParentChildrenPage from './pages/ParentChildrenPage'
import ParentHomePage from './pages/ParentHomePage'
import ParentLessonsPage from './pages/ParentLessonsPage'
import PlaceholderSection from './pages/PlaceholderSection'

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
                path="reports"
                element={
                  <PlaceholderSection
                    title="Reports and analytics"
                    copy="The protected parent shell now has a stable route for child report summaries and CSV export."
                    epic="Epic 5"
                  />
                }
              />
            </Route>
          </Route>

          <Route element={<ProtectedRoute allowedRoles={['Child']} />}>
            <Route path="/child" element={<AppShell />}>
              <Route index element={<ChildHomePage />} />
              <Route
                path="results"
                element={
                  <PlaceholderSection
                    title="Child results"
                    copy="This route is reserved for completed assignment detail and score breakdown screens."
                    epic="Epic 4.3"
                  />
                }
              />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
