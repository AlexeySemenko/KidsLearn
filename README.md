# KidsLearn — React + .NET 8 + PostgreSQL

Responsive full-stack learning platform for kids and parents. Runs locally with Docker Compose; deploys to a single Fly.io app via GitHub Actions.

## Current Status (2026-06-21)

- Backend: complete and stable. All core domain flows implemented and tested.
- Frontend: fully implemented across all major feature areas.

**Implemented:**
- Auth: parent login (email/password + Google SSO), child login (access code + Google SSO), session persistence, role-protected routes
- Parent dashboard: overview stats, per-child progress drill-down with clickable lesson/result rows
- Children management: create, edit, reset access code, delete
- Lessons management: create, edit, duplicate, delete; subject/difficulty dropdowns; optional story field; AI generation with optional story; AI edit commands + revision history
- Assignments: create, review, result breakdown with per-question feedback
- Child workspace: assignments list, solving flow with instant check, result detail, result history
- Social: child friends system — invite by email, accept invite, view friend missions, self-assign a friend's lesson, friend notes, SignalR notifications
- Linked parent accounts: two parent accounts share children/lessons/assignments
- Reports: per-child progress summary, CSV export, date filters
- Email notifications: parent notified (all linked parents) on child assignment completion with score + recent stats; child notified on assignment creation
- Dashboard UX: all lesson/assignment rows in parent dashboard are clickable and open LessonViewModal

---

## Stack

| Layer      | Tech                        |
|------------|-----------------------------|
| Frontend   | React 18 + Vite             |
| Backend    | .NET 8 Minimal API          |
| Database   | PostgreSQL 16               |
| Realtime   | SignalR (friend notifications) |
| Deploy     | Fly.io (single app)         |
| CI/CD      | GitHub Actions              |

---

## Local Development

### Option A — Docker Compose (recommended)

```bash
docker compose  up -d --build backend
```

- App (frontend + backend) → http://localhost:8080  
- Postgres → localhost:5432

Note: In Docker Compose, frontend static files are served by the backend container.

### Option B — Run each service manually

**PostgreSQL**
```bash
docker run -e POSTGRES_DB=kidslearn -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16-alpine
```

**Backend**
```bash
cd backend/KidsLearn.Api
dotnet run
# Listens on http://localhost:8080
```

**Frontend**
```bash
cd frontend
npm install
npm run dev
# Vite dev server on http://localhost:5173
# /api/* proxied to localhost:8080
```

### OpenAI config for local compose

`docker-compose.yml` reads:

- `OPENAI_API_KEY`
- `OPENAI_MODEL` (default `gpt-4o-mini`)

Set these in a root `.env` file:

```env
OPENAI_API_KEY=<your-key>
OPENAI_MODEL=gpt-4o-mini
```

### Google SSO config

`docker-compose.yml` reads the following for parent and child SSO:

- `GOOGLE_AUTH_CLIENT_ID`
- `GOOGLE_AUTH_CLIENT_SECRET`
- `GOOGLE_AUTH_REDIRECT_URI` (parent callback, default `http://localhost:8080/api/v1/auth/google/callback`)
- `GOOGLE_AUTH_FRONTEND_CALLBACK_URL` (parent frontend, default `http://localhost:8080/login/parent/google/callback`)
- `GOOGLE_AUTH_CHILD_REDIRECT_URI` (child callback, default `http://localhost:8080/api/v1/auth/child/google/callback`)
- `GOOGLE_AUTH_CHILD_FRONTEND_CALLBACK_URL` (child frontend, default `http://localhost:8080/login/child/google/callback`)

Add these to root `.env`:

```env
GOOGLE_AUTH_CLIENT_ID=<google-client-id>
GOOGLE_AUTH_CLIENT_SECRET=<google-client-secret>
GOOGLE_AUTH_REDIRECT_URI=http://localhost:8080/api/v1/auth/google/callback
GOOGLE_AUTH_FRONTEND_CALLBACK_URL=http://localhost:8080/login/parent/google/callback
GOOGLE_AUTH_CHILD_REDIRECT_URI=http://localhost:8080/api/v1/auth/child/google/callback
GOOGLE_AUTH_CHILD_FRONTEND_CALLBACK_URL=http://localhost:8080/login/child/google/callback
```

