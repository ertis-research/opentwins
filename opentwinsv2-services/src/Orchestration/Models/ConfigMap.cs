using System.Text.Json.Serialization;

public record ConfigMap (
    [property: JsonPropertyName("Name")]string? Name,
    [property: JsonPropertyName("Namespace")]string? Namespace,
    [property: JsonPropertyName("CreationTime")]DateTime? CreationTime,
    [property: JsonPropertyName("Labels")]Dictionary<string, string>? Labels,
    [property: JsonPropertyName("Data")]Dictionary<string, object>? Data
);