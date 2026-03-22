using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KelliPhoto.Web.Data;
using KelliPhoto.Web.Services;
using Serilog;
using System.IO;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
// ReadFrom.Configuration already includes console/file sinks from appsettings
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext();

// Only add file logging if configured (production) or if log path exists in config
var logPath = builder.Configuration["Serilog:WriteTo:1:Args:path"];
if (!string.IsNullOrEmpty(logPath))
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

// Configure PostgreSQL and Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configure DbContext options action (shared configuration)
Action<DbContextOptionsBuilder> configureDbContext = options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Enable retry on failure for transient errors
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
    
    // Enable sensitive data logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
    
    // SQL query logging disabled to reduce log clutter
    // Uncomment below to enable SQL logging for debugging:
    // options.LogTo(message => 
    // {
    //     if (message.Contains("Executing") || message.Contains("Executed") || message.Contains("SELECT") || message.Contains("FROM"))
    //     {
    //         Log.Information("SQL: {Message}", message);
    //     }
    // }, 
    // Microsoft.Extensions.Logging.LogLevel.Information);
};

// Register DbContextFactory for thread-safe operations
// The factory is singleton and creates its own DbContext instances on demand
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
    {
        options.EnableSensitiveDataLogging();
    }
    
    // SQL query logging disabled to reduce log clutter
    // Uncomment below to enable SQL logging for debugging:
    // options.LogTo(message => 
    // {
    //     if (message.Contains("Executing") || message.Contains("Executed") || message.Contains("SELECT") || message.Contains("FROM"))
    //     {
    //         Log.Information("SQL: {Message}", message);
    //     }
    // }, 
    // Microsoft.Extensions.Logging.LogLevel.Information);
});

// Register DbContextPool for Identity (uses pooling, more efficient than AddDbContext)
// This also provides a scoped DbContext that Identity can use
builder.Services.AddDbContextPool<ApplicationDbContext>(configureDbContext, poolSize: 128);

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
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Register HttpClient for Blazor components
builder.Services.AddHttpClient();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Register application services
builder.Services.AddSingleton<IPathService, PathService>();
builder.Services.AddSingleton<IScanProgressService, ScanProgressService>();
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IThumbnailService, ThumbnailService>();
builder.Services.AddScoped<IWebImageService, WebImageService>();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<CatalogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

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
        
        // Create Admin role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            Log.Information("Admin role created.");
        }
        
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
                await userManager.AddToRoleAsync(adminUser, "Admin");
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
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
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

// Make Program accessible to WebApplicationFactory for testing
public partial class Program { }