In Google Cloud Console, add both redirect URIs to your OAuth client:
- `http://localhost:8080/api/v1/auth/google/callback`
- `http://localhost:8080/api/v1/auth/child/google/callback`

### Email notifications config

```env
Email__SmtpHost=<smtp-host>
Email__SmtpPort=587
Email__SmtpUsername=<username>
Email__SmtpPassword=<password>
Email__FromAddress=noreply@yourdomain.com
Email__FromName=KidsLearnAI
```

Email is optional — the app runs without it and logs what would have been sent.

---

## Deploy to Fly.io

### 1 — Create one Fly app

1. Sign up at https://fly.io
2. Install `flyctl` and authenticate:
  ```bash
  fly auth login
  ```
3. Create one Fly app (name must be globally unique).

### 2 — Set backend secrets on Fly

```bash
fly secrets set DATABASE_URL=<postgres-connection-string> AllowedOrigins__0=https://<your-app>.fly.dev -a <your-app>
```

For AI generation/editing in production:

```bash
fly secrets set OpenAI__ApiKey=<openai-key> OpenAI__Model=gpt-4o-mini -a <your-app>
```

For Google SSO in production:

```bash
fly secrets set \
  GoogleAuth__ClientId=<google-client-id> \
  GoogleAuth__ClientSecret=<google-client-secret> \
  GoogleAuth__RedirectUri=https://<your-app>.fly.dev/api/v1/auth/google/callback \
  GoogleAuth__ParentFrontendCallbackUrl=https://<your-app>.fly.dev/login/parent/google/callback \
  GoogleAuth__ChildRedirectUri=https://<your-app>.fly.dev/api/v1/auth/child/google/callback \
  GoogleAuth__ChildFrontendCallbackUrl=https://<your-app>.fly.dev/login/child/google/callback \
  AllowedOrigins__0=https://<your-app>.fly.dev \
  -a <your-app>
```

In Google Cloud Console, add the production redirect URIs to the same OAuth client.

For email notifications in production:

```bash
fly secrets set \
  Email__SmtpHost=<smtp-host> \
  Email__SmtpPort=587 \
  Email__SmtpUsername=<username> \
  Email__SmtpPassword=<password> \
  Email__FromAddress=noreply@yourdomain.com \
  -a <your-app>
```

To run fly postger proxy - for db access on prod:
flyctl proxy 15433:5432 -a alexey-postgres

### 2.1 — Deploy from repository root

```bash
flyctl deploy --remote-only --config fly.toml --app <your-app>
```

### 3 — Add GitHub secrets

In your repo → Settings → Secrets and variables → Actions:

| Secret            | Value                          |
|-------------------|--------------------------------|
| `FLY_API_TOKEN`   | From Fly.io account token       |

The Fly app name is configured in workflow env as `FLY_APP_NAME` in `.github/workflows/ci-deploy.yml`.

### 4 — Push to main

```bash
git push origin main
```

GitHub Actions will:
1. Build & test the .NET project
2. Run frontend tests and build the Vite frontend
3. Build a single image that includes frontend + backend
4. Deploy one Fly.io app

---

## API Endpoints

### Auth

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/v1/auth/register | Register parent account |
| POST | /api/v1/auth/login | Get access and refresh token |
| POST | /api/v1/auth/refresh | Rotate refresh token |
| POST | /api/v1/auth/revoke | Revoke refresh token |
| GET | /api/v1/auth/google/start | Start Google OAuth for parent |
| GET | /api/v1/auth/google/callback | Google OAuth callback (parent) |
| POST | /api/v1/auth/google/finalize | Exchange one-time code for JWT (parent) |
| GET | /api/v1/auth/child/google/start | Start Google OAuth for child |
| GET | /api/v1/auth/child/google/callback | Google OAuth callback (child) |
| POST | /api/v1/auth/child/google/finalize | Exchange one-time code for JWT (child) |
| POST | /api/v1/auth/child-login | Child login by `childId + accessCode` |

### Parent — Children

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/v1/children | List parent's children |
| POST | /api/v1/children | Create child |
| PATCH | /api/v1/children/{childId} | Update child name/grade/accessCode |
| POST | /api/v1/children/{childId}/access-code/reset | Reset child access code |
| DELETE | /api/v1/children/{childId} | Delete child |

