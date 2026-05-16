using System.Net.Http.Json;
using System.Net.Http.Headers;
using TreadLog.Models;
using TreadLog.Services.Interfaces;

namespace TreadLog.Web.Services;

/// <summary>
/// Thin HTTP wrapper around the TreadLog.Api endpoints.
/// Registered as a scoped service; the base URL is read from appsettings.json → ApiBaseUrl.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    // ── Workouts ──────────────────────────────────────────────────────────────

    public Task<List<WorkoutSession>?> GetWorkoutsAsync()
        => _http.GetFromJsonAsync<List<WorkoutSession>>("api/workouts");

    public Task<WorkoutSession?> GetWorkoutAsync(int id)
        => _http.GetFromJsonAsync<WorkoutSession>($"api/workouts/{id}");

    public Task<List<WorkoutSession>?> GetWorkoutsByRangeAsync(string start, string end)
        => _http.GetFromJsonAsync<List<WorkoutSession>>(
               $"api/workouts/range?start={Uri.EscapeDataString(start)}&end={Uri.EscapeDataString(end)}");

    public async Task<WorkoutSession?> AddWorkoutAsync(WorkoutSession session)
    {
        var response = await _http.PostAsJsonAsync("api/workouts", session);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkoutSession>();
    }

    public async Task<WorkoutSession?> UpdateWorkoutAsync(int id, WorkoutSession session)
    {
        var response = await _http.PutAsJsonAsync($"api/workouts/{id}", session);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkoutSession>();
    }

    public async Task DeleteWorkoutAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/workouts/{id}");
        response.EnsureSuccessStatusCode();
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public Task<UserSettings?> GetSettingsAsync()
        => _http.GetFromJsonAsync<UserSettings>("api/settings");

    public async Task<UserSettings?> SaveSettingsAsync(UserSettings settings)
    {
        var response = await _http.PutAsJsonAsync("api/settings", settings);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserSettings>();
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task<byte[]> ExportCsvAsync()
    {
        var response = await _http.GetAsync("api/export/csv");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> ExportJsonAsync()
    {
        var response = await _http.GetAsync("api/export/json");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult?> ImportFileAsync(byte[] fileBytes, string fileName)
    {
        using var content     = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync("api/import", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportResult>();
    }
}
