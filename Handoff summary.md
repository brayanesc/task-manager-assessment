# Task Manager Assessment — Conversation Handoff

> Summary of all decisions, context, and current state for AI handoff.
> Project: take-home technical assessment. Stack: .NET 10 + Angular 18, monorepo.
> Last updated: 2026-06-22

---

## 1. Project Identity

| Item | Value |
|---|---|
| Repo name | `task-manager-assessment` |
| Monorepo structure | `/backend` (.NET 10) + `/frontend` (Angular 18) |
| Assessment type | Code-reviewed take-home, presented to a senior panel |
| Developer | Ricardo — Senior Full-Stack Engineer, 9+ years, specializes in Angular/TypeScript/.NET |
| AI tool in use | Claude Code via VS Code extension (switching to Augment Code) |

---

## 2. Monorepo Structure

```
task-manager-assessment/
├── .gitignore              (single root gitignore — covers .NET + Angular + SQLite)
├── CLAUDE.md               (AI memory file — loaded automatically by Claude Code)
├── README.md               (skeleton — partially filled, see section 8)
├── docs/
│   ├── user-story.md       (full story + Given/When/Then acceptance criteria)
│   └── genai-writeup.md    (to be filled at the end — GenAI section of assessment)
├── backend/
│   ├── TaskManager.sln
│   ├── Directory.Build.props   (Nullable+ImplicitUsings; NuGetAuditMode=direct)
│   ├── src/
│   │   ├── TaskManager.Domain/
│   │   ├── TaskManager.Application/
│   │   ├── TaskManager.Infrastructure/
│   │   └── TaskManager.Api/
│   └── tests/
│       ├── TaskManager.Domain.Tests/
│       ├── TaskManager.Application.Tests/
│       ├── TaskManager.Infrastructure.Tests/
│       └── TaskManager.Api.Tests/
├── frontend/               (Angular 18 — not started yet)
└── docker-compose.yml      (not created yet — final phase)
```

---

## 3. Tech Stack — Confirmed Decisions

| Layer | Decision | Reason |
|---|---|---|
| Backend runtime | .NET 10 (LTS), C# | LTS, correct naming (not "Core") |
| Data access | Raw ADO.NET — `Microsoft.Data.Sqlite` | EF/Dapper/MediatR explicitly prohibited |
| Data store | SQLite | Demo portability, zero infra, ADO.NET puro; migration path documented |
| Auth | JWT HS256 symmetric, secret via user-secrets | Proportionate for single service; RS256 noted as production extension |
| Password hashing | `ASP.NET Core PasswordHasher<T>` (PBKDF2+HMAC-SHA256) | Framework-native, zero extra NuGet |
| Due date type | `DateOnly` stored as TEXT `YYYY-MM-DD` in SQLite | Semantic correctness; validated against `DateOnly.FromDateTime(DateTime.UtcNow)` |
| Test framework | xUnit + Moq | Standard .NET TDD stack |
| Frontend | Angular 18 | Developer's primary expertise |
| Frontend styling | Tailwind CSS | — |
| Frontend testing | Jasmine | Angular default |
| Coverage tool | Coverlet + reportgenerator | Lightweight, zero infra, HTML report |
| E2E | Playwright (post-frontend, if time allows) | — |
| Containerization | Docker + docker-compose (final phase) | Single `docker compose up` for demo |

---

## 4. Architecture — Enforced by Compiler

```
Domain  ←  Application  ←  Infrastructure  ←  Api
```

- **Domain** — entities + domain rules, zero external dependencies.
- **Application** — use-case interfaces, DTOs, validation, business rules. Depends only on Domain.
- **Infrastructure** — ADO.NET repositories, SQLite schema/seed, JWT, password hashing.
- **Api** — controllers, DI wiring, JWT middleware. Zero business logic.

Each layer is its own `.csproj`. A reference in the wrong direction is a **build error**.

---

## 5. Design Patterns in Use

| Pattern | Where | Why |
|---|---|---|
| Repository | Application (interface) + Infrastructure (impl) | Abstracts data access; enables swapping SQLite → SQL Server without touching Domain/Application |
| Unit of Work | Application (IUnitOfWork interface) + Infrastructure (UnitOfWork wraps SqliteConnection + SqliteTransaction) | Coordinates multi-repository transactions atomically |
| Result<T> | Application use cases | Use cases return `Result<T>` instead of throwing for business rule failures. `DomainException` reserved for unexpected/unrecoverable errors only |
| Factory Method | Domain entities | `TaskItem.Create(...)` for new records (validates rules); `TaskItem.Reconstitute(...)` for ADO.NET mapping (bypasses due-date validation — persisted records can legitimately have past dates) |

