using System.Text;
using FluentValidation;
using MediBook.Notification.API.Data;
using MediBook.Notification.API.Helpers;
using MediBook.Notification.API.Repositories;
using MediBook.Notification.API.Services;
using MediBook.Notification.API.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MediBook.Notification.API.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers PostgreSQL + EF Core DbContext.</summary>
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("Connection string 'NotificationDb' is missing.");

        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount:   5,
                    maxRetryDelay:   TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        });

        return services;
    }

    /// <summary>Registers JWT Bearer authentication middleware.</summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);

        var jwtSettings = jwtSection.Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings section is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
            throw new InvalidOperationException("JwtSettings:SecretKey must be at least 32 characters.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken            = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = signingKey,
                    ValidateIssuer           = true,
                    ValidIssuer              = jwtSettings.Issuer,
                    ValidateAudience         = true,
                    ValidAudience            = jwtSettings.Audience,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.FromSeconds(30),
                };

                // Allow SignalR to pass the JWT via query string (?access_token=...)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments("/hubs/notifications"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode  = 401;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(
                            """{"message":"Authentication required. Provide a valid Bearer token."}""");
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode  = 403;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(
                            """{"message":"You do not have permission to access this resource."}""");
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Registers application services.
    /// Email is sent via MailKit — the .NET equivalent of Node.js Nodemailer.
    /// SMS is intentionally not registered per project requirements.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Email settings
        services.Configure<EmailSettings>(
            configuration.GetSection(EmailSettings.SectionName));

        // Repository
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Email sender (MailKit — .NET Nodemailer equivalent)
        services.AddScoped<IEmailService, MailKitEmailService>();

        // Core notification orchestrator
        services.AddScoped<INotificationService, NotificationService>();

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<SendNotificationRequestValidator>();

        return services;
    }

    /// <summary>Configures Swagger with JWT Bearer auth support.</summary>
    public static IServiceCollection AddSwaggerDocs(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "MediBook Notification Service",
                Version     = "v1",
                Description = "Handles in-app (SignalR), email (MailKit/SMTP) notifications for MediBook. " +
                              "SMS is not supported in this implementation."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Description  = "Enter your JWT token. Example: eyJhbGciO..."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id   = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        return services;
    }
}
