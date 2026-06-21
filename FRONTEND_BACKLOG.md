# Frontend Backlog

Stack: React 18 + Vite + KidsLearn.Api.

Related files:
- [BACKEND_BACKLOG.md](BACKEND_BACKLOG.md)
- [MASTER_ROADMAP.md](MASTER_ROADMAP.md)

## Status Summary (2026-06-21)

All planned epics are delivered. The frontend is in feature-complete / iteration mode.

**Delivered:**
- Epic 1 Authentication and Session: parent login (email/password + Google SSO), child login (access code + Google SSO), session persistence, refresh, role-protected routes.
- Epic 2 App Shell and Navigation: responsive shell, header, navigation, reusable UI primitives.
- Epic 3 Parent Dashboard: overview stats, per-child drill-down, children CRUD, lessons CRUD (create/edit/duplicate/delete with unified LessonFormModal), assignments management, assignment review with result breakdown. All lesson/assignment rows clickable — open LessonViewModal.
- Epic 4 Child Workspace: assignments list, solving flow with instant check and story display, completion, result detail, result history.
- Epic 5 Reports and Analytics: per-child summary, CSV export, date filters, progress charts (ChildStatsPanel with weekly bar chart and subject breakdown).
- Epic 6 AI Lesson Workflows: generation form with subject/grade/topic/difficulty/language/story options, AI edit commands, revision history.
- Epic 7 Social (Friends): child friends page — invite by email, accept invite, view friend missions, self-assign ("Solve by myself" button with toast), friend notes, SignalR real-time notifications.
- Epic 8 Linked Parent Accounts: manage screen to link/unlink another parent by email; shared scope reflected across dashboard.
- Epic 9 Lesson Story: optional story field in create/edit modal; AI generation "include story" checkbox; story shown in LessonViewModal and SolveMissionModal.
- Epic 10 Email Notifications: shown at backend layer; frontend triggers via normal assignment/completion API calls.
- Epic 11 Hardening: typed API module (`api.ts`), global error boundary, frontend regression tests in CI, accessibility pass on key flows.

## Epic 1. Authentication and Session

### 1.1 Parent login screen ✅
- Parent login with email/password and Google SSO.
- Token storage and session persistence.

### 1.2 Child login screen ✅
- Dedicated child login screen with access code and Google SSO.
- Redirects to child workspace on success.

### 1.3 Session persistence and refresh ✅
- Auto-refresh on token expiry.
- Logout clears state.

### 1.4 Route protection ✅
- Parent and child routes protected by role.

## Epic 2. App Shell and Navigation ✅

- Responsive shell with header and navigation.
- Reusable UI: buttons, inputs, selects, cards, modals, alerts, empty states.
- Global loading and error states.

## Epic 3. Parent Dashboard ✅

### 3.1 Overview dashboard ✅
- Stats cards, recent lessons, recently assigned, recently solved.
- Per-child drill-down with stats panel and mission lists.
- All rows clickable — open LessonViewModal (lesson review or result breakdown).

### 3.2 Children management ✅
- Create, edit, reset access code, delete.

### 3.3 Lessons management ✅
- List, create, edit, duplicate, delete.
- LessonFormModal: subject dropdown (with "Other"), difficulty dropdown, optional story textarea.
- AI generation modal: subject/grade/topic/difficulty/language/count + "include story" checkbox.

### 3.4 Assignments management ✅
- Create assignments, filter by status and child.
- Review button: opens LessonViewModal with questions or result breakdown.

## Epic 4. Child Workspace ✅

### 4.1 Child assignments list ✅
### 4.2 Assignment solving flow ✅
- Instant check with per-question feedback.
- Story displayed above questions if present.
- Completion result with score.

### 4.3 Child results view ✅
- Result detail with breakdown.
- Results history list.

## Epic 5. Reports and Analytics ✅

- Per-child summary card.
- CSV export.
- Date filters.
- ChildStatsPanel: weekly bar chart, subject performance table, streak, average score.

## Epic 6. AI Lesson Workflows ✅

- Generation form with all parameters + optional story.
- AI edit commands.
- Revision history viewer.

## Epic 7. Social — Friends ✅

- Friends list with status pills.
- Send invite by email, accept invite page.
- Friend page: view friend stats, missions, self-assign button with toast.
- Friend notes.
- SignalR real-time notifications.

## Epic 8. Linked Parent Accounts ✅

- Manage page: view linked parents, link by email, unlink.
- Dashboard reflects shared scope automatically.

## Epic 9. Lesson Story ✅

- Story textarea in LessonFormModal (create/edit).
- "Include lesson story" checkbox in AI generation.
- Story block shown in LessonViewModal and SolveMissionModal.

## Epic 10. Email Notifications ✅

- Triggered automatically via backend on assignment creation and completion.
- No frontend-specific work required beyond the existing API calls.

## Epic 11. Hardening ✅

- Typed API module (`src/lib/api.ts`) covers all endpoints.
- Global error boundary.
- Frontend regression tests in CI.
- Accessibility pass on key flows.

## Ongoing / Next

1. Iterate on UX feedback — new screens and flows as product requirements arise.
2. Expand typed API coverage as new endpoints are added.
3. Additional integration/regression tests for social and notification flows.
4. Mobile polish on new screens (friends page, linked parents, dashboard drill-down).
