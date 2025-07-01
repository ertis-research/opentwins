using Dapr.Messaging.PublishSubscribe;

namespace Events.Models
{
    public class IncomingEvent
    {
        public TopicMessage Message { get; }
        public string Topic { get; }

        public IncomingEvent(TopicMessage message, string topic)
        {
            Message = message;
            Topic = topic;
        }
    }
}