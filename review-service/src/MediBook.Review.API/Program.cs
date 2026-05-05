using MediBook.Review.API.Data;
using MediBook.Review.API.Extensions;
using MediBook.Review.API.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReviewServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();

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

// 2. Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediBook Review v1");
    c.RoutePrefix = string.Empty;
});

// app.UseHttpsRedirection();

// 3. Auth — order matters
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Auto-apply EF migrations on startup ───────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
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
