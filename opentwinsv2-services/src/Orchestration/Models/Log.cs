using System.Text.Json.Serialization;

public record Log(
    [property: JsonPropertyName("level")] string? level,    
    [property: JsonPropertyName("time")] DateTime? time,    
    [property: JsonPropertyName("msg")] string? msg
);