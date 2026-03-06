using Hangfire;
using Hangfire.MySql;
using ImperaOps.Api;
using ImperaOps.Api.Health;
using ImperaOps.Infrastructure.Jobs;
using System.Text;
using System.Threading.RateLimiting;
using ImperaOps.Application;
using ImperaOps.Infrastructure;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<SessionValidationFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<SessionValidationFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Hangfire ──────────────────────────────────────────────────────────────────

var cs = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Database");

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(cs, new MySqlStorageOptions
    {
        TablesPrefix          = "Hangfire_",
        TransactionTimeout    = TimeSpan.FromMinutes(1),
        QueuePollInterval     = TimeSpan.FromSeconds(15),
    })));

builder.Services.AddHangfireServer(opts =>
{
    opts.WorkerCount  = 4;
    opts.ServerName   = "imperaops-worker";
    opts.Queues       = ["critical", "default", "low"];
});

// ── Health checks ─────────────────────────────────────────────────────────────

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["db"])
    .AddCheck<StorageHealthCheck>("storage",   tags: ["storage"]);

// ── JWT auth ──────────────────────────────────────────────────────────────────

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Missing Jwt:Key");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        // SSE connections can't set headers — accept the token from ?token= for the stream endpoint
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api/v1/notifications/stream")
                    && ctx.Request.Query.TryGetValue("token", out var token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("SuperAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_super_admin", "true"));
});

// ── Rate limiting ─────────────────────────────────────────────────────────────

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy<string>("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    options.AddPolicy<string>("sensitive", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(60),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"api:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
        }
        return RateLimitPartition.GetNoLimiter(string.Empty);
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.NumberFormatInfo.InvariantInfo);
        }
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.", cancellationToken: token);
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("LocalDev");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Hangfire dashboard (local-only in dev, super-admin key in prod) ────────────

var dashboardKey = app.Configuration["Hangfire:DashboardKey"];
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardFilter(dashboardKey, app.Environment.IsDevelopment())],
    DashboardTitle = "ImperaOps Job Queue",
});

// ── Health endpoints ──────────────────────────────────────────────────────────

// ── Run migrations + dev seed on startup ──────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db      = scope.ServiceProvider.GetRequiredService<ImperaOpsDbContext>();
    var logger  = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
    await db.Database.MigrateAsync();
    await storage.EnsureBucketExistsAsync();

    if (app.Environment.IsDevelopment())
    {
        var connStr = app.Configuration.GetConnectionString("Database")!;
        await ImperaOps.Infrastructure.Data.DevSeeder.SeedAsync(db, connStr, logger);
    }
}

// ── Hangfire recurring jobs ───────────────────────────────────────────────────

RecurringJob.AddOrUpdate<TaskReminderJob>(
    "task-due-reminders",
    x => x.RunAsync(CancellationToken.None),
    "0 15 * * *",          // 15:00 UTC = 9:00 AM CST
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

RecurringJob.AddOrUpdate<SlaEscalationJob>(
    "sla-escalation",
    x => x.RunAsync(CancellationToken.None),
    "*/30 * * * *",        // every 30 minutes
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapControllers();

app.Run();
