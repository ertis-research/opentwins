using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers; // This should now work automatically
using Swashbuckle.AspNetCore.SwaggerGen;

public class UnifiedSwaggerFilter : IDocumentFilter
{
    private readonly IConfiguration _configuration;

    public UnifiedSwaggerFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var daprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3501";
        var daprServices = _configuration.GetSection("Dapr").Get<Dictionary<string, string>>() ?? [];

        using var client = new HttpClient();

        foreach (var service in daprServices)
        {
            var prefix = $"/{service.Key}"; // ej: "/twins" o "/things"
            var appId = service.Value;      // ej: "twins-service"

            var url = $"http://localhost:{daprHttpPort}/v1.0/invoke/{appId}/method/swagger/v1/swagger.json";
            
            try
            {
                // 1. Get the Raw Stream (More robust than String)
                var stream = client.GetStreamAsync(url).GetAwaiter().GetResult();

                // 2. Use Stream Reader
                var reader = new OpenApiStreamReader();
                var remoteDoc = reader.Read(stream, out _);

                // 3. Merge Paths
                foreach (var path in remoteDoc.Paths)
                {
                    var newPath = $"{prefix}{path.Key}";
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
                Console.WriteLine($"Skipping {prefix} (AppId: {appId}): {ex.Message}");
            }
        }
    }
}