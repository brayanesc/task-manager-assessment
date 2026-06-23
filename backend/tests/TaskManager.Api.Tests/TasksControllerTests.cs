using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskManager.Api.Tests;

public sealed class TasksControllerTests(TaskApiFactory factory)
    : IClassFixture<TaskApiFactory>
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateClient() => factory.CreateClient();

    private async Task<string> RegisterAndGetTokenAsync(HttpClient client)
    {
        var email = $"u_{Guid.NewGuid():N}@test.com";
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "Password1!" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthBody>()
                   ?? throw new InvalidOperationException("No body");
        return body.Token;
    }

    private static HttpClient WithToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object ValidTask(int daysAhead = 7) => new
    {
        title       = "Test task",
        description = "desc",
        status      = "Todo",
        dueDate     = DateTime.UtcNow.AddDays(daysAhead).ToString("yyyy-MM-dd")
    };

    // ── GET /api/tasks — no token → 401 ──────────────────────────────────────

    [Fact]
    public async Task GetTasks_NoToken_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST /api/tasks — happy path → 201 + Location ────────────────────────

    [Fact]
    public async Task CreateTask_ValidPayload_Returns201WithLocationHeader()
    {
        var client = CreateClient();
        WithToken(client, await RegisterAndGetTokenAsync(client));

        var response = await client.PostAsJsonAsync("/api/tasks", ValidTask());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    // ── POST /api/tasks — missing title → 422 ────────────────────────────────

    [Fact]
    public async Task CreateTask_MissingTitle_Returns422()
    {
        var client = CreateClient();
        var token  = await RegisterAndGetTokenAsync(client);
        WithToken(client, token);

        var response = await client.PostAsJsonAsync("/api/tasks", new
        {
            title       = "",
            description = "desc",
            status      = "Todo",
            dueDate     = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── POST /api/tasks — past due date → 422 ────────────────────────────────

    [Fact]
    public async Task CreateTask_PastDueDate_Returns422()
    {
        var client = CreateClient();
        var token  = await RegisterAndGetTokenAsync(client);
        WithToken(client, token);

        var response = await client.PostAsJsonAsync("/api/tasks", new
        {
            title       = "Old task",
            description = "",
            status      = "Todo",
            dueDate     = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── GET /api/tasks — returns paged list scoped to user ───────────────────

    [Fact]
    public async Task GetTasks_ReturnsOnlyAuthenticatedUsersTasks()
    {
        var client = CreateClient();
        var token  = await RegisterAndGetTokenAsync(client);
        WithToken(client, token);

        await client.PostAsJsonAsync("/api/tasks", ValidTask());
        await client.PostAsJsonAsync("/api/tasks", ValidTask(8));

        var response = await client.GetAsync("/api/tasks?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedBody>(JsonOpts);
        Assert.NotNull(paged);
        Assert.True(paged.TotalCount >= 2);
    }

    // ── GET /api/tasks/{id} — owned task → 200 ───────────────────────────────

    [Fact]
    public async Task GetTaskById_OwnedTask_Returns200()
    {
        var client = CreateClient();
        var token  = await RegisterAndGetTokenAsync(client);
        WithToken(client, token);

        var created  = await (await client.PostAsJsonAsync("/api/tasks", ValidTask())).Content
            .ReadFromJsonAsync<TaskBody>(JsonOpts);

        var response = await client.GetAsync($"/api/tasks/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GET /api/tasks/{id} — other user's task → 403 ────────────────────────

    [Fact]
    public async Task GetTaskById_OtherUsersTask_Returns403()
    {
        var ownerClient = CreateClient();
        WithToken(ownerClient, await RegisterAndGetTokenAsync(ownerClient));
        var created = await (await ownerClient.PostAsJsonAsync("/api/tasks", ValidTask())).Content
            .ReadFromJsonAsync<TaskBody>(JsonOpts);

        var otherClient = CreateClient();
        WithToken(otherClient, await RegisterAndGetTokenAsync(otherClient));
        var response = await otherClient.GetAsync($"/api/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GET /api/tasks/{id} — non-existent → 404 ─────────────────────────────

    [Fact]
    public async Task GetTaskById_NonExistentId_Returns404()
    {
        var client = CreateClient();
        WithToken(client, await RegisterAndGetTokenAsync(client));

        var response = await client.GetAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /api/tasks/{id} — updates task → 200 ─────────────────────────────

    [Fact]
    public async Task UpdateTask_OwnedTask_Returns200WithUpdatedData()
    {
        var client = CreateClient();
        WithToken(client, await RegisterAndGetTokenAsync(client));

        var created = await (await client.PostAsJsonAsync("/api/tasks", ValidTask())).Content
            .ReadFromJsonAsync<TaskBody>(JsonOpts);

        var updatePayload = new
        {
            title       = "Updated title",
            description = "updated",
            status      = "InProgress",
            dueDate     = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd")
        };
        var response = await client.PutAsJsonAsync($"/api/tasks/{created!.Id}", updatePayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TaskBody>(JsonOpts);
        Assert.Equal("Updated title", updated!.Title);
    }

    // ── PUT /api/tasks/{id} — other user's task → 403 ────────────────────────

    [Fact]
    public async Task UpdateTask_OtherUsersTask_Returns403()
    {
        var ownerClient = CreateClient();
        WithToken(ownerClient, await RegisterAndGetTokenAsync(ownerClient));
        var created = await (await ownerClient.PostAsJsonAsync("/api/tasks", ValidTask())).Content
            .ReadFromJsonAsync<TaskBody>(JsonOpts);

        var otherClient = CreateClient();
        WithToken(otherClient, await RegisterAndGetTokenAsync(otherClient));

        var response = await otherClient.PutAsJsonAsync($"/api/tasks/{created!.Id}", new
        {
            title = "Hijacked", description = "", status = "Todo",
            dueDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── DELETE /api/tasks/{id} — removes task → 204 ──────────────────────────

    [Fact]
    public async Task DeleteTask_OwnedTask_Returns204()
    {
        var client = CreateClient();
        WithToken(client, await RegisterAndGetTokenAsync(client));

        var created = await (await client.PostAsJsonAsync("/api/tasks", ValidTask())).Content
            .ReadFromJsonAsync<TaskBody>(JsonOpts);

        var response = await client.DeleteAsync($"/api/tasks/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── DELETE /api/tasks/{id} — other user's task → 403 ────────────────────

    [Fact]
    public async Task DeleteTask_OtherUsersTask_Returns403()
    {
        var ownerClient = CreateClient();
        WithToken(ownerClient, await RegisterAndGetTokenAsync(ownerClient));
        var created = await (await ownerClient.PostAsJsonAsync("/api/tasks", ValidTask())).Content
            .ReadFromJsonAsync<TaskBody>(JsonOpts);

        var otherClient = CreateClient();
        WithToken(otherClient, await RegisterAndGetTokenAsync(otherClient));

        var response = await otherClient.DeleteAsync($"/api/tasks/{created!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record AuthBody(string Token, string Email);
    private sealed record TaskBody(Guid Id, string Title, string Description,
        string Status, string DueDate, Guid UserId);
    private sealed record PagedBody(List<TaskBody> Items, int Page, int PageSize, int TotalCount);
}
