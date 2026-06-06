# Master Roadmap

This roadmap connects the backend and frontend backlogs into one working plan.

Related files:
- [FRONTEND_BACKLOG.md](FRONTEND_BACKLOG.md)
- [BACKEND_BACKLOG.md](BACKEND_BACKLOG.md)

## Current State

- Backend implementation is complete and stable.
- Frontend implementation has a defined backlog but has not started in the repository yet.
- The next execution path should follow the frontend-first delivery order, while keeping the backend backlog as a maintenance/support track.

## Phase 1. Foundation
- [ ] Confirm frontend app shell, routing, and reusable UI primitives.
- [ ] Implement auth/session flow for parent and child users.
- [ ] Keep backend contracts unchanged while frontend client is introduced.

Dependency notes:
- Uses auth endpoints from the backend backlog.
- Relies on stable response shapes for login and refresh flows.

## Phase 2. Parent Experience
- [ ] Build parent dashboard.
- [ ] Add children management screens.
- [ ] Add lessons management screens.
- [ ] Add assignment creation and review flows.

Dependency notes:
- Relies on backend children, lessons, assignments, and result endpoints.
- Any backend contract changes must be reflected in the frontend backlog.

## Phase 3. Child Experience
- [ ] Build child assignments list.
- [ ] Build assignment solving flow.
- [ ] Build child results view.

Dependency notes:
- Relies on backend child assignment and child result endpoints.
- UI should preserve instant-check and completion semantics.

## Phase 4. Reports and AI
- [ ] Add report summary and CSV export UI.
- [ ] Add AI lesson generation UI.
- [ ] Add AI lesson editing and revision review UI.

Dependency notes:
- Relies on backend reports and AI endpoints.
- 422 and fallback semantics must remain stable.

## Phase 5. Hardening
- [ ] Add typed frontend API client.
- [ ] Add global error handling.
- [ ] Add UI tests and accessibility coverage.
- [ ] Review backend backlog items only when frontend needs them.

Dependency notes:
- Frontend quality work depends on stable backend error semantics.
- Backend maintenance items should stay aligned with the frontend backlog.

## Execution Rule
1. Use [FRONTEND_BACKLOG.md](FRONTEND_BACKLOG.md) for feature delivery work.
2. Use [BACKEND_BACKLOG.md](BACKEND_BACKLOG.md) only for support, stability, and contract upkeep.
3. Keep the two files in sync whenever API contracts or user flows change.
