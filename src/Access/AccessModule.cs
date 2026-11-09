using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Access;

internal sealed class LabUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
}

internal sealed class AccessDatabase(DbContextOptions<AccessDatabase> options)
    : IdentityDbContext<LabUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("access");
        model.Entity<LabUser>().Property(x => x.DisplayName).HasMaxLength(150);
    }
}

public static class AccessModule
{
    public static IServiceCollection AddAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AccessDatabase>(options => options.UseNpgsql(configuration.GetConnectionString("Database")));
        services.AddIdentityCore<LabUser>(options =>
        {
            options.Password.RequiredLength = 16;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AccessDatabase>().AddSignInManager();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = "assessment.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Candidate", policy => policy.RequireRole("Candidate"));
            options.AddPolicy("Reviewer", policy => policy.RequireRole("Reviewer"));
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "assessment.csrf";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }

    public static WebApplication MapAccess(this WebApplication app)
    {
        app.MapGet("/api/session", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new
            {
                authenticated = context.User.Identity?.IsAuthenticated == true,
                userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                name = context.User.FindFirstValue("display_name") ?? context.User.Identity?.Name,
                role = context.User.FindFirstValue(ClaimTypes.Role),
                csrfToken = tokens.RequestToken,
                serverTime = DateTimeOffset.UtcNow
            });
        });
        app.MapPost("/api/login", async (LoginRequest request, SignInManager<LabUser> signIn, UserManager<LabUser> users) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 ||
                string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
                return Results.BadRequest(new { error = "Enter your email and password." });
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null)
            {
                // Unknown accounts still pay a password hash cost and receive the same response.
                new PasswordHasher<LabUser>().VerifyHashedPassword(new LabUser(), DummyPasswordHash, request.Password);
                return Results.Json(new { error = "Email or password was not accepted. Try again later if your account is locked." }, statusCode: 401);
            }
            var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.Json(new { error = "Email or password was not accepted. Try again later if your account is locked." }, statusCode: 401);
            await signIn.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("display_name", user.DisplayName)]);
            return Results.Ok(new { authenticated = true });
        }).RequireRateLimiting("login");
        app.MapPost("/api/logout", async (SignInManager<LabUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        return app;
    }

    public static async Task MigrateAccessAsync(this IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccessDatabase>();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2718001)", cancellationToken);
        await database.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS access; CREATE TABLE IF NOT EXISTS access.schema_versions(version integer PRIMARY KEY)", cancellationToken);
        var connection = database.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM access.schema_versions WHERE version=1)";
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken))!)
        {
            await database.Database.ExecuteSqlRawAsync(database.Database.GenerateCreateScript(), cancellationToken);
            await database.Database.ExecuteSqlRawAsync("INSERT INTO access.schema_versions VALUES(1)", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task SeedAccessAsync(this IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var path = configuration["Seed:CredentialsPath"] ?? ".local/credentials.json";
        await using var stream = File.OpenRead(path);
        var seed = await JsonSerializer.DeserializeAsync<SeedCredentials>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            ?? throw new InvalidOperationException("Seed credentials are required.");
        if (seed.Users.Count != 3 || seed.Users.Count(x => x.Role == "Candidate") != 2 || seed.Users.Count(x => x.Role == "Reviewer") != 1 || seed.Users.Select(x => x.Id).Distinct().Count() != 3 ||
            seed.Users.Any(x => x.Role is not ("Candidate" or "Reviewer") || x.Password.Length < 16 || x.Name.Length > 150))
            throw new InvalidOperationException("Seed requires two candidates and one reviewer with private passwords.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<LabUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Candidate", "Reviewer" })
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole<Guid>(role)));
        foreach (var item in seed.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = await users.FindByIdAsync(item.Id.ToString());
            if (existing is not null) continue;
            var user = new LabUser { Id = item.Id, UserName = item.Email, Email = item.Email, DisplayName = item.Name, EmailConfirmed = true };
            Check(await users.CreateAsync(user, item.Password));
            Check(await users.AddToRoleAsync(user, item.Role));
        }
    }

    private static readonly string DummyPasswordHash = new PasswordHasher<LabUser>().HashPassword(new LabUser(), Guid.NewGuid().ToString());
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Identity seed validation failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
    private sealed record LoginRequest(string Email, string Password);
    private sealed record SeedUser(Guid Id, string Email, string Name, string Role, string Password);
    private sealed record SeedCredentials(List<SeedUser> Users);
}
