using MediBook.Payment.API.Data;
using MediBook.Payment.API.Messaging.Infrastructure;
using MediBook.Payment.API.Extensions;
using MediBook.Payment.API.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ ADD CORS BEFORE BUILD
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});


builder.Services.AddPaymentServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ✅ 1. Exception middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// ✅ 2. CORS MUST BE EARLY

app.UseCors("AllowAll");


// ✅ 3. Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediBook Payment v1");
    c.RoutePrefix = string.Empty;
});

// ❌ REMOVE THIS (IMPORTANT)
// app.UseHttpsRedirection();


// ✅ 4. Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


// ── Declare RabbitMQ exchange + queues (idempotent) ────────────────────────────
{
    var mqLogger = app.Services.GetRequiredService<ILogger<Program>>();
    try
    {
        var connFactory = app.Services.GetRequiredService<RabbitMqConnectionFactory>();
        RabbitMqTopology.DeclareAll(connFactory.GetConnection(), mqLogger);
        mqLogger.LogInformation("RabbitMQ topology declared successfully.");
    }
    catch (Exception ex)
    {
        mqLogger.LogError(ex, "Failed to declare RabbitMQ topology. Saga will be unavailable.");
    }
}

// ── DB migration ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
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