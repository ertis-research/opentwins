using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class APIOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // 1. ORIGINAL CODE: Handle Method-level examples (for [FromBody])
        var methodAttribute = context.MethodInfo
            .GetCustomAttributes(typeof(SwaggerExampleAttribute), false)
            .Cast<SwaggerExampleAttribute>()
            .FirstOrDefault();

        if (methodAttribute != null && operation.RequestBody != null)
        {
            foreach (var content in operation.RequestBody.Content.Values)
            {
                try 
                {
                    content.Example = OpenApiAnyFactory.CreateFromJson(methodAttribute.Example);
                }
                catch
                {
                    content.Example = new OpenApiString(methodAttribute.Example);
                }
            }
        }

        // 2. NEW CODE: Handle Parameter-level examples (for [FromForm])
        if (operation.RequestBody != null)
        {
            var parameters = context.MethodInfo.GetParameters();
            foreach (var param in parameters)
            {
                var paramAttribute = param.GetCustomAttributes(typeof(SwaggerFormExampleAttribute), false)
                    .Cast<SwaggerFormExampleAttribute>()
                    .FirstOrDefault();

                if (paramAttribute != null)
                {
                    // Loop through content types (usually multipart/form-data for forms)
                    foreach (var content in operation.RequestBody.Content.Values)
                    {
                        if (content.Schema?.Properties != null)
                        {
                            // Find the schema property that matches the parameter name (ignoring case for camelCase differences)
                            var property = content.Schema.Properties
                                .FirstOrDefault(p => string.Equals(p.Key, param.Name, StringComparison.OrdinalIgnoreCase));

                            if (property.Value != null)
                            {
                                // Apply the example directly to the specific form field
                                property.Value.Example = new OpenApiString(paramAttribute.Example);
                            }
                        }
                    }
                }
            }
        }

        var responseAttributes = context.MethodInfo
            .GetCustomAttributes(typeof(SwaggerResponseExampleAttribute), false)
            .Cast<SwaggerResponseExampleAttribute>();

        foreach (var attr in responseAttributes)
        {
            var statusCodeString = attr.StatusCode.ToString();
            
            // Check if the endpoint actually declares this status code (e.g., via [ProducesResponseType])
            if (operation.Responses.TryGetValue(statusCodeString, out var response))
            {
                // Apply the example to all content types (usually application/json)
                foreach (var content in response.Content.Values)
                {
                    try 
                    {
                        // Safely parse as rich JSON object in Swagger UI
                        content.Example = OpenApiAnyFactory.CreateFromJson(attr.Example);
                    }
                    catch
                    {
                        // Fallback to plain string if parsing fails
                        content.Example = new OpenApiString(attr.Example);
                    }
                }
            }
        }
    }
}

