using System.Text.Json.Serialization;

public record Container (
    [property: JsonPropertyName("Name")]string? Name,
    [property: JsonPropertyName("Ready")]bool Ready,
    [property: JsonPropertyName("RestartCount")]int RestartCount,
    [property: JsonPropertyName("Image")]string Image,
    [property: JsonPropertyName("State")]string State
);