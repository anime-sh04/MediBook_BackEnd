using System.Text;
using FluentValidation;
using MediBook.Payment.API.Data;
using MediBook.Payment.API.Messaging.Consumers;
using MediBook.Payment.API.Messaging.Infrastructure;
using MediBook.Payment.API.Helpers;
using MediBook.Payment.API.Repositories;
using MediBook.Payment.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MediBook.Payment.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        // ── Database ──────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("PaymentDb");
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Payment");
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        // ── Repository + Service ──────────────────────────────────────────────
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddHttpClient();

        // ── Razorpay settings ─────────────────────────────────────────────────
        var razorpaySettings = new RazorpaySettings();
        configuration.Bind(RazorpaySettings.SectionName, razorpaySettings);

        if (string.IsNullOrWhiteSpace(razorpaySettings.KeyId) ||
            string.IsNullOrWhiteSpace(razorpaySettings.KeySecret))
            throw new InvalidOperationException(
                "Razorpay:KeyId and Razorpay:KeySecret must be configured.");

        services.AddSingleton(razorpaySettings);

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
                Title       = "MediBook Payment Service",
                Version     = "v1",
                Description = "Handles appointment payment initiation (Razorpay), payment confirmation " +
                              "with signature verification, invoice generation, and provider revenue reporting."
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

        // Background consumer for PaymentRequested events
        services.AddHostedService<PaymentRequestedConsumer>();

        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        return services;
    }
}
