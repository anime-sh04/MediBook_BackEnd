using MediBook.Appointment.API.Data;
using MediBook.Appointment.API.Extensions;
using MediBook.Appointment.API.Messaging.Infrastructure;
using MediBook.Appointment.API.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppointmentServices(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// 1. Global exception handler — must be first
app.UseMiddleware<GlobalExceptionMiddleware>();

// 2. Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediBook Appointment v1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("AllowAll");


// 3. Auth — order matters
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Auto-apply EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
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

// ── Declare RabbitMQ topology on startup ──────────────────────────────────────
// Creates the exchange and queue.appointment.payment.succeeded queue if they
// don't already exist.  Safe to run multiple times (idempotent).
using (var scope = app.Services.CreateScope())
{
    var logger     = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var connFactory = app.Services.GetRequiredService<RabbitMqConnectionFactory>();
    try
    {
        var connection = connFactory.GetConnection();
        RabbitMqTopology.DeclareAll(connection, logger);
        logger.LogInformation("[Appointment] RabbitMQ topology declared.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[Appointment] Failed to declare RabbitMQ topology on startup.");
        // Do not throw — the consumer's StartConsuming will retry when it starts.
    }
}

app.Run();
