using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WebHooks.Api.Swagger;

public sealed class WebhookSignatureHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + context.ApiDescription.RelativePath?.TrimStart('/');

        if (path is null)
            return;

        // 🔥 SADECE webhook receiver endpoint’leri
        if (!path.StartsWith("/webhooks", StringComparison.OrdinalIgnoreCase))
            return;

        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Webhook-Id",
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema { Type = "string" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Webhook-Timestamp",
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema { Type = "string" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Webhook-Signature",
            In = ParameterLocation.Header,
            Required = true,
            Description = "sha256=<hex>. Computed over {timestamp}.{rawBody}",
            Schema = new OpenApiSchema { Type = "string" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Webhook-Event",
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema { Type = "string" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Webhook-Provider",
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema { Type = "string" }
        });
    }


}