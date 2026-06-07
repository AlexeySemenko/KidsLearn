# Frontend Backlog

Основа: React 18 + Vite + KidsLearn.Api.

Related files:
- [BACKEND_BACKLOG.md](BACKEND_BACKLOG.md)
- [MASTER_ROADMAP.md](MASTER_ROADMAP.md)

## Status Summary

- This backlog is the primary delivery track for the UI layer.
- Backend support is already in place and tracked separately in [BACKEND_BACKLOG.md](BACKEND_BACKLOG.md).
- The implementation order below follows the master roadmap.
- Delivered in repository:
	- Epic 1 Authentication and Session: implemented.
	- Epic 2 App Shell and Navigation: implemented.
	- Epic 3 Parent Dashboard core flows: implemented (children, lessons, assignments, assignment review).
	- Epic 4 Child Workspace: implemented (list, solving, completion, result detail route).
	- Epic 6.1 AI lesson generation: implemented (dedicated page + reusable popup from lessons page).
- Remaining high-priority slices:
	- Epic 5 Reports and Analytics.
	- Epic 6.2-6.3 AI lesson editing + richer workflow feedback.
	- Epic 7 hardening (typed client, global error boundary, tests, accessibility).

## Epic 1. Authentication and Session

### 1.1 Parent login screen
- Build a parent login form with email and password.
- Submit credentials to `/api/v1/auth/login`.
- Store access token and refresh token securely on success.

Acceptance criteria:
- User can log in as parent.
- Invalid credentials show an inline error.
- Successful login redirects to the parent area.

### 1.2 Child login screen
- Build a child login form with `childId` and `access code`.
- Submit credentials to `/api/v1/auth/child-login`.
- Store tokens on success.

Acceptance criteria:
- Child can log in with valid credentials.
- Invalid credentials show an inline error.
- Successful login redirects to the child area.

### 1.3 Session persistence and refresh
- Keep session after page reload.
- Refresh access token with `/api/v1/auth/refresh`.
- Revoke tokens on logout via `/api/v1/auth/revoke`.

Acceptance criteria:
- Reloading the app keeps the user signed in when refresh token is valid.
- Expired access token is refreshed automatically.
- Logout clears client session state.

### 1.4 Route protection
- Protect parent routes from child access.
- Protect child routes from parent access.
- Redirect unauthenticated users to the correct login screen.

Acceptance criteria:
- Unauthorized users cannot access protected pages.
- Role mismatches are handled cleanly.

## Epic 2. App Shell and Navigation

### 2.1 Global layout
- Create a shared shell with header, navigation, and content area.
- Add responsive behavior for desktop and mobile.

Acceptance criteria:
- App has a consistent layout across major screens.
- Navigation remains usable on mobile.

### 2.2 Reusable UI primitives
- Implement buttons, inputs, selects, cards, tables, modals, alerts, and empty states.
- Keep styling consistent across the app.

Acceptance criteria:
- Core screens reuse the same UI primitives.
- UI states look consistent and predictable.

### 2.3 Global error and loading states
- Add loading indicators for async actions.
- Add error banners and inline validation states.

Acceptance criteria:
- Users see progress during network calls.
- Errors are understandable and non-blocking where possible.

## Epic 3. Parent Dashboard

### 3.1 Overview dashboard
- Add a dashboard for children, lessons, assignments, and recent progress.
- Show quick links to major actions.

Acceptance criteria:
- Parent can reach the most important actions from one screen.
- Dashboard loads data successfully from backend APIs.

### 3.2 Children management
- Show list of children from `/api/v1/children`.
- Add child creation form.
- Add edit child form.
- Add reset access code action.
- Add delete child action.

Acceptance criteria:
- Parent can create, update, reset access code, and delete a child.
- Validation errors are shown clearly.

### 3.3 Lessons management
- Show list of lessons from `/api/v1/lessons`.
- Add lesson creation form.
- Add lesson edit form.
- Add duplicate lesson action.
- Add delete lesson action.

Acceptance criteria:
- Parent can manage lessons without leaving the app.
- Empty lesson states and validation errors are handled.

### 3.4 Assignments management
- Show parent assignments list from `/api/v1/assignments`.
- Add assignment creation flow.
- Add assignment detail and solving preview.

Acceptance criteria:
- Parent can create and review assignments.
- Assignment state updates are reflected in the UI.

## Epic 4. Child Workspace

### 4.1 Child assignments list
- Show the child's assigned work from `/api/v1/child/assignments`.
- Add filters or grouping if needed.

Acceptance criteria:
- Child can see only their own assignments.
- Assignment list loads with clear empty state.

### 4.2 Assignment solving flow
- Build the solving screen for answering questions.
- Support instant check from `/answers` endpoints.
- Support final completion from `/complete` endpoints.

Acceptance criteria:
- Child can solve an assignment end-to-end.
- Instant check results are shown clearly.
- Completion result is displayed after submit.

### 4.3 Child results view
- Show result detail from `/api/v1/child/results/{resultId}`.
- Present correctness breakdown and score.

Acceptance criteria:
- Child can review completed work.
- Score and breakdown are easy to read.

## Epic 5. Reports and Analytics

### 5.1 Parent child report summary
- Show summary from `/api/v1/reports/children/{childId}`.
- Display completion rate, average score, solved count, and streak.

Acceptance criteria:
- Parent can understand progress at a glance.
- Summary matches backend values.

### 5.2 CSV export
- Add export action for `/api/v1/reports/children/{childId}/export?format=csv`.
- Trigger file download in browser.

Acceptance criteria:
- Parent can export a valid CSV file.
- Export errors are shown when backend rejects the request.

### 5.3 Visual progress UI
- Add simple charts or progress cards for recent activity.
- Keep visuals lightweight and responsive.

Acceptance criteria:
- Progress is readable on desktop and mobile.
- Charts or cards do not block the main workflows.

## Epic 6. AI Lesson Workflows

### 6.1 AI lesson generation
- Build generation form for subject, grade, topic, difficulty, question count, and language.
- Call `/api/v1/ai/lessons/generate`.
- Show generated lesson detail and provider meta.

Acceptance criteria:
- Parent can generate a lesson draft and persist it.
- 422 schema errors are displayed clearly.

### 6.2 AI lesson editing
- Build an edit command UI for generated lessons.
- Support command execution and revision display.

Acceptance criteria:
- Parent can modify AI lesson content.
- Revision info is visible after edit.

### 6.3 AI workflow feedback
- Show retry, fallback, and validation states in the UI.

Acceptance criteria:
- User understands when generation succeeds with fallback or fails validation.

## Epic 7. API Integration and Quality

### 7.1 Typed API client
- Create a frontend API layer for all backend endpoints.
- Centralize request/response mapping and auth headers.

Acceptance criteria:
- Screens use a shared client instead of ad hoc fetch calls.
- Token handling is centralized.

### 7.2 Cross-cutting error handling
- Add a global error boundary and API error mapper.
- Handle 400, 401, 404, 409, 422, and 500 responses consistently.

Acceptance criteria:
- Common backend errors render consistent user messages.
- App does not crash on expected API failures.

### 7.3 Testing and accessibility
- Add UI tests for login, assignment solving, and report flows.
- Check keyboard navigation, focus handling, and basic ARIA coverage.

Acceptance criteria:
- Main flows have regression coverage.
- Core interactions are accessible.

## Suggested implementation order
1. Authentication and Session
2. App Shell and Navigation
3. Parent Dashboard
4. Child Workspace
5. Reports and Analytics
6. AI Lesson Workflows
7. API Integration and Quality
