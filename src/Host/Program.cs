using CandidateAssessment.Access;
using CandidateAssessment.Assessments;
using CandidateAssessment.Evaluation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("ConnectionStrings:Database is required. Run make configure first.");
foreach (var key in new[] { "Kafka:BootstrapServers", "Storage:Root", "Providers:OllamaUrl", "Providers:Model", "Providers:TranscriptionUrl" })
    if (string.IsNullOrWhiteSpace(builder.Configuration[key])) throw new InvalidOperationException($"Configuration {key} is required.");
foreach (var key in new[] { "Providers:OllamaUrl", "Providers:TranscriptionUrl" })
    if (!Uri.TryCreate(builder.Configuration[key], UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
        !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query))
        throw new InvalidOperationException($"Configuration {key} must be an HTTP URL without credentials or query parameters.");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024 * 1024);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
var keyPath = builder.Configuration["DataProtection:Path"] ?? ".local/keys";
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().SetApplicationName("CandidateAssessment").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddAccess(builder.Configuration);
builder.Services.AddAssessments(builder.Configuration);
builder.Services.AddEvaluation(builder.Configuration);

var app = builder.Build();
if (args.Contains("--migrate"))
{
    await app.Services.MigrateAccessAsync();
    await app.Services.MigrateAssessmentsAsync();
    await app.Services.MigrateEvaluationAsync();
    return;
}
if (args.Contains("--seed"))
{
    await app.Services.SeedAccessAsync();
    await app.Services.SeedAssessmentsAsync();
    return;
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "microphone=(self), camera=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; media-src 'self' blob:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (AntiforgeryValidationException)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Your session token expired. Refresh before trying again." });
        }
    }
    catch (BadHttpRequestException)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "The request could not be accepted." });
        }
    }
    catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
    {
        // Provider/database exceptions may embed submitted content or credentials; log only type and trace ID.
        app.Logger.LogError("Request failed ({FailureType}); trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "The service is temporarily unavailable. Your local work is still on this device.", traceId = context.TraceIdentifier });
        }
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    await next();
});
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (CancellationToken token) =>
{
    try
    {
        await using var database = new NpgsqlConnection(connection);
        await database.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT to_regclass('access.\"AspNetUsers\"') IS NOT NULL AND to_regclass('assessments.attempts') IS NOT NULL AND to_regclass('assessments.outbox') IS NOT NULL AND to_regclass('evaluation.jobs') IS NOT NULL AND to_regclass('evaluation.reviews') IS NOT NULL", database);
        var ready = (bool)(await command.ExecuteScalarAsync(token))!;
        return Results.Json(new { status = ready ? "ready" : "migrations required" }, statusCode: ready ? 200 : 503);
    }
    catch (Exception) when (!token.IsCancellationRequested)
    {
        return Results.Json(new { status = "database unavailable" }, statusCode: 503);
    }
});
app.MapAccess();
app.MapAssessments();
app.MapEvaluation();
app.Map("/api/{**path}", () => Results.NotFound(new { error = "Endpoint not found." }));
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
await app.RunAsync();

public partial class Program;
