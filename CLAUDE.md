# Task Manager Assessment

Technical take-home. Monorepo: /backend (.NET 10) + /frontend (Angular 18).
Will be code-reviewed by a senior panel.

## Hard constraints (violating any fails the review)
- Backend: NO Entity Framework, NO Dapper, NO MediatR.
- Data access: raw ADO.NET, parameterized queries only.
- Clean Architecture: enforce dependency direction Domain <- Application <- Infrastructure/API.
  Each layer is its own .csproj. Domain has zero external dependencies.
- TDD: write the failing xUnit test first, then minimal code, then refactor.

## Backend conventions
- .NET 10, C#. PascalCase projects: TaskManager.Domain, .Application, .Infrastructure, .Api.
- async/await end-to-end. Passwords hashed, never logged. JWT auth.
- No business logic in controllers.
- Result Pattern: use cases return Result<T> instead of throwing for business rule
  failures. DomainException reserved for unexpected/unrecoverable errors only.
- Unit of Work: use cases receive IUnitOfWork (defined in Application) instead of
  individual repositories. IUnitOfWork owns the transaction scope.
  Infrastructure implementation: UnitOfWork wraps SqliteConnection + SqliteTransaction.

## Frontend conventions
- Angular 18: standalone components, signals for local state, async pipe for streams.
- Smart/dumb component split. Tailwind. Jasmine for tests.
- Angular-native first: before custom logic, ask "does Angular already provide this?"
  Use built-in Validators, pipes (DatePipe), functional interceptors, route guards.
- No manual subscribe/unsubscribe where async pipe applies. No 'any'.

## Commands
- Backend build: dotnet build backend/TaskManager.sln
- Backend test:  dotnet test backend/TaskManager.sln
- Frontend dev:  cd frontend && ng serve
- Frontend test: cd frontend && ng test