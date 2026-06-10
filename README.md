# KidsLearn — React + .NET 8 + PostgreSQL

Responsive full-stack starter. Runs locally with Docker Compose; deploys to a single Fly.io app via GitHub Actions.

## Current Status (2026-06-07)

- Backend: complete and stable for core domain flows (auth, children, lessons, assignments, child solving, reports, AI generation/edit endpoints).
- Frontend: foundation and major feature slices are implemented.
  - Done: auth/session, role-protected shell, parent dashboard/children/lessons/assignments/reports, child assignments solving + child result detail, AI lesson generation/editing + revision history.
  - Hardening delivered: global error boundary, shared typed API module, frontend regression tests in CI, keyboard/focus/accessibility pass on key flows.

## Stack

| Layer      | Tech                        |
|------------|-----------------------------|
| Frontend   | React 18 + Vite             |
| Backend    | .NET 8 Minimal API          |
| Database   | PostgreSQL 16               |
| Deploy     | Fly.io (single app)         |
| CI/CD      | GitHub Actions              |

---

## Local Development

### Option A — Docker Compose (recommended)

```bash
docker compose up --build
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

### Google SSO config for parent login/registration

`docker-compose.yml` also reads:

- `GOOGLE_AUTH_CLIENT_ID`
- `GOOGLE_AUTH_CLIENT_SECRET`
- `GOOGLE_AUTH_REDIRECT_URI` (default `http://localhost:8080/api/v1/auth/google/callback`)
- `GOOGLE_AUTH_FRONTEND_CALLBACK_URL` (default `http://localhost:8080/login/parent/google/callback`)

Google Cloud OAuth app setup notes:

- Authorized redirect URI: `http://localhost:8080/api/v1/auth/google/callback`
- Frontend callback page (compose flow): `http://localhost:8080/login/parent/google/callback`
- For local Vite flow, use: `http://localhost:5173/login/parent/google/callback`

Add these to root `.env`:

```env
GOOGLE_AUTH_CLIENT_ID=<google-client-id>
GOOGLE_AUTH_CLIENT_SECRET=<google-client-secret>
GOOGLE_AUTH_REDIRECT_URI=http://localhost:8080/api/v1/auth/google/callback
GOOGLE_AUTH_FRONTEND_CALLBACK_URL=http://localhost:8080/login/parent/google/callback
```

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

Set these on your Fly app:

```
fly secrets set DATABASE_URL=<postgres-connection-string> AllowedOrigins__0=https://<your-app>.fly.dev -a <your-app>
```

For AI generation/editing in production, also set:

```bash
fly secrets set OpenAI__ApiKey=<openai-key> OpenAI__Model=gpt-4o-mini -a <your-app>
```

For Google SSO in production, set:

```bash
fly secrets set \
  GoogleAuth__ClientId=<google-client-id> \
  GoogleAuth__ClientSecret=<google-client-secret> \
  GoogleAuth__RedirectUri=https://<your-app>.fly.dev/api/v1/auth/google/callback \
  GoogleAuth__FrontendCallbackUrl=https://<your-app>.fly.dev/login/parent/google/callback \
  AllowedOrigins__0=https://<your-app>.fly.dev \
  -a <your-app>
```

In Google Cloud Console, add the production redirect URI to the same OAuth client:

- `https://<your-app>.fly.dev/api/v1/auth/google/callback`

Use Fly Postgres or any managed Postgres provider and place its connection string in `DATABASE_URL`.

### 2.1 — Deploy from repository root

If you run deploy commands from repository root, use the root `fly.toml`:

```bash
flyctl deploy --remote-only --config fly.toml --app <your-app>
```

Why: this project keeps `backend/KidsLearn.Api/Dockerfile` outside root. The root `fly.toml` points Fly to the correct Dockerfile path so deploy works without changing directories.

### 3 — Add GitHub secrets

In your repo → Settings → Secrets and variables → Actions:

| Secret            | Value                          |
|-------------------|--------------------------------|
| `FLY_API_TOKEN`   | From Fly.io account token       |

The Fly app name is configured in workflow env as `FLY_APP_NAME` in `.github/workflows/ci-deploy.yml`.

### 4 — Push to main

```bash
git add .
git commit -m "initial commit"
git push origin main
```

