using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TicketingSystem.API.OpenApi
{
    // Tells the OpenAPI document that the API uses bearer tokens,
    // which is what makes Swagger UI show its Authorize button
    public class BearerSecurityTransformer : IOpenApiDocumentTransformer
    {
        private const string SchemeName = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };

            document.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
                }
            ];

            return Task.CompletedTask;
        }
    }
}
