using MediBook.Schedule.API.Data;
using MediBook.Schedule.API.Messaging.Infrastructure;
using MediBook.Schedule.API.Extensions;
using MediBook.Schedule.API.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScheduleServices(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// 1. Global exception handler — must be first
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AllowAll");
// 2. Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediBook Schedule v1");
    c.RoutePrefix = string.Empty;
});

// app.UseHttpsRedirection();

// 3. Auth — order matters
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Declare RabbitMQ exchange + queues (idempotent — safe to run every start) ──
using (var mqScope = app.Services.CreateScope())
{
    var mqLogger = mqScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var connFactory = app.Services.GetRequiredService<RabbitMqConnectionFactory>();
        RabbitMqTopology.DeclareAll(connFactory.GetConnection(), mqLogger);
        mqLogger.LogInformation("RabbitMQ topology declared successfully.");
    }
    catch (Exception ex)
    {
        // Log but don't crash — the app can still serve non-Saga endpoints
        mqLogger.LogError(ex, "Failed to declare RabbitMQ topology. Saga will be unavailable.");
    }
}

// ── Auto-apply EF migrations on startup ───────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
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
