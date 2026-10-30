using Microsoft.EntityFrameworkCore;
using AccessRequestHub.Api.Data;
using AccessRequestHub.Api.Middleware;
using AccessRequestHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Database connection (SQLite)
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection") 
             ?? "Data Source=access_hub.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(dbPath);
});

builder.Services.AddScoped<AccessRequestService>();

// CORS policy for Frontend (Vite)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowViteFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Auto-migrate and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Middleware Pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors("AllowViteFrontend");
app.UseMiddleware<SimulatedAuthMiddleware>();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

app.Run();

// Required for WebApplicationFactory<Program> in xUnit Integration Tests
public partial class Program { }
