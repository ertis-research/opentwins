using System;
using System.Text.Json.Serialization;

public class StateMetadata
{
    [JsonPropertyName("isUpdating")]
    public bool IsUpdating { get; set; }

    [JsonPropertyName("lastUpdate")]
    public string LastUpdate { get; set; }

    public StateMetadata() 
    { 
        LastUpdate = default!; 
    }

    public StateMetadata(bool isUpdating, string lastAccess)
    {
        IsUpdating = isUpdating;
        LastUpdate = lastAccess;
    }
}

public class StateWrapper<T>
{
    [JsonPropertyName("metadata")]
    public StateMetadata Metadata { get; set; }

    [JsonPropertyName("data")]
    public T Data { get; set; }

    [JsonConstructor]
    public StateWrapper() 
    { 
        Metadata = default!;
        Data = default!;
    }

    public StateWrapper(T data, string lastAccess, bool isUpdating = false)
    {
        Metadata = new StateMetadata(isUpdating, lastAccess);
        Data = data;
    }
}