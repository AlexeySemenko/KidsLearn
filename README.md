# Hello World — React + .NET 8 + PostgreSQL

Responsive full-stack starter. Runs locally with Docker Compose; deploys to Fly.io via GitHub Actions.

## Stack

| Layer      | Tech                        |
|------------|-----------------------------|
| Frontend   | React 18 + Vite             |
| Backend    | .NET 8 Minimal API          |
| Database   | PostgreSQL 16               |
| Deploy     | Fly.io + managed Postgres   |
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
docker run -e POSTGRES_DB=helloworld -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16-alpine
```

**Backend**
```bash
cd backend/HelloWorld.Api
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

### 1 — Create Fly apps

1. Sign up at https://fly.io
2. Install `flyctl` and authenticate:
  ```bash
  fly auth login
  ```
3. Create two Fly apps (names must be globally unique):
  - one for backend API
  - one for frontend web

### 2 — Set backend secrets on Fly

Set these on your backend app:

```
fly secrets set DATABASE_URL=<postgres-connection-string> AllowedOrigins__0=https://<your-frontend-app>.fly.dev -a <your-backend-app>
```

Use Fly Postgres or any managed Postgres provider and place its connection string in `DATABASE_URL`.

### 3 — Add GitHub secrets

In your repo → Settings → Secrets and variables → Actions:

| Secret            | Value                          |
|-------------------|--------------------------------|
| `FLY_API_TOKEN`   | From Fly.io account token       |

Add as **Variables** (not secrets):

| Variable            | Value                                         |
|---------------------|-----------------------------------------------|
| `FLY_BACKEND_APP`   | your backend Fly app name                     |
| `FLY_FRONTEND_APP`  | your frontend Fly app name                    |
| `VITE_API_URL`      | `https://<your-backend-app>.fly.dev`          |

### 4 — Push to main

```bash
git add .
git commit -m "initial commit"
git push origin main
```

GitHub Actions will:
1. Build & test the .NET project
2. Build the Vite frontend
3. Deploy backend to Fly.io
4. Deploy frontend to Fly.io

---

## API Endpoints

| Method | Path        | Description                   |
|--------|-------------|-------------------------------|
| GET    | /health     | Health check                  |
| GET    | /api/hello  | Returns latest greeting       |
| POST   | /api/hello  | `{"message":"..."}` — saves a new greeting |

---

## Project Structure

```
.
├── .github/
│   └── workflows/
│       └── ci-deploy.yml     # CI + Fly.io deploy
├── backend/
│   └── HelloWorld.Api/
│       ├── Program.cs          # Minimal API + EF Core
│       ├── Dockerfile
│       └── fly.toml
├── frontend/
│   ├── src/
│   │   ├── main.jsx
│   │   └── App.jsx             # Responsive UI
│   ├── Dockerfile
│   ├── fly.toml
│   ├── nginx.conf
│   └── vite.config.js
└── docker-compose.yml          # Local dev
```
