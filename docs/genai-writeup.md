# GenAI Tool Usage — Task Manager Assessment

## Tools used

| Phase | Tool | Model |
|---|---|---|
| Backend (all layers) | **Claude Code** — Anthropic's official VS Code extension | `claude-sonnet-4-6` |
| Frontend (Angular) | **Augment Code** — VS Code extension | GPT-4o / Claude 3.5 Sonnet |

Both tools are conversational coding agents that read and write files directly, run shell
commands, and reason about architecture. Neither is a simple autocomplete — they understand
context, enforce patterns across files, and explain their reasoning on request.

---

## Prompt strategy

Rather than asking the tools to "build a task manager," I treated each prompt as a
**formal specification** the model had to satisfy. Every prompt included:

- **ROLE** — frames the model's persona and quality bar.
- **CONTEXT** — project boundaries, hard constraints, what's already decided.
- **STOP + ASK** — forces clarifying questions before writing code, surfacing decisions early.
- **DEFINITION OF DONE** — gives the model an explicit exit condition so it doesn't
  over-engineer or stop too early.

This approach means the model cannot rationalise its way around a constraint it has been
told about explicitly.

---

## Prompt 1 — Backend scaffold (Claude Code)

```
ROLE
You are a senior .NET engineer. You optimize for clean separation of concerns,
testability, and code a reviewer can read without explanation. You favor appropriate
engineering over the kitchen sink, and you can justify what you deliberately leave out.

CONTEXT
This is a code-reviewed take-home. The monorepo root is task-manager-assessment/.
Backend lives under /backend, frontend (Angular 18) is a separate later phase under /frontend.
Hard constraints and conventions are already loaded from CLAUDE.md — treat them as active.

User story (from docs/user-story.md — read it for the full acceptance criteria):
"As a registered user, I want to create, view, update and delete my personal tasks
so I can track my work; each task has a title, description, status (Todo/InProgress/Done)
and due date. Tasks are private — a user can only see and modify their own."

TECH VERSIONS
- .NET 10 (LTS), C#, xUnit.
- Data store: SQLite via Microsoft.Data.Sqlite (raw ADO.NET, parameterized queries only).
- Two tables: Tasks (id PK, title, description, status, due_date, user_id)
              Users (id PK, email, password_hash).

NON-FUNCTIONAL REQUIREMENTS (build these — proportionate and expected for this scope)
- Stateless API: JWT only, no server session — horizontal scale by default.
- async/await end-to-end; no blocking calls.
- Global exception middleware returning RFC 7807 ProblemDetails; never leak stack traces.
- GET /api/tasks paginated (page, pageSize) — never return an unbounded list.
- Structured logging via ILogger; expose GET /health (anonymous).
- CORS configured for the Angular origin.
- Secrets via user-secrets / config — never hard-coded.

OUT OF SCOPE (note in README as production extensions — do NOT build unless I ask)
- Polly retry/circuit-breaker, rate limiting, API versioning, response caching,
  distributed tracing.

ARCHITECTURE
Enforce the dependency direction the COMPILER already guarantees via .csproj references:
  Domain <- Application <- Infrastructure / Api
- Domain:         entities + domain rules, zero external dependencies.
- Application:    use-case interfaces, DTOs, validation, business rules. Depends only on Domain.
- Infrastructure: ADO.NET repositories + SQLite schema/seed + JWT + password hashing.
- Api:            controllers, DI wiring, JWT middleware. Zero business logic here.

Solution structure under /backend:
  backend/
  ├── TaskManager.sln
  ├── src/
  │   ├── TaskManager.Domain/
  │   ├── TaskManager.Application/
  │   ├── TaskManager.Infrastructure/
  │   └── TaskManager.Api/
  └── tests/
      ├── TaskManager.Domain.Tests/
      ├── TaskManager.Application.Tests/
      ├── TaskManager.Infrastructure.Tests/
      └── TaskManager.Api.Tests/

WORKFLOW (TDD — red-green-refactor, enforced)
For each use case: write the failing xUnit test first → minimal code to pass → refactor.
Every acceptance criterion in docs/user-story.md maps to at least one test.
Tests per layer:
- Domain.Tests:          domain rules in isolation (no mocks needed).
- Application.Tests:     use cases with mocked repositories.
- Infrastructure.Tests:  repositories against an in-memory or test SQLite store.
- Api.Tests:             controllers — authorized and anonymous endpoints.

OUTPUT
1. Scaffold the full /backend solution tree with .csproj references wired correctly.
2. Print the resulting tree.
3. Then STOP — ask me up to 3 clarifying questions before writing any implementation code.
   (SQLite, .NET 10 and xUnit are already decided — do not ask about these.)

We then build layer by layer: Domain + Domain.Tests first, strict TDD.

DEFINITION OF DONE
Zero build warnings. All tests green. Schema + seed run at startup (no manual DB setup).
README updated with run instructions and demo credentials.
```