GitHub Actions will:
1. Build & test the .NET project
2. Run frontend tests and build the Vite frontend
3. Build a single image that includes frontend + backend
4. Deploy one Fly.io app

---

## API Endpoints

| Method | Path        | Description                   |
|--------|-------------|-------------------------------|
| GET    | /health     | Health check                  |
| GET    | /api/v1/health | Versioned health check     |
| POST   | /api/v1/auth/register | Register parent account |
| POST   | /api/v1/auth/login | Get access and refresh token |
| POST   | /api/v1/auth/child-login | Child login by `childId + accessCode` |
| GET    | /api/v1/auth/google/start | Start Google OAuth redirect flow for parent auth |
| GET    | /api/v1/auth/google/callback | Google OAuth callback endpoint |
| POST   | /api/v1/auth/google/finalize | Exchange one-time auth code for app JWT + refresh tokens |
| POST   | /api/v1/auth/refresh | Rotate refresh token and issue new access token |
| POST   | /api/v1/auth/revoke | Revoke refresh token |
| GET    | /api/v1/children | List children for authenticated parent |
| POST   | /api/v1/children | Create child `{ "name": "...", "grade": 1..12, "accessCode": "2468" }` |
| PATCH  | /api/v1/children/{childId} | Update child name/grade and optional access code |
| POST   | /api/v1/children/{childId}/access-code/reset | Reset child access code |
| DELETE | /api/v1/children/{childId} | Delete child |
| POST   | /api/v1/ai/lessons/generate | Generate AI lesson draft and persist lesson |
| POST   | /api/v1/ai/lessons/{lessonId}/edit | Apply AI lesson edit command and create revision |
| GET    | /api/v1/ai/lessons/{lessonId}/revisions | Get AI lesson revision history |
| POST   | /api/v1/lessons | Create lesson with nested questions and answers |
| GET    | /api/v1/lessons?page=1&pageSize=20 | List parent lessons with pagination |
| GET    | /api/v1/lessons/{lessonId} | Get lesson details |
| POST   | /api/v1/lessons/{lessonId}/duplicate | Duplicate own lesson with questions/answers |
| PATCH  | /api/v1/lessons/{lessonId} | Update lesson metadata |
| DELETE | /api/v1/lessons/{lessonId} | Delete lesson (if no assignments) |
| POST   | /api/v1/assignments | Assign parent lesson to parent child |
| GET    | /api/v1/assignments | List assignments for authenticated parent |
| GET    | /api/v1/assignments/{assignmentId}/for-solving | Get assignment payload for solving |
| POST   | /api/v1/assignments/{assignmentId}/answers | Submit answers and get instant check |
| POST   | /api/v1/assignments/{assignmentId}/complete | Complete assignment and calculate score |
| GET    | /api/v1/results/{resultId} | Get result with correctness breakdown |
| GET    | /api/v1/reports/children/{childId}?from=&to= | Parent child progress summary |
| GET    | /api/v1/reports/children/{childId}/export?format=csv&from=&to= | Parent child report CSV export |
| GET    | /api/v1/child/assignments | Child list of own assignments |
| GET    | /api/v1/child/assignments/{assignmentId}/for-solving | Child gets own assignment for solving |
| POST   | /api/v1/child/assignments/{assignmentId}/answers | Child submits answers and gets instant check |
| POST   | /api/v1/child/assignments/{assignmentId}/complete | Child completes own assignment |
| GET    | /api/v1/child/results/{resultId} | Child gets own result |

### Auth header for protected routes

Send a bearer token returned by `/api/v1/auth/login`.

Example:

`Authorization: Bearer <access-token>`

---

## Project Structure

```
.
├── .github/
│   └── workflows/
│       └── ci-deploy.yml     # CI + Fly.io deploy
├── backend/
│   └── KidsLearn.Api/
│       ├── Program.cs          # Minimal API + EF Core
│       ├── Dockerfile          # Builds frontend + backend in one image
│       └── fly.toml
├── frontend/
│   ├── src/
│   │   ├── main.jsx
│   │   └── App.jsx             # Responsive UI
│   ├── Dockerfile
│   ├── nginx.conf
│   └── vite.config.js
└── docker-compose.yml          # Local dev
```
