# KidsLearn — React + .NET 8 + PostgreSQL

Responsive full-stack starter. Runs locally with Docker Compose; deploys to a single Fly.io app via GitHub Actions.

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

- Frontend → http://localhost:3000  
- Backend  → http://localhost:8080  
- Postgres → localhost:5432

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

Use Fly Postgres or any managed Postgres provider and place its connection string in `DATABASE_URL`.

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
2. Build the Vite frontend
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
| POST   | /api/v1/auth/refresh | Rotate refresh token and issue new access token |
| POST   | /api/v1/auth/revoke | Revoke refresh token |
| GET    | /api/hello  | Returns latest greeting       |
| POST   | /api/hello  | `{"message":"..."}` — saves a new greeting |
| GET    | /api/v1/children | List children for authenticated parent |
| POST   | /api/v1/children | Create child `{ "name": "...", "grade": 1..12, "accessCode": "2468" }` |
| PATCH  | /api/v1/children/{childId} | Update child name/grade and optional access code |
| POST   | /api/v1/children/{childId}/access-code/reset | Reset child access code |
| DELETE | /api/v1/children/{childId} | Delete child |
| POST   | /api/v1/lessons | Create lesson with nested questions and answers |
| GET    | /api/v1/lessons?page=1&pageSize=20 | List parent lessons with pagination |
| GET    | /api/v1/lessons/{lessonId} | Get lesson details |
| PATCH  | /api/v1/lessons/{lessonId} | Update lesson metadata |
| DELETE | /api/v1/lessons/{lessonId} | Delete lesson (if no assignments) |
| POST   | /api/v1/assignments | Assign parent lesson to parent child |
| GET    | /api/v1/assignments | List assignments for authenticated parent |
| GET    | /api/v1/assignments/{assignmentId}/for-solving | Get assignment payload for solving |
| POST   | /api/v1/assignments/{assignmentId}/answers | Submit answers and get instant check |
| POST   | /api/v1/assignments/{assignmentId}/complete | Complete assignment and calculate score |
| GET    | /api/v1/results/{resultId} | Get result with correctness breakdown |
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