**What the "STOP + ASK" surfaced:** The model asked about password hashing algorithm,
JWT signing strategy, and date storage format — three decisions with real trade-offs.
I answered:

```
1. Use ASP.NET Core PasswordHasher for this specific case
2. Symmetric HS256 with a strong secret via user-secrets
3. DateOnly stored as text (YYYY-MM-DD) in SQLite validated against UTC Now

Architecture note for README — document under "Architecture Decisions & Production Extensions":
- SQLite is used for demo portability. Its single-writer constraint means it does not
  support horizontal scaling with multiple API instances.
- JWT uses HS256 (symmetric). All instances share the secret via environment config,
  sufficient for horizontal scaling of a single service. For multi-service architectures
  where token verification must be distributed, upgrade to RS256 — only the Infrastructure
  JWT implementation changes.
```

Without the stop condition, the model would have made these choices silently,
and I would have found them buried in generated code rather than reviewed up front.

### Representative output — Backend scaffold

**Domain entity** (`TaskManager.Domain/Entities/TaskItem.cs`) — shows the two factory
methods and domain validation the prompt required:

```csharp
public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public TaskItemStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateOnly DueDate { get; private set; }
    public Guid UserId { get; private set; }

    private TaskItem() { }

    // Enforces domain rules — called by use cases for new tasks
    public static TaskItem Create(
        string title, string? description, DateOnly dueDate, Guid userId,
        DateOnly today,
        TaskItemStatus status = TaskItemStatus.Todo,
        TaskPriority priority = TaskPriority.Medium)
    {
        Validate(title, dueDate, today);
        return new TaskItem { Id = Guid.NewGuid(), Title = title, ... };
    }

    // Bypasses due-date rule — called only by repositories when mapping persisted rows
    public static TaskItem Reconstitute(Guid id, string title, ...) => new() { ... };

    private static void Validate(string title, DateOnly dueDate, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(title))       throw new DomainException("Title must not be empty.");
        if (title.Length > 120)                     throw new DomainException("Title must not exceed 120 characters.");
        if (dueDate < today)                        throw new DomainException("Due date must be today or in the future.");
    }
}
```

**Application use case** (`TaskManager.Application/UseCases/CreateTaskUseCase.cs`) — shows
`Result<T>`, `IUnitOfWork`, and `DomainException` catch:

```csharp
public sealed class CreateTaskUseCase(IUnitOfWork uow, IClock clock)
{
    public async Task<Result<TaskItemResponse>> ExecuteAsync(
        TaskItemRequest request, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var task = TaskItem.Create(
                request.Title, request.Description, request.DueDate,
                userId, clock.Today, request.Status, request.Priority);
            await uow.Tasks.CreateAsync(task, ct);
            await uow.CommitAsync(ct);
            return Result<TaskItemResponse>.Ok(task.ToResponse());
        }
        catch (DomainException ex)
        {
            return Result<TaskItemResponse>.Fail(ex.Message);
        }
    }
}
```

**Infrastructure repository** (`TaskManager.Infrastructure/Persistence/TaskRepository.cs`) — raw
ADO.NET with parameterized queries and an allowlist to prevent ORDER BY injection:

```csharp
// Allowlist prevents SQL injection via the sortBy parameter.
private static readonly Dictionary<string, string> SortColumnMap =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"]    = "title",
        ["priority"] = "priority",
        ["status"]   = "status",
        ["dueDate"]  = "due_date",
    };

public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    await using var cmd = connection.CreateCommand();
    cmd.Transaction = getTransaction();
    cmd.CommandText =
        "SELECT id, title, description, status, due_date, user_id, updated_at, priority " +
        "FROM Tasks WHERE id = @id";
    cmd.Parameters.AddWithValue("@id", id.ToString());

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    return await reader.ReadAsync(ct) ? Map(reader) : null;
}
```

---

## Prompt 2 — Angular frontend (Augment Code)

```
ROLE
You are a senior Angular engineer. You write idiomatic Angular that a reviewer
reads without explanation, and you prefer Angular's built-in tools over hand-rolled logic.

CONTEXT
The backend is done: an ASP.NET Core API with JWT auth and CRUD over /api/tasks.
Endpoints (scope tasks to the authenticated user):
- POST /api/auth/register, POST /api/auth/login  -> returns JWT
- GET /api/tasks, GET /api/tasks/{id}
- POST /api/tasks, PUT /api/tasks/{id}, DELETE /api/tasks/{id}
Task shape: { id, title, description, status (Todo|InProgress|Done), dueDate }

BUILD
A responsive Angular SPA implementing full CRUD against this API, plus login/register.

ANGULAR-NATIVE FIRST (non-negotiable)
Before writing any custom logic, ask "does Angular already provide this?" and use it:
- Reactive Forms + built-in Validators (required, maxLength, custom for future-dueDate).
- HttpClient + a typed service layer; a functional HTTP interceptor for the JWT.
- Built-in pipes (DatePipe for dueDate, etc.) instead of manual formatting.
- Route guards (CanActivate) for protected routes; lazy-loaded feature routes.
- Standalone components, signals for local state, async pipe for streams.
  Do NOT manually subscribe/unsubscribe where the async pipe applies.

ARCHITECTURE
- Clean separation: presentational components vs. a tasks data service vs. auth service.
- Strongly typed models matching the API DTOs. No 'any'.
- Centralized error handling (interceptor) with user-facing feedback.

WORKFLOW
1. Print the folder/component/service structure first.
2. Then STOP and ask me up to 3 clarifying questions (state management approach,
   styling lib, Angular version) before writing code.
We build it in slices: auth flow first, then the tasks CRUD feature.

DEFINITION OF DONE
Builds with zero console warnings, responsive, JWT persisted and attached automatically,
protected routes guarded, forms validated with inline errors.
```

### Representative output — Angular frontend

**Functional JWT interceptor** (`core/interceptors/auth.interceptor.ts`) — Angular-native
approach; no class, no `implements HttpInterceptor`, no manual unsubscribe:

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token();
  if (!token) return next(req);
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
```

**Custom validator** (`core/validators/future-date.validator.ts`) — used in Reactive Forms
alongside built-in `Validators.required` and `Validators.maxLength`:

```typescript
export function futureDateValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (!control.value) return null;
    const picked = new Date(control.value);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return picked >= today ? null : { pastDate: true };
  };
}
```

**Signal-based state in the main shell** (`features/tasks/shell/shell.component.ts`) —
all local state is signals or computed values; no explicit subscriptions:

```typescript
// State signals
readonly theme      = signal<'light'|'dark'>('light');
readonly view       = signal<'board'|'list'|'grid'>('board');
readonly tasks      = signal<Task[]>([]);
readonly sortCol    = signal<'title'|'priority'|'status'|'dueDate'|null>(null);
readonly sortDir    = signal<'asc'|'desc'>('asc');

