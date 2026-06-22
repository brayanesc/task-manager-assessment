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

## Architecture decisions & production extensions

### Decisions made for this scope

| Decision | Choice | Rationale |
|---|---|---|
| Password hashing | `ASP.NET Core PasswordHasher<T>` | In-box, no extra dependency; PBKDF2-SHA512 with a random salt, NIST-compliant. |
| JWT signing | HS256 (symmetric) | Single service sharing one secret via environment config — sufficient for horizontal scaling of one service. See upgrade path below. |
| Due-date storage | `DateOnly` → SQLite `TEXT` (`YYYY-MM-DD`) | SQLite has no native date type; ISO-8601 text sorts correctly and is unambiguous. Validated against `DateOnly.FromDateTime(DateTime.UtcNow)`. |
| Data store | SQLite | Zero-config for demo portability. **Single-writer constraint** means it does not support multiple concurrent API instances writing simultaneously. For production, swap the `ITaskRepository` / `IUserRepository` implementations for PostgreSQL or SQL Server — no domain or application code changes needed. |

### HS256 → RS256 upgrade path

For multi-service architectures where token verification must be distributed (other services
need to validate tokens without sharing the secret), upgrade to RS256:

1. Generate an RSA key pair; store the private key in secrets, publish the public key via JWKS.
2. Change `AddJwtBearer` signing credentials in `Infrastructure/Auth/JwtTokenService.cs` to use `RsaSecurityKey`.
3. No domain, application, or controller code changes — the token shape is identical.

### Out of scope (deliberate)

The following were intentionally left out as production extensions, to keep the exercise
appropriately scoped: Polly retry / circuit breaker for transient DB faults, rate limiting,
API versioning, response caching, distributed tracing, task sharing, notifications, soft delete.