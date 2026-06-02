# Hello World — React + .NET 8 + PostgreSQL

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

Add as **Variables** (not secrets):

| Variable            | Value                                         |
|---------------------|-----------------------------------------------|
| `FLY_BACKEND_APP`   | your Fly app name                             |

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
