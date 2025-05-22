using System.Text.Json;

namespace OpenTwinsv2.Things.Interfaces
{
    public interface ICloudEvent
    {
        public string Type { get; set; }
        public JsonElement Data { get; set; }
    }
}