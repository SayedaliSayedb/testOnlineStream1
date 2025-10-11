using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SiteBarnaQ.Services;
using WebApplication1.Hubs;
using WebApplication1.Services;
using WebApplication1.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ValidateModelAttribute>();
    options.Filters.Add<LogActionAttribute>();
}).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.WriteIndented = true;

    });

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024000; // 1MB
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader().WithExposedHeaders("X-Total-Count");
    });
});
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISystemMonitorService, SystemMonitorService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IStreamStateService, StreamStateService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
builder.Services.AddScoped<LogActionAttribute>();
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
});
builder.Services.AddHealthChecks()
    .AddCheck<UserServiceHealthCheck>("user_service")
    .AddCheck<SystemHealthCheck>("system_health")
    .AddCheck<StreamStateHealthCheck>("stream_state");
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseCustomExceptionHandler();
app.UseAuthorization();
app.MapControllerRoute(
    name: "fullscreen",
    pattern: "fullscreen/{action=Index}",
    defaults: new { controller = "FullScreen" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<WebRTCHub>("/webrtcHub");
app.MapHealthChecks("/health");
app.MapGet("/health/detailed", async (ISystemMonitorService monitorService) =>
{
    var resources = await monitorService.GetSystemResourcesAsync();
    return new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        version = "1.0.0",
        resources = new
        {
            resources.CpuUsage,
            resources.MemoryUsage,
            resources.ActiveConnections,
            resources.ActiveStreams,
            uptime = DateTime.UtcNow - resources.MonitorTime
        }
    };
});
app.MapGet("/api/info", () =>
{
    return new
    {
        application = "WebRTC Live Quiz System",
        version = "1.0.0",
        description = "سیستم پخش زنده و مسابقه با قابلیت WebRTC",
        endpoints = new
        {
            auth = "/api/auth",
            admin = "/api/admin",
            quiz = "/api/quiz",
            chat = "/api/chat",
            system = "/api/system",
            stream = "/api/stream",
            hub = "/webrtcHub"
        },
        support = new
        {
            email = "support@webrtclive.com",
            documentation = "/swagger" // اگر Swagger اضافه شود
        }
    };
});

app.Run();


public class UserServiceHealthCheck : IHealthCheck
{
    private readonly IUserService _userService;

    public UserServiceHealthCheck(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // تست سرویس کاربران
            var users = await _userService.GetAllUsersAsync();
            return HealthCheckResult.Healthy($"User service is healthy. Total users: {users.Count}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("User service is unhealthy", ex);
        }
    }
}

public class SystemHealthCheck : IHealthCheck
{
    private readonly ISystemMonitorService _monitorService;

    public SystemHealthCheck(ISystemMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var resources = await _monitorService.GetSystemResourcesAsync();

            var status = resources.CpuUsage < 90 && resources.MemoryUsage < 1024
                ? HealthCheckResult.Healthy($"System is healthy. CPU: {resources.CpuUsage}%, Memory: {resources.MemoryUsage}MB")
                : HealthCheckResult.Degraded($"System is under load. CPU: {resources.CpuUsage}%, Memory: {resources.MemoryUsage}MB");

            return status;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("System health check failed", ex);
        }
    }
}

public class StreamStateHealthCheck : IHealthCheck
{
    private readonly IStreamStateService _streamState;

    public StreamStateHealthCheck(IStreamStateService streamState)
    {
        _streamState = streamState;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var streams = _streamState.GetActiveStreams();
            var users = _streamState.GetConnectedUsers();

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Stream state is healthy. Active streams: {streams.Count}, Connected users: {users.Count}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Stream state health check failed", ex));
        }
    }
}