using System.Text;
using FluentValidation;
using MediBook.Review.API.Data;
using MediBook.Review.API.Helpers;
using MediBook.Review.API.Repositories;
using MediBook.Review.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MediBook.Review.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReviewServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        // ── Database ──────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("ReviewDb");
        services.AddDbContext<ReviewDbContext>(options =>
            options.UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Review");
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        // ── Repository + Service ──────────────────────────────────────────────
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReviewService, ReviewService>();

        // ── Typed HTTP client for Provider-Service ────────────────────────────
        var providerBaseUrl = configuration["ServiceClients:ProviderServiceBaseUrl"]
            ?? throw new InvalidOperationException(
                "ServiceClients:ProviderServiceBaseUrl is not configured.");

        services.AddHttpClient<IProviderClient, ProviderClient>(client =>
        {
            client.BaseAddress = new Uri(providerBaseUrl.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

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
                Title       = "MediBook Review Service",
                Version     = "v1",
                Description = "Manages patient reviews and star ratings for healthcare providers. " +
                              "Enforces one-review-per-appointment, computes provider average ratings, " +
                              "and supports admin moderation."
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