### Parent — Lessons

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/v1/lessons | List lessons (paginated, filterable) |
| POST | /api/v1/lessons | Create lesson with questions |
| GET | /api/v1/lessons/{lessonId} | Get lesson detail |
| PATCH | /api/v1/lessons/{lessonId} | Update lesson metadata and story |
| PUT | /api/v1/lessons/{lessonId}/questions | Replace lesson questions |
| POST | /api/v1/lessons/{lessonId}/duplicate | Duplicate lesson |
| DELETE | /api/v1/lessons/{lessonId} | Delete lesson |

### Parent — Assignments

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/v1/assignments | List parent assignments |
| POST | /api/v1/assignments | Assign lesson to child |
| GET | /api/v1/assignments/{assignmentId}/for-solving | Get assignment for review |
| POST | /api/v1/assignments/{assignmentId}/answers | Submit answers (instant check) |
| POST | /api/v1/assignments/{assignmentId}/complete | Complete assignment |
| GET | /api/v1/results/{resultId} | Get result with breakdown |

### Parent — Manage (Linked Accounts)

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/v1/manage/linked-parents | List linked parent accounts |
| POST | /api/v1/manage/linked-parents | Link another parent account by email |
| DELETE | /api/v1/manage/linked-parents/{linkedParentId} | Unlink a parent account |

### Parent — AI

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/v1/ai/lessons/generate | Generate AI lesson (optionally with story) |
| POST | /api/v1/ai/lessons/{lessonId}/edit | Apply AI edit command |
| GET | /api/v1/ai/lessons/{lessonId}/revisions | Get revision history |

### Parent — Reports

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/v1/reports/children/{childId} | Child progress summary |
| GET | /api/v1/reports/children/{childId}/export | CSV export |
| GET | /api/v1/results | List child results for parent |
| GET | /api/v1/results/{childId}/list | Child results list for parent |

### Child

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/v1/child/assignments | List own assignments |
| GET | /api/v1/child/assignments/{assignmentId}/for-solving | Get assignment for solving |
| POST | /api/v1/child/assignments/{assignmentId}/answers | Submit answers (instant check) |
| POST | /api/v1/child/assignments/{assignmentId}/complete | Complete assignment |
| POST | /api/v1/child/self-assign | Self-assign a friend's lesson |
| GET | /api/v1/child/results | List own results |
| GET | /api/v1/child/results/{resultId} | Get own result detail |

### Child — Friends

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/v1/child/friends | List friends |
| POST | /api/v1/child/friends/invite | Send friend invite by email |
| POST | /api/v1/child/friends/invite/{token}/accept | Accept friend invite |
| GET | /api/v1/child/friends/invite/{token} | Get invite info (public) |
| GET | /api/v1/child/friends/{friendChildId}/results | View friend's results |
| GET | /api/v1/child/friends/{friendChildId}/assignments | View friend's assignments |
| GET | /api/v1/child/friends/{friendChildId}/note | Get friend note |
| PUT | /api/v1/child/friends/{friendChildId}/note | Update friend note |

### Health

| Method | Path | Description |
|--------|------|-------------|
| GET | /health | Health check |
| GET | /health/live | Liveness check |
| GET | /health/ready | Readiness check (DB ping) |
| GET | /api/v1/health | Versioned health check |

### Auth header for protected routes

`Authorization: Bearer <access-token>`

### SignalR

`/hubs/friends` — real-time friend notifications for child users.

---

## Project Structure

```
.
├── .github/
│   └── workflows/
│       └── ci-deploy.yml         # CI + Fly.io deploy
├── backend/
│   └── KidsLearn.Api/
│       ├── Application/          # CQRS commands and queries (MediatR)
│       ├── Controllers/          # Minimal API route groups
│       ├── EFMigration/          # EF Core migrations
│       ├── Models/               # Domain models + AppDbContext
│       ├── Services/             # Email, AI generation, assignment solving
│       ├── Program.cs            # App startup + DI
│       └── Dockerfile
├── backend/
│   └── KidsLearn.Api.Tests/      # xUnit unit tests
├── frontend/
│   └── src/
│       ├── auth/                 # AuthProvider + session management
│       ├── components/           # Shared UI components
│       ├── lib/                  # Typed API client (api.ts)
│       ├── pages/                # Page-level components
│       └── App.jsx               # Router + layout
└── docker-compose.yml            # Local dev
```
