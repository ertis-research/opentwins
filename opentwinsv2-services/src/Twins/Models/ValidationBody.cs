using System.Text.Json.Nodes;

namespace OpenTwinsV2.Twins.Models
{
    public record ValidationBody(JsonNode Graph){
        public JsonNode? CurrentThings = null; 
        public ICollection<string> CheckedThings = new HashSet<string>();
        public Dictionary<string, ICollection<string>>? Logs {get; set;} = null;
    }
    
}