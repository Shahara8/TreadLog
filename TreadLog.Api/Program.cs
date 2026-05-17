using TreadLog.Api.Services;
using TreadLog.Api.Services.Interfaces;
using TreadLog.Models;
using TreadLog.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── Database connection ───────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

// ── DI container ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDatabaseService>(_ => new DatabaseService(connStr));
builder.Services.AddSingleton<IWorkoutRepository, WorkoutRepository>();
builder.Services.AddSingleton<IUserSettingsRepository, UserSettingsRepository>();
builder.Services.AddSingleton<DataPortabilityService>();

builder.Services.AddEndpointsApiExplorer();

// CORS — allow Blazor WASM dev origin + any origins configured via AllowedOrigins
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "https://localhost:7001", "http://localhost:5001" };

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyMethod()
     .AllowAnyHeader()));

var app = builder.Build();

// ── DB initialisation ─────────────────────────────────────────────────────────
await app.Services.GetRequiredService<IDatabaseService>().InitializeAsync();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.UseCors();

// ── Workouts endpoints ────────────────────────────────────────────────────────
var workouts = app.MapGroup("/api/workouts");

workouts.MapGet("/", async (IWorkoutRepository repo) =>
    Results.Ok(await repo.GetAllAsync()));

workouts.MapGet("/{id:int}", async (int id, IWorkoutRepository repo) =>
    await repo.GetByIdAsync(id) is { } s ? Results.Ok(s) : Results.NotFound());

workouts.MapGet("/range", async (string start, string end, IWorkoutRepository repo) =>
    Results.Ok(await repo.GetByDateRangeAsync(start, end)));

workouts.MapPost("/", async (WorkoutSession session, IWorkoutRepository repo) =>
{
    var id      = await repo.AddAsync(session);
    var created = await repo.GetByIdAsync(id);
    return Results.Created($"/api/workouts/{id}", created);
});

workouts.MapPut("/{id:int}", async (int id, WorkoutSession session, IWorkoutRepository repo) =>
{
    if (await repo.GetByIdAsync(id) is null)
        return Results.NotFound();
    session.Id = id;
    await repo.UpdateAsync(session);
    return Results.Ok(await repo.GetByIdAsync(id));
});

workouts.MapDelete("/{id:int}", async (int id, IWorkoutRepository repo) =>
{
    await repo.DeleteAsync(id);
    return Results.NoContent();
});

// ── Settings endpoints ────────────────────────────────────────────────────────
var settings = app.MapGroup("/api/settings");

settings.MapGet("/", async (IUserSettingsRepository repo) =>
    Results.Ok(await repo.GetAsync()));

settings.MapPut("/", async (UserSettings body, IUserSettingsRepository repo) =>
{
    await repo.SaveAsync(body);
    return Results.Ok(await repo.GetAsync());
});

// ── Export endpoints ──────────────────────────────────────────────────────────
var export = app.MapGroup("/api/export");

export.MapGet("/csv", async (IWorkoutRepository repo, DataPortabilityService svc) =>
{
    var sessions = await repo.GetAllAsync();
    var ms       = new MemoryStream();
    await svc.ExportToCsvAsync(sessions, ms);
    ms.Position  = 0;
    return Results.File(ms, "text/csv", $"treadlog-{DateTime.UtcNow:yyyyMMdd}.csv");
});

export.MapGet("/json", async (IWorkoutRepository repo, DataPortabilityService svc) =>
{
    var sessions = await repo.GetAllAsync();
    var ms       = new MemoryStream();
    await svc.ExportToJsonAsync(sessions, ms);
    ms.Position  = 0;
    return Results.File(ms, "application/json", $"treadlog-{DateTime.UtcNow:yyyyMMdd}.json");
});

// ── Import endpoint ───────────────────────────────────────────────────────────
app.MapPost("/api/import", async (IFormFile file, DataPortabilityService svc) =>
{
    var ext    = Path.GetExtension(file.FileName);
    await using var stream = file.OpenReadStream();
    var result = await svc.ImportFromStreamAsync(stream, ext);
    return Results.Ok(result);
}).DisableAntiforgery();

app.Run();
