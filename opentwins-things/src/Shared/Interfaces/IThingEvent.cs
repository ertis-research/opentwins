using OpenTwinsv2.Things.Models;

namespace OpenTwinsv2.Things.Interfaces
{
    public interface IThingEvent
    {
        public string EventName { get; set; }
        public string? Data { get; set; }
    }
}