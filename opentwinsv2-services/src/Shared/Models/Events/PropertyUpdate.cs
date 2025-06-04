namespace Shared.Models
{
    public class PropertyUpdateBatch
    {
        public List<PropertyUpdate> Updates { get; set; } = [];
    }
    
    public class PropertyUpdate
    {
        public string InteractionType { get; set; } = "property";
        public required string Name { get; set; }
        public required object Value { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}