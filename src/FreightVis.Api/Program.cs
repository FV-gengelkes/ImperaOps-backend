using System.Text;
using FreightVis.Application;
using FreightVis.Infrastructure;
using FreightVis.Infrastructure.Data;
using FreightVis.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT authentication (middleware registered, but [Authorize] not yet applied to existing endpoints)
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
    });
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("SuperAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_super_admin", "true"));
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

app.UseAuthentication();
app.UseAuthorization();

// Run migrations + dev seed on startup
using (var scope = app.Services.CreateScope())
{
    var db      = scope.ServiceProvider.GetRequiredService<FreightVisDbContext>();
    var logger  = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
    await db.Database.MigrateAsync();
    await storage.EnsureBucketExistsAsync();

    if (app.Environment.IsDevelopment())
        await FreightVis.Infrastructure.Data.DevSeeder.SeedAsync(db, logger);
}

app.MapControllers();

app.Run();