// Derived state — recomputed automatically when signals change
readonly sortedPagedTasks = computed(() => {
  const col = this.sortCol();
  if (!col) return this.pagedTasks();
  const dir = this.sortDir();
  const PRIORITY_ORDER: Record<TaskPriority, number> = { Low:0, Medium:1, High:2 };
  return [...this.pagedTasks()].sort((a, b) => {
    const cmp = col === 'priority'
      ? PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority]
      : a[col].localeCompare(b[col]);
    return dir === 'asc' ? cmp : -cmp;
  });
});
```

---

## How I validated the AI's suggestions

Validation was never "it looked right." Each phase had a concrete gate:

### Gate 1 — Compile-time dependency enforcement

After scaffolding I ran `dotnet build` immediately. The `.csproj` project references
make a wrong-direction dependency a **build error**, not a code review finding.
There were zero warnings or errors after scaffold — I verified the output file by file
before proceeding.

### Gate 2 — Test-first review (TDD as verification)

The model wrote failing tests before any implementation. Before saying "proceed," I read
every test against the acceptance criteria in `docs/user-story.md` and confirmed:

- Does this test cover the acceptance criterion? ✓ / ✗
- Does it test behaviour (what the system does), not implementation (how)? ✓ / ✗
- Would this test catch a regression if the implementation changed? ✓ / ✗

If a test was too shallow (e.g., only checking that a method was called, not the result),
I pushed back before letting the model write the green code.

### Gate 3 — Security review of each use case

For every use case I checked three things manually:

| Check | What I looked for |
|---|---|
| Authorization scope | Does the use case filter by `userId`? A user must never see another user's data. |
| Error information | Does a failure response leak whether the resource exists or belongs to someone else? |
| SQL injection | Are all query parameters bound via `cmd.Parameters.AddWithValue()`? Is there any string interpolation in SQL? |

### Gate 4 — Full test suite after every change

`dotnet test backend/TaskManager.sln` was run after every non-trivial edit.
The test count progression — 21 → 75 → 99 → 120 — acted as a regression gate.
A drop in passing tests meant something broke; I investigated before continuing.

---

## Corrections and improvements I made

### Correction 1 — FK constraint failure after enabling foreign key enforcement

**What the model generated:**
`TaskRepositoryTests` created `TaskItem` records using a random `Guid.NewGuid()` as the
`userId`. This worked initially because SQLite disables FK checks by default.

**What broke:**
When I instructed the model to add `PRAGMA foreign_keys=ON` to `UnitOfWork` (required
for data integrity), 6 tests immediately failed with:
```
SQLite Error 19: 'FOREIGN KEY constraint failed'
```

**Why it broke:**
The tests were inserting tasks before inserting the corresponding user. The model hadn't
considered that enabling FK enforcement would invalidate test data that was previously
implicitly valid.

**How I caught it:** The test runner output made it obvious — 6 failures in one class,
all with the same SQLite error code.

**Fix I directed:** Add a `CreateUserAsync()` helper to `TaskRepositoryTests` that inserts
a real user into the database first, then creates tasks referencing that user's ID.
The tests became self-contained and semantically correct.

**Lesson:** The model correctly added the PRAGMA but didn't reason about the downstream
effect on existing tests. I had to hold that context.

---

### Correction 2 — Wrong MSBuild property for suppressing a transitive NuGet advisory

**What the model generated:**
`<NuGetAuditSuppress>` in `Directory.Build.props` to suppress a NU1903 advisory from
the transitive dependency `SQLitePCLRaw.lib.e_sqlite3` (no fix available upstream).

**Why it was wrong:**
`NuGetAuditSuppress` is applied during restore, not build. It was not being picked up,
and the warning persisted on every build.

**How I caught it:** The warning was still printed after the supposed fix. I investigated
the MSBuild property documentation.

**Fix I directed:** Switch to `<NuGetAuditMode>direct</NuGetAuditMode>`, which limits
the audit to directly-referenced packages only, correctly excluding the transitive
advisory. The warning disappeared immediately.

---

### Correction 3 — TypeScript literal type widening inside an Angular template `@for`

**What the model generated:**
A column-header array defined inline inside the Angular template:

```html
@for (hdr of [
  { col: 'title',    label: 'Task'     },
  { col: 'priority', label: 'Priority' }
]; track hdr.col) {
  <button (click)="cycleSort(hdr.col)">...</button>
}
```

**Why it broke:**
The Angular template compiler widened `'title'` to `string` when inferring the type
of the inline object literal. `cycleSort` expects
`'title' | 'priority' | 'status' | 'dueDate'`, so the build failed:
```
Argument of type 'string' is not assignable to parameter of type
'"title" | "priority" | "status" | "dueDate"'.
```

**How I caught it:** The Angular build error was immediate and descriptive.

**Fix I directed:** Move the array to a typed class property with an explicit annotation:
```typescript
readonly listHeaders: { col: 'title' | 'priority' | 'status' | 'dueDate'; label: string }[] = [...]
```
The template compiler then infers the correct literal union from the class property's
declared type. This is a well-known Angular template type-checking behaviour — the model
generated idiomatic-looking code that was subtly wrong in a typed context.

---

## How I handled edge cases, authentication, and validation

### Email enumeration prevention

The naive implementation returns different errors for "email not found" vs.
"wrong password." An attacker can use this to enumerate valid accounts.

**What I instructed:**
`LoginUseCase` must return the same error string for both cases:
`Result.Fail("Invalid email or password.")` — never two separate messages.
The model's first pass had separate error paths; I caught this in code review and
directed the merge.

### Ownership probing prevention

A user must not be able to determine whether a task ID *exists* if it belongs to
someone else. If `GET /api/tasks/{id}` returns `404` for "not mine" but `403` for
"not found," an attacker learns which IDs are valid.

**What I instructed:**
`GetTaskByIdUseCase` returns `Result.NotFound(...)` for **both** scenarios
("task not found" and "task belongs to another user"). The controller maps both to `404`.
The model initially separated the two cases; I directed the merge during review.

### Validation boundary (domain vs. application layer)

**What the model initially did:** Placed title and due-date validation in the use case.

**Why that's wrong:** Business rules (title ≤ 120 chars, due date ≥ today) are domain
invariants. They belong in `TaskItem.Create(...)` which throws `DomainException`.
The use case catches that exception and converts it to `Result<T>.Fail(...)`.
Controllers never contain validation logic.

**Reconstitute pattern for past due dates:**
When reading from the database, rows may legitimately have past due dates
(they were valid when created). `TaskItem.Reconstitute(...)` is a second factory method
that bypasses due-date validation — it exists precisely for ADO.NET mapping.
This was my explicit instruction; the model would have used a single factory method and
either lost the constraint or broken reads.

### Pagination (never an unbounded list)

The model's initial `GET /api/tasks` implementation loaded all tasks into memory then
sliced. I directed `LIMIT` and `OFFSET` directly in the SQL query, with the count
coming from a separate `SELECT COUNT(*)` — the standard pattern for paginated ADO.NET
queries that does not over-fetch.

---

## What GenAI accelerated vs. what required my judgment

| Accelerated by GenAI | Required my judgment |
|---|---|
| Boilerplate: solution scaffold, .csproj references, 8 projects | Security: email enumeration prevention, ownership probing |
| Pattern consistency: Result\<T\>, IUnitOfWork, Repository applied uniformly | Domain boundary: where validation lives (entity vs. use case vs. controller) |
| SQL: parameterized queries for all CRUD operations | Edge cases: Reconstitute pattern for past due dates in persisted records |
| Angular: functional interceptors, auth guard, reactive forms, signals | Type safety: literal widening bug in Angular template `@for` |
| Design translation: `.dc.html` prototype → typed Angular component | Scope control: redirecting the model away from out-of-scope features |
| Test structure: one test class per use case, one per repository | Test quality: reviewing tests for behavioural coverage, not just coverage % |

---

## Conclusion

The single most important insight from this exercise: **the quality of GenAI output is
directly proportional to the quality of the specification you give it.**

A vague prompt ("build a task manager API") produces vague code — syntactically valid
but architecturally ambiguous, with security gaps the model had no reason to consider.

A precise prompt with an explicit ROLE, hard constraints, a stop condition, and a
definition of done produces code that can be reviewed, tested, and shipped with
confidence — because the model is working from the same spec the reviewer is
evaluating against.

The model handled velocity. I handled correctness.
