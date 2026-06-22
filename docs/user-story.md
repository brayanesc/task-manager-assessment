# User Story — Personal Task Manager

## Story

**As a** registered user,
**I want** to create, view, update and delete my personal tasks,
**so that** I can keep track of my work in one place.

Each task has a **title**, a **description**, a **status** (`Todo` / `InProgress` / `Done`)
and a **due date**.

## Scope notes

- Tasks are **private to each user**: a user can only see and modify their own tasks.
- The system supports **user registration and login**; protected endpoints require a valid JWT.
- Public (anonymous) endpoints exist alongside protected ones.

## Acceptance Criteria

> These double as the TDD test cases — each one maps to at least one unit/integration test.

### Authentication

- **Given** a new user, **when** they register with a unique email and a valid password,
  **then** the account is created and the password is stored hashed (never in plaintext).
- **Given** valid credentials, **when** the user logs in, **then** they receive a JWT.
- **Given** no valid token, **when** a protected task endpoint is called,
  **then** the response is `401 Unauthorized`.

### Create

- **Given** an authenticated user, **when** they create a task with a non-empty title
  (≤ 120 chars) and a due date that is today or in the future,
  **then** the task is saved and returned with `201 Created` and a `Location` header.
- **Given** an empty or too-long title, **when** they try to create a task,
  **then** the response is `400 Bad Request` with a validation message.
- **Given** a due date in the past, **when** they try to create a task,
  **then** the response is `400 Bad Request`.

### Read

- **Given** an authenticated user with tasks, **when** they list their tasks,
  **then** the results are **paginated** (`page`, `pageSize`) and **scoped to that user only**.
- **Given** a task that belongs to another user, **when** the user requests it by id,
  **then** they cannot retrieve it (`404 Not Found`).

### Update

- **Given** an authenticated user owning a task, **when** they update its fields with valid data,
  **then** the changes are persisted and returned.
- **Given** a task owned by another user, **when** the user tries to update it,
  **then** the operation is rejected.

### Delete

- **Given** an authenticated user owning a task, **when** they delete it,
  **then** the response is `204 No Content` and the task no longer exists.
- **Given** a non-existent task id, **when** they try to delete it,
  **then** the response is `404 Not Found`.