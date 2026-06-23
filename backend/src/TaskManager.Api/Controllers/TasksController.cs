using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Extensions;
using TaskManager.Application.DTOs;
using TaskManager.Application.UseCases;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class TasksController(
    GetTasksUseCase getTasks,
    GetTaskByIdUseCase getTaskById,
    CreateTaskUseCase createTask,
    UpdateTaskUseCase updateTask,
    DeleteTaskUseCase deleteTask) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new InvalidOperationException("User ID claim not found."));

    // GET /api/tasks?page=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, 50);
        var result = await getTasks.ExecuteAsync(CurrentUserId, page, pageSize, ct);
        if (!result.IsSuccess) return result.ToErrorResult();
        return Ok(result.Value);
    }

    // GET /api/tasks/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await getTaskById.ExecuteAsync(id, CurrentUserId, ct);
        if (!result.IsSuccess) return result.ToErrorResult();
        return Ok(result.Value);
    }

    // POST /api/tasks
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TaskItemRequest request,
        CancellationToken ct)
    {
        var result = await createTask.ExecuteAsync(request, CurrentUserId, ct);
        if (!result.IsSuccess) return result.ToErrorResult();
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // PUT /api/tasks/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] TaskItemRequest request,
        CancellationToken ct)
    {
        var result = await updateTask.ExecuteAsync(id, request, CurrentUserId, ct);
        if (!result.IsSuccess) return result.ToErrorResult();
        return Ok(result.Value);
    }

    // DELETE /api/tasks/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await deleteTask.ExecuteAsync(id, CurrentUserId, ct);
        if (!result.IsSuccess) return result.ToErrorResult();
        return NoContent();
    }
}
