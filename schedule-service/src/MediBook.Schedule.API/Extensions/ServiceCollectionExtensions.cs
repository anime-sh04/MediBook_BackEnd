using System.Text;
using FluentValidation;
using MediBook.Schedule.API.Data;
using MediBook.Schedule.API.Messaging.Consumers;
using MediBook.Schedule.API.Messaging.Infrastructure;
using MediBook.Schedule.API.Helpers;
using MediBook.Schedule.API.Repositories;
using MediBook.Schedule.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MediBook.Schedule.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScheduleServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        // ── Database ──────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("ScheduleDb");
        services.AddDbContext<ScheduleDbContext>(options =>
            options.UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Schedule");
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        // ── Repository + Service ──────────────────────────────────────────────
        services.AddScoped<ISlotRepository, SlotRepository>();
        services.AddScoped<IScheduleService, ScheduleService>();

        // ── FluentValidation ──────────────────────────────────────────────────
        services.AddValidatorsFromAssemblyContaining<Program>();

        // ── JWT ───────────────────────────────────────────────────────────────
        var jwtSettings = new JwtSettings();
        configuration.Bind(JwtSettings.SectionName, jwtSettings);
        services.AddSingleton(jwtSettings);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtSettings.Issuer,
                    ValidAudience            = jwtSettings.Audience,
                    IssuerSigningKey         = new SymmetricSecurityKey(
                                                  Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
                };
            });

        services.AddAuthorization();

        // ── Swagger ───────────────────────────────────────────────────────────
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "MediBook Schedule Service",
                Version     = "v1",
                Description = "Manages provider availability slots: add, bulk-create, block, book, unbook, and generate recurring schedules."
            });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name         = "JWT Authentication",
                Description  = "Enter JWT Bearer token",
                In           = ParameterLocation.Header,
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                Reference    = new OpenApiReference
                {
                    Id   = JwtBearerDefaults.AuthenticationScheme,
                    Type = ReferenceType.SecurityScheme
                }
            };
            c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() }
            });
        });


        // ── RabbitMQ / Saga messaging ─────────────────────────────────────────────
        var rabbitSettings = new RabbitMqSettings();
        configuration.Bind(RabbitMqSettings.SectionName, rabbitSettings);

        if (string.IsNullOrWhiteSpace(rabbitSettings.ConnectionString))
            throw new InvalidOperationException(
                "RabbitMQ:ConnectionString must be configured in appsettings.json.");

        services.AddSingleton(rabbitSettings);
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<SagaEventPublisher>();

        // Background consumer for PaymentSucceeded / PaymentFailed events
        services.AddHostedService<PaymentResultConsumer>();

        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        return services;
    }
}
