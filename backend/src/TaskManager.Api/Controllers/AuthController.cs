using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Extensions;
using TaskManager.Application.DTOs;
using TaskManager.Application.UseCases;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    RegisterUserUseCase registerUseCase,
    LoginUseCase loginUseCase) : ControllerBase
{
    /// <summary>
    /// POST /api/auth/register
    /// Registers a new user and returns an auth token on success.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var registerResult = await registerUseCase.ExecuteAsync(request, ct);
        if (!registerResult.IsSuccess)
            return registerResult.ToErrorResult();

        // Registration succeeded — issue a token immediately.
        var loginResult = await loginUseCase.ExecuteAsync(
            new LoginRequest(request.Email, request.Password), ct);

        if (!loginResult.IsSuccess)
            return loginResult.ToErrorResult();

        return StatusCode(StatusCodes.Status201Created, loginResult.Value);
    }

    /// <summary>
    /// POST /api/auth/login
    /// Authenticates a user and returns an auth token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await loginUseCase.ExecuteAsync(request, ct);
        if (!result.IsSuccess)
            return result.ToErrorResult();

        return Ok(result.Value);
    }
}
