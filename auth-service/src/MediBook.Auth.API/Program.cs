using MediBook.Auth.API.Extensions;
using MediBook.Auth.API.Middleware;
using Microsoft.EntityFrameworkCore;
using MediBook.Auth.API.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddOAuthServices(builder.Configuration);   // ← Google + GitHub OAuth
builder.Services.AddSwaggerDocs();
builder.Services.AddControllers();

builder.Services.Configure<RouteOptions>(options =>
    options.LowercaseUrls = true);

// ── App / Middleware Pipeline ──────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
var app = builder.Build();

app.UseCors("AllowAll");

// 1. Global exception handler — must be first
app.UseMiddleware<GlobalExceptionMiddleware>();

// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediBook Auth v1");
    c.RoutePrefix = string.Empty; // Swagger at http://localhost:5000
});
// }

// app.UseHttpsRedirection();

// 2. Authentication + Authorization — order matters: authn before authz
app.UseAuthentication();    // ← UC-2: validates JWT Bearer tokens
app.UseAuthorization();     // ← UC-2: enforces [Authorize] attributes

app.MapControllers();

// ── Auto-apply EF migrations on startup ───────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        if (db.Database.GetPendingMigrations().Any())
        {
            logger.LogInformation("Applying pending migrations...");
            db.Database.Migrate();
            logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while applying migrations.");
        throw;
    }
}

app.Run();

public partial class Program { }
