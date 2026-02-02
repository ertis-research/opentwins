using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers; // This should now work automatically
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;

public class UnifiedSwaggerFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
        var services = new[]
        {
            new
            {
                Prefix = "/things",
                Url = $"http://localhost:{daprHttpPort}/v1.0/invoke/things-service/method/swagger/v1/swagger.json"
            },
            new
            {
                Prefix = "/twins",
                Url = $"http://localhost:{daprHttpPort}/v1.0/invoke/twins-service/method/swagger/v1/swagger.json"
            }
        };

        using var client = new HttpClient();

        foreach (var service in services)
        {
            try
            {
                // 1. Get the Raw Stream (More robust than String)
                var stream = client.GetStreamAsync(service.Url).Result;

                // 2. Use Stream Reader
                var reader = new OpenApiStreamReader();
                var remoteDoc = reader.Read(stream, out _);

                // 3. Merge Paths
                foreach (var path in remoteDoc.Paths)
                {
                    var newPath = $"{service.Prefix}{path.Key}";
                    if (!swaggerDoc.Paths.ContainsKey(newPath))
                    {
                        swaggerDoc.Paths.Add(newPath, path.Value);
                    }
                }

                // 4. Merge Schemas
                foreach (var schema in remoteDoc.Components.Schemas)
                {
                    if (!swaggerDoc.Components.Schemas.ContainsKey(schema.Key))
                    {
                        swaggerDoc.Components.Schemas.Add(schema.Key, schema.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Skipping {service.Prefix}: {ex.Message}");
            }
        }
    }
}