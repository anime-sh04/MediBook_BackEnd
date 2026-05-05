using System.Text;
using FluentValidation;
using MediBook.Provider.API.Data;
using MediBook.Provider.API.Helpers;
using MediBook.Provider.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

namespace MediBook.Provider.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProviderServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        var connectionString = configuration.GetConnectionString("ProviderDb");
        services.AddDbContext<ProviderDbContext>(options =>
            options.UseNpgsql(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Provider");
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        services.AddScoped<IProviderService, ProviderService>();
        services.AddValidatorsFromAssemblyContaining<Program>();

        // ── Redis ──────────────────────────────────────────────────────────────
        var redisConnectionString = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<RedisCacheService>();
        // ──────────────────────────────────────────────────────────────────────

        // JWT Configuration
        var jwtSettings = new JwtSettings();
        configuration.Bind(JwtSettings.SectionName, jwtSettings);
        services.AddSingleton(jwtSettings);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
                };
            });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "MediBook Provider Service",
                Version     = "v1",
                Description = "Manages provider profiles, search, verification, availability, and rating aggregation."
            });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name         = "JWT Authentication",
                Description  = "Enter JWT Bearer token",
                In           = ParameterLocation.Header,
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
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
        services.AddAuthorization();

        return services;
    }
}
