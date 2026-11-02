using Microsoft.OpenApi;

namespace OnlineLearningPlatform.Api.Documentation;

public static class SwaggerExtensions
{
    public const string BearerSchemeName = "Bearer";

    /// <summary>
    /// Registers the OpenAPI document and the "Authorize" button for JWT bearer tokens.
    /// Whether the Swagger endpoints are actually served is decided in Program.cs by the "Swagger:Enabled" setting.
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "OnlineLearningPlatform API",
                Version = "v1",
                Description = "Backend for the OnlineLearningPlatform. Protected endpoints need a JWT access token: " +
                              "click Authorize and paste the token (without the \"Bearer \" prefix).",
            });

            options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token. Access tokens are short-lived (15 minutes or less).",
            });

            // Swashbuckle 10 / Microsoft.OpenApi 2: a requirement is built from the document it belongs to.
            // This marks every operation as accepting the token; endpoints that are anonymous simply ignore it.
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = [],
            });
        });

        return services;
    }
}
