# Task Manager Assessment

Full-stack technical take-home: a personal task manager built with **.NET 10** (Clean Architecture,
TDD, raw ADO.NET) and **Angular 18**.

- User story and acceptance criteria: [`docs/user-story.md`](docs/user-story.md)
- GenAI prompt-engineering writeup: [`docs/genai-writeup.md`](docs/genai-writeup.md)

---

## Architecture

Clean Architecture with the dependency direction enforced by project references
(a reference in the wrong direction does not compile):

```
Domain  <-  Application  <-  Infrastructure / API
```

- **Domain** — entities and business rules, zero external dependencies.
- **Application** — use cases, DTOs, interfaces, validation. Depends only on Domain.
- **Infrastructure** — ADO.NET repositories, JWT, password hashing. Implements Application interfaces.
- **API** — ASP.NET controllers, DI wiring, JWT middleware. No business logic.

### Key decisions

- **No Entity Framework, Dapper, or MediatR** — data access is raw ADO.NET with
  parameterized queries; use cases replace the mediator pattern.
- **Stateless API** (JWT only) — scales horizontally by default.
- **TDD** — every layer is covered by xUnit tests, written test-first.

> Full rationale and out-of-scope decisions are documented at the end of this README.

---

## Tech stack

| Layer | Tech |
|---|---|
| Backend | .NET 10, ASP.NET Core, raw ADO.NET, JWT, xUnit |
| Frontend | Angular 18, TypeScript, RxJS, Signals, Tailwind, Jasmine |
| Data | SQLite |

---

## Project structure

```
task-manager-assessment/
├── CLAUDE.md
├── docs/
│   ├── user-story.md
│   └── genai-writeup.md
├── backend/
│   ├── TaskManager.sln
│   ├── src/   (Domain, Application, Infrastructure, Api)
│   └── tests/ (one test project per layer)
├── frontend/
└── docker-compose.yml
```

---

## Getting started

> _To be completed as the project is built._

### Prerequisites

- .NET 10 SDK
- Node.js (for Angular 18) / Angular CLI
- Docker (optional, for containerized run)

### Run with Docker (recommended for demo)

```bash
docker compose up
```

### Run locally

```bash
# Backend
cd backend
dotnet build TaskManager.sln
dotnet test TaskManager.sln
dotnet run --project src/TaskManager.Api

# Frontend
cd frontend
npm install
ng serve
```

### Demo credentials

> _Seeded credentials will be listed here._

| Email | Password |
|---|---|
|  | |

---

## Testing

```bash
# Backend
cd backend && dotnet test TaskManager.sln

# Frontend
cd frontend && ng test
```

---

## Out of scope (deliberate)

The following were intentionally left out as production extensions, to keep the exercise
appropriately scoped: Polly retry / circuit breaker for transient DB faults, rate limiting,
API versioning, response caching, distributed tracing, task sharing, notifications, soft delete.