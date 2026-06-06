# Backend Backlog

This backlog is aligned with [FRONTEND_BACKLOG.md](FRONTEND_BACKLOG.md) and [MASTER_ROADMAP.md](MASTER_ROADMAP.md), and reflects the backend work that supports the frontend roadmap.

Related files:
- [FRONTEND_BACKLOG.md](FRONTEND_BACKLOG.md)
- [MASTER_ROADMAP.md](MASTER_ROADMAP.md)

## Status Summary

The core backend implementation is complete and currently in a stable state.
- CQRS/MediatR is in place across lessons, assignments, child flows, reports, AI, and auth.
- Validation, request logging, health checks, error handling, and security middleware are implemented.
- Backend tests are green.

Because the main backend epics are already delivered, the remaining backlog is focused on maintenance, hardening, and any backend follow-up needed by frontend work.

## Epic 1. API Contract Stability

### 1.1 Keep response contracts aligned with frontend needs
- Verify request/response shapes remain stable for auth, children, lessons, assignments, reports, and AI.
- Add contract tests for any newly introduced endpoints.

Acceptance criteria:
- Frontend can rely on stable API shapes.
- Breaking contract changes are detected early.

### 1.2 Document endpoint behavior
- Keep endpoint descriptions and examples in sync with backend behavior.
- Update README/API docs when payloads or statuses change.

Acceptance criteria:
- Frontend developers can discover endpoint behavior without reading implementation code.

## Epic 2. Validation and Error Semantics

### 2.1 Preserve validation behavior
- Keep `ValidationBehavior` consistent for commands and queries.
- Ensure validators continue returning the intended HTTP-friendly errors.

Acceptance criteria:
- Validation failures map to predictable 400/404/409/422 responses.

### 2.2 Standardize error payloads
- Keep error responses consistent across handlers and controllers.
- Avoid introducing endpoint-specific error shapes unless needed.

Acceptance criteria:
- Frontend can display a uniform error experience.

## Epic 3. Performance and Reliability

### 3.1 Review hot paths
- Revisit assignment solving, reports, and AI generation for performance if usage grows.
- Keep EF queries predictable and maintainable.

Acceptance criteria:
- Common flows remain responsive under typical usage.

### 3.2 Keep health and startup checks reliable
- Preserve readiness/liveness behavior.
- Keep relational DB startup checks guarded for non-relational test environments.

Acceptance criteria:
- App startup and health endpoints remain reliable across environments.

## Epic 4. Test Coverage Maintenance

### 4.1 Keep handler coverage current
- Add tests when new commands, queries, or validators are introduced.
- Keep unit tests close to the application-layer behavior.

Acceptance criteria:
- New backend features ship with matching tests.

### 4.2 Protect integration flows
- Keep the existing integration tests for parent and child solving flows up to date.
- Add regression tests for any backend change that affects the frontend.

Acceptance criteria:
- Main user journeys remain covered end-to-end.

## Epic 5. Frontend Support Work

### 5.1 Support typed client generation or manual client upkeep
- Keep endpoint names, payloads, and status codes frontend-friendly.
- Update backend contracts if frontend needs a cleaner integration surface.

Acceptance criteria:
- Frontend API client remains straightforward to implement and maintain.

### 5.2 Support AI workflow UX
- Preserve stable error/status semantics for AI generation and AI editing.
- Keep 422 validation and fallback behavior explicit.

Acceptance criteria:
- Frontend can distinguish validation failure, fallback success, and true server errors.

## Relationship to Frontend Backlog

Backend work is the dependency layer for the frontend epics in [FRONTEND_BACKLOG.md](FRONTEND_BACKLOG.md):
- Frontend auth depends on `/api/v1/auth/*`.
- Parent dashboard depends on `/api/v1/children`, `/api/v1/lessons`, `/api/v1/assignments`, and result/report endpoints.
- Child workspace depends on `/api/v1/child/*` assignment and result endpoints.
- Reports and analytics depend on `/api/v1/reports/children/{childId}` and CSV export.
- AI lesson workflows depend on `/api/v1/ai/lessons/generate` and `/api/v1/ai/lessons/{lessonId}/edit`.

## Recommended Ongoing Order
1. Keep API contracts stable.
2. Maintain validation and error semantics.
3. Keep tests current when frontend-driven changes land.
4. Only add new backend functionality when frontend requires it.
