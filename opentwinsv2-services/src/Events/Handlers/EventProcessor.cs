using System.Text;
using System.Threading.Channels;
using Dapr.Messaging.PublishSubscribe;
using Events.Services;
using Shared.Models;

namespace Events.Handlers
{
    public class EventProcessor
    {
        //private static readonly HttpClient httpClient = DaprClient.CreateInvokeHttpClient(appId: "things-service");
        private readonly Channel<IncomingEvent> _channel;
        private readonly RoutingService _routingService;
        private readonly ActorEventRouter _actorRouter;

        //private static readonly DaprClient _daprClient = new DaprClientBuilder().Build();

        public EventProcessor(RoutingService routingService, ActorEventRouter actorRouter, int workerCount = 4)
        {
            _routingService = routingService;
            _actorRouter = actorRouter;
            _channel = Channel.CreateUnbounded<IncomingEvent>();

            for (int i = 0; i < workerCount; i++)
            {
                _ = Task.Run(ProcessEventsAsync);
            }
        }

        public async Task EnqueueEvent(TopicMessage message, string topic)
        {
            await _channel.Writer.WriteAsync(new IncomingEvent(message, topic));
        }

        private async Task ProcessEventsAsync()
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync())
            {
                await HandleEvent(evt);
            }
        }

        private async Task HandleEvent(IncomingEvent evt)
        {
            try
            {
                //Console.WriteLine("[DEBUG] HandleEvent: " + evt.Message);
                var cloudEvent = FromTopicMessageToCloudEvent(evt.Message);

                if (evt.Message.Type is null)
                {
                    Console.WriteLine("Field 'type' not found");
                    return;
                }

                var eventType = evt.Message.Type;
                var actors = _routingService.GetActorsByEventType(eventType);
                if (actors.Count == 0)
                {
                    Console.WriteLine($"No hay actores registrados para el evento '{eventType}'.");
                    return;
                }

                foreach (var actor in actors)
                {
                    await _actorRouter.SendToActorAsync(actor.ActorId, actor.ActorType, cloudEvent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleEvent: {ex.Message}");
            }
        }

        private static MyCloudEvent<string> FromTopicMessageToCloudEvent(TopicMessage message)
        {
            string evntData;
            try
            {
                evntData = Encoding.UTF8.GetString(message.Data.Span);
            }
            catch
            {
                evntData = "";
            }

            return new MyCloudEvent<string>(
                    message.Id,
                    message.Source, //Id del actor que lo produce (si es mqtt por ejemplo, igualmente en benthos ponemos el id del thing que lo controla)
                    message.Type,
                    message.SpecVersion,
                    null, //Como tal los eventos no los dirigimos a nadie en concreto, por el momento
                    DateTime.UtcNow, //Esto sera el momento de llegada del evento al sistema
                    message.DataContentType,
                    evntData
                );
        }

    }
}

public record IncomingEvent(TopicMessage Message, string Topic);