**Patterns explicitly rejected (over-engineering for this scope):**
- Specification Pattern — queries are simple, no combinable filters needed
- Decorator Pattern — optional only if time allows after frontend
- Separate Factory classes — factory methods on entities are sufficient

---

## 6. Backend Conventions (from CLAUDE.md)

```
- .NET 10, C#. PascalCase projects: TaskManager.Domain, .Application, .Infrastructure, .Api.
- async/await end-to-end. Passwords hashed, never logged. JWT auth.
- No business logic in controllers.
- Result Pattern: use cases return Result<T> instead of throwing for business rule
  failures. DomainException reserved for unexpected/unrecoverable errors only.
- Unit of Work: use cases receive IUnitOfWork (defined in Application) instead of
  individual repositories. IUnitOfWork owns the transaction scope.
  Infrastructure implementation: UnitOfWork wraps SqliteConnection + SqliteTransaction.
```

---

## 7. Hard Constraints (non-negotiable — violating any fails the review)

```
- NO Entity Framework, NO Dapper, NO MediatR.
- Data access: raw ADO.NET, parameterized queries only.
- Clean Architecture: dependency direction Domain <- Application <- Infrastructure/API.
  Each layer is its own .csproj. Domain has zero external dependencies.
- TDD: write the failing xUnit test first, then minimal code, then refactor.
```

---

## 8. Infrastructure Rules (ADO.NET Robustness — non-negotiable)

These must be applied in every repository method:

```
- New SqliteConnection per method — never a shared connection field.
- await using for every IAsyncDisposable (connection, command, reader).
- CancellationToken passed to every async ADO.NET call.
- GetOrdinal("ColumnName") for all reader access — never positional indexes.
- Single private static Map method per repository — no inline mapping.
- Transactions for multi-statement operations via IUnitOfWork.
- PRAGMA journal_mode=WAL on DatabaseInitializer startup.
- CREATE INDEX IF NOT EXISTS idx_tasks_user_id ON Tasks(user_id).
```

---

## 9. Non-Functional Requirements (built — not out of scope)

```
- Stateless API (JWT only, no server session) — horizontal scale by default.
- async/await end-to-end; no blocking DB calls.
- Global exception middleware returning RFC 7807 ProblemDetails; never leak stack traces.
- GET /api/tasks paginated (page, pageSize) — never return unbounded list.
- Structured logging via ILogger; expose GET /health (anonymous).
- CORS configured for the Angular origin.
- Secrets via user-secrets / config — never hard-coded.
```

---

## 10. Out of Scope (documented in README as production extensions)

```
- Polly retry/circuit-breaker for transient DB faults
- Rate limiting
- API versioning
- Response caching
- Distributed tracing
- Soft delete / audit history
- Task sharing between users
- Refresh token mechanism
- RS256 asymmetric JWT (noted: upgrade path for multi-service architectures)
- SQLite WAL mode (noted: PRAGMA journal_mode=WAL; one line at startup)
- idx_tasks_user_id index (noted: prevents full table scan on GET /api/tasks)
- Playwright E2E (noted: post-frontend if time allows)
- SonarQube (noted: CI/CD extension for production)
```

---

## 11. Architecture Decisions Documented in README

| Decision | Trade-off | Production fix |
|---|---|---|
| SQLite | Single-writer; no horizontal scale with multiple API instances | Swap Infrastructure repos to PostgreSQL/SQL Server — Domain/Application untouched |
| HS256 symmetric JWT | All instances share secret via env config (fine for single service) | RS256 for multi-service token verification |
| PasswordHasher<T> | Stateless, instance-safe | No changes needed at scale |
| No WAL mode | Default journal blocks readers during writes | `PRAGMA journal_mode=WAL` on connection open |
| No index on Tasks.user_id | Full table scan on every GET /api/tasks | `CREATE INDEX idx_tasks_user_id ON Tasks(user_id)` |
| No refresh token | Users must re-login on expiry | Refresh token endpoint + HttpOnly cookie + short-lived access token |

---

## 12. Current Build State

| Layer | Status | Tests |
|---|---|---|
| Domain | ✅ Complete | 21/21 green |
| Application | ✅ Complete | All AC covered |
| Infrastructure | 🔲 Not started | — |
| Api | 🔲 Not started | — |
| Frontend | 🔲 Not started | — |
| Docker | 🔲 Not started | — |

