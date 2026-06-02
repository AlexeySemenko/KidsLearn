# Hello World — React + .NET 8 + PostgreSQL

Responsive full-stack starter. Runs locally with Docker Compose; deploys to Railway via GitHub Actions.

## Stack

| Layer      | Tech                        |
|------------|-----------------------------|
| Frontend   | React 18 + Vite             |
| Backend    | .NET 8 Minimal API          |
| Database   | PostgreSQL 16               |
| Deploy     | Railway (free tier)         |
| CI/CD      | GitHub Actions → GHCR       |

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

## Deploy to Railway

### 1 — Create a Railway project

1. Sign up at https://railway.app (free tier available)
2. Create a new project → **Empty project**
3. Add services:
   - **PostgreSQL** (from Railway plugin) — copy the `DATABASE_URL`
   - **Backend** — connect your GitHub repo, set root directory `backend/HelloWorld.Api`
   - **Frontend** — connect your GitHub repo, set root directory `frontend`

### 2 — Set environment variables in Railway

**Backend service**
```
DATABASE_URL=<from Railway Postgres plugin>
AllowedOrigins__0=https://<your-frontend-domain>.railway.app
```

**Frontend service**
```
VITE_API_URL=https://<your-backend-domain>.railway.app
```

### 3 — Add GitHub secrets

In your repo → Settings → Secrets and variables → Actions:

| Secret            | Value                          |
|-------------------|--------------------------------|
| `RAILWAY_TOKEN`   | From Railway → Account → Tokens |

Add as a **Variable** (not secret):

| Variable        | Value                                           |
|-----------------|-------------------------------------------------|
| `VITE_API_URL`  | `https://<your-backend-domain>.railway.app`     |

### 4 — Push to main

```bash
git add .
git commit -m "initial commit"
git push origin main
```

GitHub Actions will:
1. Build & test the .NET project
2. Build the Vite frontend
3. Push Docker images to GHCR
4. Deploy both services to Railway

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
│       └── ci-deploy.yml     # CI + Railway deploy
├── backend/
│   └── HelloWorld.Api/
│       ├── Program.cs          # Minimal API + EF Core
│       ├── Dockerfile
│       └── railway.toml
├── frontend/
│   ├── src/
│   │   ├── main.jsx
│   │   └── App.jsx             # Responsive UI
│   ├── Dockerfile
│   ├── nginx.conf
│   └── vite.config.js
└── docker-compose.yml          # Local dev
```
