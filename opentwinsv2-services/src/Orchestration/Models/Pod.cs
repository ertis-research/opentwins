using System.Text.Json.Serialization;

public record Pod(
    [property: JsonPropertyName("Name")]string? Name,
    [property: JsonPropertyName("JobId")]string? JobId,
    [property: JsonPropertyName("Namespace")]string? Namespace,
    [property: JsonPropertyName("Phase")]string? Phase,
    [property: JsonPropertyName("Node")]string? Node,
    [property: JsonPropertyName("StartTime")]DateTime? StartTime,
    [property: JsonPropertyName("RestartPolicy")]string? RestartPolicy,
    [property: JsonPropertyName("Containers")]List<Container> Containers
);