**Last commit pushed:** `feat(application): use cases, DTOs and interfaces — all acceptance criteria covered`

**Next commit pending:** CLAUDE.md update (Result<T> + UoW conventions) — not yet committed.

---

## 13. Next Steps in Order

```
1. Update CLAUDE.md with Result<T> and Unit of Work conventions (pending commit).
2. Infrastructure layer — prompt below.
3. Api layer.
4. Frontend (Angular 18) — separate prompt, builds against real endpoints.
5. Coverlet coverage report.
6. Docker + docker-compose.
7. Playwright E2E (if time allows).
8. docs/genai-writeup.md (GenAI section of assessment).
9. Final README update with run instructions + demo credentials.
```

---

## 14. Infrastructure Prompt (ready to send)

```
Confirm you have read CLAUDE.md. Restate the two new conventions
(Result<T> and Unit of Work) in one line each before proceeding.

Then build the Infrastructure layer (TaskManager.Infrastructure +
TaskManager.Infrastructure.Tests). Strict TDD.

SQLITE SETUP
- DatabaseInitializer: creates schema + seed on startup.
  Run PRAGMA journal_mode=WAL on every new connection.
  CREATE INDEX IF NOT EXISTS idx_tasks_user_id ON Tasks(user_id).
- Seed: one demo user (email: demo@taskmanager.com, password: Demo1234!)
  and three sample tasks.

REPOSITORY RULES (non-negotiable for robustness)
- New SqliteConnection per method — never a shared connection field.
- await using for every IAsyncDisposable (connection, command, reader).
- CancellationToken passed to every async ADO.NET call.
- GetOrdinal("ColumnName") for all reader access — never positional indexes.
- Single private static Map method per repository — no inline mapping.
- Transactions for multi-statement operations via IUnitOfWork.

PATTERNS
- Result<T>: repositories return domain objects or null — they do NOT return Result<T>.
  Result<T> is the use case return type. Keep the boundary clean.
- Unit of Work: IUnitOfWork (already defined in Application) wraps
  SqliteConnection + SqliteTransaction. Repositories receive the active
  connection/transaction from UnitOfWork — they do not open their own connections
  when called within a UoW scope.

IMPLEMENTATIONS
- UnitOfWork: IUnitOfWork (owns connection + transaction lifecycle)
- TaskRepository: ITaskRepository
- UserRepository: IUserRepository
- PasswordHasherService: IPasswordHasher using ASP.NET Core PasswordHasher<T>
- JwtTokenService: ITokenService using HS256 symmetric key from configuration
- DatabaseInitializer: schema + WAL pragma + seed

TESTS (TaskManager.Infrastructure.Tests)
- Use real SQLite in-memory connection (:memory:) — no mocks for repositories.
- One test class per repository.
- Cover: CRUD happy paths, not-found returns null, pagination (page 1 + page 2),
  duplicate email check, UnitOfWork commit + rollback behavior.
- PasswordHasher: Hash produces non-plaintext, Verify round-trips correctly.
- DatabaseInitializer: tables exist and seed user present after init.

SCALABILITY CHECKS
- Every async ADO.NET call receives CancellationToken.
- WAL mode enables concurrent reads — verify in DatabaseInitializer test.
- idx_tasks_user_id prevents full table scan — verify query plan in a test comment.

After tests are green, print summary table:
  Component | Tests | Key behaviors covered
Then stop and wait for instruction before moving to Api layer.
```

---

## 15. Commit Convention (Conventional Commits)

```
chore:   config, tooling, scaffolding
feat:    new functionality
test:    tests
refactor: no behavior change
fix:     bug fix
docs:    documentation
```

Scope by layer: `feat(domain):`, `feat(application):`, `feat(infrastructure):`,
`feat(api):`, `feat(web):`, `chore(docker):`

---

## 16. User Story Reference

Full story + acceptance criteria live in `docs/user-story.md`.
Every acceptance criterion maps to at least one xUnit test.

Summary:
- CRUD on personal tasks (title, description, status, due date)
- Tasks are private — user sees only their own
- Register + login with JWT
- Pagination on GET /api/tasks
- Ownership validation on all mutations
- 401 on protected endpoints without token
- 400 with validation message on invalid input
- 404 when task not found or belongs to another user
- 204 on successful delete
- 201 + Location header on successful create