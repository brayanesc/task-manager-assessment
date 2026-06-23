using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Api.Tests;

public sealed class AuthControllerTests(TaskApiFactory factory)
    : IClassFixture<TaskApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Register: happy path ──────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidPayload_Returns201WithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email    = $"user_{Guid.NewGuid():N}@example.com",
            password = "Password1!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthBody>();
        Assert.NotNull(body?.Token);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
    }

    // ── Register: duplicate email ─────────────────────────────────────────────

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var payload = new { email = $"dup_{Guid.NewGuid():N}@example.com", password = "Password1!" };
        await _client.PostAsJsonAsync("/api/auth/register", payload);

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Register: validation failures ────────────────────────────────────────

    [Theory]
    [InlineData("not-an-email", "Password1!")]   // bad email
    [InlineData("valid@example.com", "")]         // missing password
    [InlineData("valid@example.com", "short")]    // password too short
    [InlineData("", "Password1!")]                // missing email
    public async Task Register_InvalidPayload_Returns422(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── Login: happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task Login_CorrectCredentials_Returns200WithToken()
    {
        var email = $"login_{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password1!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthBody>();
        Assert.NotNull(body?.Token);
    }

    // ── Login: wrong password ────────────────────────────────────────────────

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = $"wp_{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password1!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Login: unknown email ─────────────────────────────────────────────────

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "nobody@nowhere.com",
            password = "Password1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Internal DTO ─────────────────────────────────────────────────────────
    private sealed record AuthBody(string Token, string Email);
}
