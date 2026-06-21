# Frontend Plan

Stack: React 18 + Vite + backend KidsLearn.Api.

## Current Status (2026-06-21)

All epics delivered. Frontend is feature-complete and in iteration mode.

- Epic 1 (Auth and Session): done — parent and child login, Google SSO for both, session persistence, role-protected routes.
- Epic 2 (App Shell): done — responsive layout, navigation, reusable UI primitives.
- Epic 3 (Parent Dashboard): done — overview stats, per-child drill-down, children/lessons/assignments CRUD, result review. All rows clickable.
- Epic 4 (Child Workspace): done — assignments list, solving flow, instant check, story display, completion, result detail + history.
- Epic 5 (Reports): done — per-child summary, CSV export, date filters, ChildStatsPanel with charts.
- Epic 6 (AI Lessons): done — generation form with story option, AI edit commands, revision history.
- Epic 7 (Social / Friends): done — friends list, invite, accept, friend page, self-assign, notes, SignalR notifications.
- Epic 8 (Linked Parent Accounts): done — manage page, link/unlink by email, shared scope.
- Epic 9 (Lesson Story): done — story field in create/edit and AI generation, shown in view and solve modals.
- Epic 10 (Email Notifications): done at backend layer, triggered by normal API calls.
- Epic 11 (Hardening): done — typed API module, error boundary, CI tests, a11y pass.

## Epic 1. Auth and Session
- Parent login: email/password + Google SSO.
- Child login: redesigned screen with access code + Google SSO.
- Session: persist across reload, auto-refresh token, logout.
- Route protection by role.

Done criteria: users can log in, stay logged in, and are gated by role.

## Epic 2. App Shell
- Responsive layout: header, navigation, content area.
- Reusable primitives: buttons, inputs, selects, cards, modals, alerts, empty states.
- Global loading and error states.

Done criteria: stable shell across all screens, consistent look.

## Epic 3. Parent Dashboard
- Overview: stats cards (children, lessons, completion rate, overdue), recent lessons/assigned/solved with click-to-view.
- Per-child drill-down: ChildStatsPanel + missions waiting/done in 50/50 desktop grid, rows clickable.
- Children: list, create, edit, reset code, delete.
- Lessons: list, create (LessonFormModal with subject/difficulty dropdowns + story), edit, duplicate, delete; AI generate with story option.
- Assignments: list, filter, create, review (LessonViewModal with question/result breakdown).

Done criteria: parent manages all content from one area without direct API access.

## Epic 4. Child Workspace
- Assignments list: pending and completed with status pills.
- Solving: questions with radio/text answers, instant check, story shown above questions, completion summary.
- Results: history list + detail breakdown.

Done criteria: child opens, solves, and reviews a lesson end-to-end.

## Epic 5. Reports
- Per-child summary: completion rate, average score, streak, best subject.
- ChildStatsPanel: weekly bar chart, subject performance table.
- CSV export with date filter.

Done criteria: parent sees child progress and can export.

## Epic 6. AI Lessons
- Generate form: subject, grade, topic, difficulty, language, question count, optional story.
- AI edit commands.
- Revision history viewer.

Done criteria: parent generates and edits AI lesson; UI shows all states clearly.

## Epic 7. Social — Friends
- Friends list with status.
- Invite by email, invite accept page.
- Friend page: stats, missions list, self-assign ("Solve by myself") with toast.
- Friend notes.
- SignalR real-time notifications.

Done criteria: child can connect with a friend and self-assign their lessons.

## Epic 8. Linked Parent Accounts
- Manage page: list linked parents, link by email, unlink.

Done criteria: two parents can share the same workspace.

## Epic 9. Lesson Story
- Story textarea in LessonFormModal.
- "Include story" checkbox in AI generate form.
- Story shown in LessonViewModal and SolveMissionModal.

Done criteria: story is visible everywhere a lesson is viewed or solved.

## Epic 10. Email Notifications
- Parent notified on child assignment completion (all linked parents get the email).
- Child notified on assignment creation.
- No frontend-specific UI — handled transparently by backend.

Done criteria: emails sent on relevant events; app works without SMTP configured.

## Epic 11. Hardening
- Typed API client (`api.ts`) covers all endpoints.
- Global error boundary.
- Frontend tests in CI.
- Accessibility pass on key flows.

Done criteria: stable API integration, regression coverage, accessible core flows.

## Priority for next iteration
1. UX/a11y improvements from usage feedback.
2. New features as product requirements arise.
3. Expand typed API and test coverage for new endpoints.
