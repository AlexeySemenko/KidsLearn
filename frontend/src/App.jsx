import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthProvider'
import AppShell from './components/AppShell'
import AuthBootstrap from './components/AuthBootstrap'
import ProtectedRoute from './components/ProtectedRoute'
import ChildHomePage from './pages/ChildHomePage'
import LoginPage from './pages/LoginPage'
import ParentHomePage from './pages/ParentHomePage'
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
                element={
                  <PlaceholderSection
                    title="Children management"
                    copy="This route is ready for the list/create/edit/reset/delete flow from the backlog."
                    epic="Epic 3.2"
                  />
                }
              />
              <Route
                path="lessons"
                element={
                  <PlaceholderSection
                    title="Lessons management"
                    copy="This route is reserved for lesson CRUD, duplication, and the future AI lesson workflow entry points."
                    epic="Epic 3.3"
                  />
                }
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
