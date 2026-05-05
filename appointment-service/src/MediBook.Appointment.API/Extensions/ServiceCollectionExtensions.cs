using System.Text;
using FluentValidation;
using MediBook.Appointment.API.Data;
using MediBook.Appointment.API.Helpers;
using MediBook.Appointment.API.Messaging.Consumers;
using MediBook.Appointment.API.Messaging.Infrastructure;
using MediBook.Appointment.API.Repositories;
using MediBook.Appointment.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MediBook.Appointment.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppointmentServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        // ── Database ──────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("AppointmentDb");
        services.AddDbContext<AppointmentDbContext>(options =>
            options.UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Appointment");
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        // ── Repository + Service ──────────────────────────────────────────────
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAppointmentService, AppointmentService>();

        // ── Payment stub ──────────────────────────────────────────────────────
        services.AddScoped<IPaymentClient, PaymentClientStub>();

        // ── Typed HTTP client for Schedule Service ────────────────────────────
        // Used only for: GetSlotAsync (reschedule validation), UnbookSlotAsync (cancellation).
        // BookSlotAsync has been removed — slot booking is now event-driven.
        var scheduleBaseUrl = configuration["ServiceClients:ScheduleServiceBaseUrl"]
            ?? throw new InvalidOperationException(
                "ServiceClients:ScheduleServiceBaseUrl is not configured.");

        services.AddHttpClient<IScheduleClient, ScheduleClient>(client =>
        {
            client.BaseAddress = new Uri(scheduleBaseUrl.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // ── RabbitMQ ──────────────────────────────────────────────────────────
        var rabbitSettings = new RabbitMqSettings();
        configuration.Bind(RabbitMqSettings.SectionName, rabbitSettings);
        services.AddSingleton(rabbitSettings);
        services.AddSingleton<RabbitMqConnectionFactory>();

        // Background service: listens for PaymentSucceeded → creates appointment
        services.AddHostedService<PaymentSucceededConsumer>();

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
                Title       = "MediBook Appointment Service",
                Version     = "v1",
                Description = "Saga participant: creates appointment records on PaymentSucceeded events. " +
                              "Handles cancellation, rescheduling, and completion lifecycle transitions."
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

        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        return services;
    }
}
