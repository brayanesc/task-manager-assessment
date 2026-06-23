using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using TaskManager.Application.Interfaces;
using TaskManager.Application.UseCases;
using TaskManager.Domain.Exceptions;
using TaskManager.Infrastructure.Auth;
using TaskManager.Infrastructure.Clock;
using TaskManager.Infrastructure.Persistence;
#pragma warning disable ASP0027

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Application use cases (Scoped) ───────────────────────────────────────────
builder.Services.AddScoped<RegisterUserUseCase>();
builder.Services.AddScoped<LoginUseCase>();
builder.Services.AddScoped<CreateTaskUseCase>();
builder.Services.AddScoped<GetTasksUseCase>();
builder.Services.AddScoped<GetTaskByIdUseCase>();
builder.Services.AddScoped<UpdateTaskUseCase>();
builder.Services.AddScoped<DeleteTaskUseCase>();

// ── Infrastructure implementations ───────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
             ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
    return new UnitOfWork(cs);
});
builder.Services.AddScoped<IPasswordHasher, PasswordHasherService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IClock, UtcClock>();

// ── DatabaseInitializer ───────────────────────────────────────────────────────
builder.Services.AddSingleton(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
             ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
    return new DatabaseInitializer(cs);
});

// ── JWT authentication ────────────────────────────────────────────────────────
// Options are resolved lazily from IConfiguration (populated after all sources,
// including test overrides via WebApplicationFactory, have been added) so that
// integration tests that swap Jwt:Key via ConfigureAppConfiguration always get
// a matching signing key for both generation and validation.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((opts, config) =>
    {
        var key = config["Jwt:Key"]
                  ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = config["Jwt:Issuer"],
            ValidAudience            = config["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });
builder.Services.AddAuthorization();

// ── OpenAPI + Scalar UI ───────────────────────────────────────────────────────
builder.Services.AddTransient<BearerSecuritySchemeTransformer>();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

// ── Global exception handler (must be first) ──────────────────────────────────
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;

        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var message = feature?.Error is DomainException
            ? feature.Error.Message
            : "An unexpected error occurred.";

        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.MapOpenApi();
app.MapScalarApiReference(opts =>
{
    opts.Title = "Task Manager API";
    opts.AddPreferredSecuritySchemes("Bearer");
});

app.UseCors("AllowAll");
// Skip HTTPS redirect in Production: nginx (or another reverse proxy) handles
// TLS termination externally. Redirecting inside the container would send every
// proxied HTTP request into an infinite redirect loop.
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Database initialisation ───────────────────────────────────────────────────
await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

app.Run();

// Expose Program to WebApplicationFactory in integration tests
public partial class Program { }

/// <summary>
/// Injects the HTTP Bearer security scheme into the generated OpenAPI document
/// so that Scalar UI can display an "Authorize" button for JWT tokens.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(
    Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme))
            return;

        var bearerScheme = new OpenApiSecurityScheme
        {
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            In           = ParameterLocation.Header,
            Description  = "Enter your JWT token (without the 'Bearer ' prefix)."
        };

        // Register the scheme into the document's component workspace so that
        // references resolve correctly when the document is serialised.
        document.AddComponent("Bearer", bearerScheme);

        // Apply globally to every operation using the v2 reference type.
        var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document);
        var requirement = new OpenApiSecurityRequirement { [schemeRef] = [] };

        foreach (var path in document.Paths.Values)
            foreach (var op in path.Operations.Values.Where(o => o is not null))
            {
                op.Security ??= [];
                op.Security.Add(requirement);
            }
    }
}
