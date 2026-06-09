using HMC.Server.Data;
using HMC.Server.Hubs;
using HMC.Server.Middleware;
using HMC.Server.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ===== Serilog =====
var logDir = builder.Environment.IsDevelopment()
    ? Path.Combine(Directory.GetCurrentDirectory(), "logs")
    : "/var/log/hmc";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logDir, "server-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ===== Services =====
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
    {
        options.MaximumReceiveMessageSize = 512 * 1024; // 512KB for large snapshots
    })
    .AddJsonProtocol();

// EF Core + SQLite
var dbPath = builder.Configuration.GetValue("Database:Path",
    Path.Combine(Directory.GetCurrentDirectory(), "data", "hmc.db"));
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
    Directory.CreateDirectory(dbDir);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Application services
builder.Services.AddHostedService<DiscoveryService>();
builder.Services.AddSingleton<DeviceManager>();
builder.Services.AddSingleton<MetricsStoreService>();
builder.Services.AddSingleton<ServerIperf3Service>();
builder.Services.AddSingleton<NetworkTestOrchestrator>();

// CORS (for development; in production, Nginx handles this)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

// ===== Middleware =====
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();

// ===== Ensure DB =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// ===== Routes =====
app.MapControllers();
app.MapHub<AgentHub>("/hub/agent");

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

// ===== Serve Frontend (if present in wwwroot) =====
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(wwwroot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // SPA fallback: non-API routes → index.html
    app.MapFallbackToFile("index.html");

    Log.Information("Serving frontend from {Wwwroot}", wwwroot);
}

Log.Information("HMC Server starting, DB={DbPath}", dbPath);
app.Run();
