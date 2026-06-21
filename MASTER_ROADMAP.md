# Master Roadmap

This roadmap connects the backend and frontend backlogs into one working plan.

Related files:
- [FRONTEND_BACKLOG.md](FRONTEND_BACKLOG.md)
- [BACKEND_BACKLOG.md](BACKEND_BACKLOG.md)

## Current State (2026-06-21)

- Backend: complete and stable across all implemented domains.
- Frontend: fully implemented across all planned feature areas.
- Both tracks are in maintenance/iteration mode — new features are delivered as product requirements arise.

## Phase 1. Foundation
- [x] Confirm frontend app shell, routing, and reusable UI primitives.
- [x] Implement auth/session flow for parent and child users.
- [x] Keep backend contracts aligned while frontend client is introduced.

## Phase 2. Parent Experience
- [x] Build parent dashboard (overview + per-child stats).
- [x] Add children management screens.
- [x] Add lessons management screens (create, edit, duplicate, delete).
- [x] Add assignment creation and review flows.
- [x] Add result detail with per-question breakdown.

## Phase 3. Child Experience
- [x] Build child assignments list.
- [x] Build assignment solving flow with instant check.
- [x] Build child results view and result history.
- [x] Child login redesign (dedicated screen with access code + Google SSO).

## Phase 4. Reports and AI
- [x] Add report summary and CSV export UI.
- [x] Add AI lesson generation UI (subject, grade, topic, difficulty, language, optional story).
- [x] Add AI lesson editing and revision review UI.

## Phase 5. Lessons — Advanced
- [x] Unified create/edit lesson modal (LessonFormModal) with subject and difficulty dropdowns.
- [x] Optional lesson story field — shown in view modal and solve modal.
- [x] AI generation "include story" checkbox — GPT writes a story and bases questions on it.

## Phase 6. Social and Collaboration
- [x] Child friends system: invite by email, accept invite, view friend page.
- [x] Friends page: view friend missions, self-assign a friend's lesson ("Solve by myself" button).
- [x] Friend notes: children can leave notes for each other.
- [x] SignalR real-time notifications for friend events.
- [x] Linked parent accounts: two parents share children, lessons, and assignments.

## Phase 7. Notifications
- [x] Email to parent (all linked parents) when child completes an assignment — includes score + recent stats.
- [x] Email to child when a lesson is assigned.
- [x] SMTP-based email service with graceful degradation when not configured.

## Phase 8. Dashboard UX Polish
- [x] Parent dashboard overview: all lesson/assignment rows clickable, open LessonViewModal.
- [x] Child-specific view: lessons waiting + lessons done use 50/50 desktop layout.
- [x] Lesson view shows story if present; result view shows per-question breakdown.
- [x] Toast notifications for self-assign action (rendered via portal above modals).

## Next Focus
1. Iterate on product feedback — new features as required.
2. Accessibility and mobile polish.
3. Backend maintenance and contract stability.

## Execution Rule
1. Use [FRONTEND_BACKLOG.md](FRONTEND_BACKLOG.md) for feature delivery work.
2. Use [BACKEND_BACKLOG.md](BACKEND_BACKLOG.md) only for support, stability, and contract upkeep.
3. Keep the two files in sync whenever API contracts or user flows change.
