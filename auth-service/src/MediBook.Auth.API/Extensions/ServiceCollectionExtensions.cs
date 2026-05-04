using System.Text;
using FluentValidation;
using MediBook.Auth.API.Data;
using MediBook.Auth.API.Helpers;
using MediBook.Auth.API.Services;
using MediBook.Auth.API.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MediBook.Auth.API.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers PostgreSQL + EF Core DbContext.</summary>
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("AuthDb")
            ?? throw new InvalidOperationException(
                "Connection string 'AuthDb' is missing.");

        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(
                    maxRetryCount:   5,
                    maxRetryDelay:   TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
            // options.UseLazyLoadingProxies(false);
        });

        return services;
    }

    /// <summary>Registers JWT Bearer authentication middleware.</summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and validate JwtSettings from config
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);

        var jwtSettings = jwtSection.Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings section is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) ||
            jwtSettings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JwtSettings:SecretKey must be at least 32 characters.");
        }

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // true in production
                options.SaveToken            = false;  // don't save token in HttpContext
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = signingKey,
                    ValidateIssuer           = true,
                    ValidIssuer              = jwtSettings.Issuer,
                    ValidateAudience         = true,
                    ValidAudience            = jwtSettings.Audience,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.FromSeconds(30), // 30s tolerance
                };

                // Return clean JSON 401 instead of redirect
                options.Events = new JwtBearerEvents
                {
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

        // Register the JWT generator as a singleton (stateless)
        services.AddSingleton<JwtTokenGenerator>();

        return services;
    }

    /// <summary>Registers application services and validators.</summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        return services;
    }

    /// <summary>
    /// Registers OAuth2 services: OAuthService, OAuthStateStore, and the
    /// named HttpClient used to call provider token/profile endpoints.
    /// Also binds OAuthSettings from configuration.
    /// </summary>
    public static IServiceCollection AddOAuthServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind OAuthSettings (Google + GitHub client credentials)
        services.Configure<OAuthSettings>(
            configuration.GetSection(OAuthSettings.SectionName));

        // Named HttpClient for calling Google / GitHub APIs — 30s timeout
        services.AddHttpClient("oauth", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // State store: singleton so it survives across requests
        // (swap for IDistributedCache-backed implementation in multi-instance deployments)
        services.AddSingleton<OAuthStateStore>();

        // OAuth service: scoped (depends on scoped DbContext)
        services.AddScoped<IOAuthService, OAuthService>();

        return services;
    }

    /// <summary>Configures Swagger with JWT Bearer auth support.</summary>
    public static IServiceCollection AddSwaggerDocs(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "MediBook Auth Service",
                Version     = "v1",
                Description = "Handles registration, login, JWT authentication, and token refresh for MediBook."
            });

            // Add "Authorize" button to Swagger UI for JWT Bearer testing
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
