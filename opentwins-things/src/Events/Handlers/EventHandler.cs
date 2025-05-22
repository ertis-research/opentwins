using System.Text;
using System.Text.Json;
using Dapr.Messaging.PublishSubscribe;
using Events.Services;

namespace Events.Handlers
{
    public static class EventHandler
    {
        private static readonly HttpClient httpClient = new();

        public static Task<TopicResponseAction> Handle(TopicMessage message, string topic, RoutingService routing)
        {
            try
            {
                //Do something with the message
                Console.WriteLine("Ha llegado algo mi reina");
                Console.WriteLine(Encoding.UTF8.GetString(message.Data.Span));
                //Console.WriteLine(Encoding.UTF8.GetString(message.Data.Span));
                return Task.FromResult(TopicResponseAction.Success);
            }
            catch
            {
                return Task.FromResult(TopicResponseAction.Drop);
            }
        }
/*
        public static async Task<TopicResponseAction> Handle(TopicMessage message, string topic, RoutingService routing)
        {
            try
            {
                var data = JsonDocument.Parse(message.Data).RootElement;
                var targets = routing.GetActorsForTopic(topic);

                foreach (var (actorType, actorId) in targets)
                {
                    var url = $"http://localhost:3500/v1.0/actors/{actorType}/{actorId}/method/ReceiveEvent";
                    await httpClient.PostAsJsonAsync(url, data);
                }

                return TopicResponseAction.Success;
            }
            catch
            {
                return TopicResponseAction.Retry;
            }
        }
*/
    }   
}