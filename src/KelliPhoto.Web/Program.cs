using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KelliPhoto.Web.Configuration;
using KelliPhoto.Web.Data;
using KelliPhoto.Web.Services;
using Serilog;
using System.IO;
using Microsoft.Extensions.Configuration;
using DotNetEnv;

// Load .env before configuration binds (maps Email__SmtpPassword → Email:SmtpPassword).
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (!File.Exists(envPath))
    envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (File.Exists(envPath))
    Env.Load(envPath);

static bool IsMvcIntegrationTestHost() =>
    string.Equals(Environment.GetEnvironmentVariable("KELLIPHOTO_INTEGRATION_TEST"), "1", StringComparison.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<IdentitySettings>(builder.Configuration.GetSection(IdentitySettings.SectionName));

// Configure Serilog
// ReadFrom.Configuration already includes console/file sinks from appsettings
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext();

// Only add file logging in non-test hosts (CI/tests use appsettings.Testing.json — no /app/logs).
var isTestHost = string.Equals(builder.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase)
    || IsMvcIntegrationTestHost();
var logPath = builder.Configuration["Serilog:WriteTo:1:Args:path"];
if (!string.IsNullOrEmpty(logPath) && !isTestHost)
{
    // Ensure log directory exists
    var logDir = Path.GetDirectoryName(logPath);
    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
    {
        Directory.CreateDirectory(logDir);
    }
    loggerConfig.WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
}

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddControllers();

// Configure PostgreSQL and Entity Framework. WebApplicationFactory often still sees Development from
// launchSettings before UseEnvironment("Testing") runs; tests set KELLIPHOTO_INTEGRATION_TEST=1 (see test factory).
var useInMemoryIntegrationTest = string.Equals(
    builder.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase)
    || IsMvcIntegrationTestHost();

if (useInMemoryIntegrationTest)
{
    // Tests can set KELLIPHOTO_INMEMORY_DB before the host builds (configuration merge is not always visible this early).
    var inMemoryName = builder.Configuration["Testing:InMemoryDatabaseName"]
        ?? Environment.GetEnvironmentVariable("KELLIPHOTO_INMEMORY_DB")
        ?? "KelliPhotoTestingDb";
    // One factory + scoped contexts from it so IDbContextFactory and scoped ApplicationDbContext share the same in-memory store.
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase(inMemoryName));
    builder.Services.AddScoped<ApplicationDbContext>(sp =>
        sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is missing. Set ConnectionStrings__DefaultConnection in .env or the environment (see .env.example).");

    Action<DbContextOptionsBuilder> configureDbContext = options =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });

        if (builder.Environment.IsDevelopment())
            options.EnableSensitiveDataLogging();
    };

    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });

        if (builder.Environment.IsDevelopment())
            options.EnableSensitiveDataLogging();
    });

    builder.Services.AddDbContextPool<ApplicationDbContext>(configureDbContext, poolSize: 128);
}

// Configure Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add authentication state provider for Blazor
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(RoleNames.Admin));
});

// Register HttpClient for Blazor components
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Register application services
builder.Services.AddSingleton<IAppVersionService, AppVersionService>();
builder.Services.AddSingleton<IPathService, PathService>();
builder.Services.AddSingleton<IScanProgressService, ScanProgressService>();
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IThumbnailService, ThumbnailService>();
builder.Services.AddScoped<IWebImageService, WebImageService>();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IContactFormService, ContactFormService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddHostedService<CatalogService>();

var app = builder.Build();

// CI/CD and scripts/deploy/apply-migrations.sh: docker run <image> --migrate
if (args.Any(a => string.Equals(a, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    if (useInMemoryIntegrationTest)
    {
        Log.Warning("Skipping migrations: in-memory or test host configuration.");
        return;
    }

    using (var scope = app.Services.CreateScope())
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            Log.Information("No pending database migrations.");
        }
        else
        {
            Log.Information(
                "Applying {Count} pending migration(s): {Names}",
                pending.Count,
                string.Join(", ", pending));
            await db.Database.MigrateAsync();
            Log.Information("Database migrations applied successfully.");
        }
    }

    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// WebApplicationFactory uses HTTP; HTTPS redirection breaks in-memory TestServer clients.
if (!app.Environment.IsEnvironment("Testing") && !IsMvcIntegrationTestHost())
    app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");

// Seed admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        await EnsureRoleExistsAsync(roleManager, RoleNames.Admin);
        await EnsureRoleExistsAsync(roleManager, RoleNames.User);
        
        // Get admin email and password from configuration, or use defaults
        var adminEmail = builder.Configuration["Admin:Email"] ?? "admin@kelliphoto.com";
        var adminPassword = builder.Configuration["Admin:Password"] ?? "Admin123!";
        
        // Check if admin user exists
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, RoleNames.Admin);
                Log.Information("Admin user created with email: {Email}", adminEmail);
            }
            else
            {
                Log.Error("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Ensure admin user is in Admin role
            if (!await userManager.IsInRoleAsync(adminUser, RoleNames.Admin))
            {
                await userManager.AddToRoleAsync(adminUser, RoleNames.Admin);
                Log.Information("Admin role added to existing user: {Email}", adminEmail);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding the admin user.");
    }
}

app.Run();

static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
{
    if (await roleManager.FindByNameAsync(roleName) != null)
        return;

    var result = await roleManager.CreateAsync(new IdentityRole(roleName));
    if (result.Succeeded)
    {
        Log.Information("{Role} role created.", roleName);
        return;
    }

    // Parallel test hosts can race on role creation; treat "already exists" as success.
    if (await roleManager.FindByNameAsync(roleName) != null)
        return;

    Log.Warning(
        "Failed to create role {Role}: {Errors}",
        roleName,
        string.Join(", ", result.Errors.Select(e => e.Description)));
}

// Make Program accessible to WebApplicationFactory for testing
public partial class Program { }
