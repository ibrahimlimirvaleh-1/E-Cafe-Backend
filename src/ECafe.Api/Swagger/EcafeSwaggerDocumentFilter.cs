using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ECafe.Api.Swagger;

public sealed class EcafeSwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags = EcafeSwaggerMetadata.Tags
            .Select(tag => new OpenApiTag
            {
                Name = tag.Value.Name,
                Description = tag.Value.Description
            })
            .ToList();
    }
}
