using System.Text;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Dapr.Messaging.PublishSubscribe;
using Events.Services;
using OpenTwinsv2.Things.Models;
using Shared.Models;

namespace Events.Handlers
{
    public static class EventHandler
    {
        private static readonly HttpClient httpClient = DaprClient.CreateInvokeHttpClient(appId: "things-service");

        private static readonly DaprClient _daprClient = new DaprClientBuilder().Build();

        public async static Task<TopicResponseAction> Handle(TopicMessage message, string topic, RoutingService routing)
        {
            try
            {
                MyCloudEvent<string> cloudEvent = FromTopicMessageToCloudEvent(message);
                /*string evntData = Encoding.UTF8.GetString(message.Data.Span);
                Console.WriteLine($"Received event in topic '{topic}':");
                Console.WriteLine(evntData);
                Console.WriteLine(message.ToString());

                using var doc = JsonDocument.Parse(evntData);
                JsonElement root = doc.RootElement;*/

                // Extraer tipo de evento
                if (message.Type is null)
                {
                    Console.WriteLine("Field 'type' not found");
                    return TopicResponseAction.Drop;
                }

                string eventType = message.Type;

                // Obtener actores suscritos
                var actors = routing.GetActorsByEventType(eventType);
                if (actors.Count == 0)
                {
                    Console.WriteLine($"No hay actores registrados para el evento '{eventType}'.");
                    return TopicResponseAction.Success;
                }


                foreach (var actor in actors)
                {
                    try
                    {
                        Console.WriteLine($"Enviando a actor {actor.ActorId} ({actor.ActorType})...");
                        var proxy = ActorProxy.Create<IThingActor>(new ActorId(actor.ActorId), actor.ActorType);
                        await proxy.OnEventReceived(cloudEvent);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error enviando a {actor.ActorId}: {ex.Message}");
                    }
                }


                return TopicResponseAction.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error {e.Message}");
                return TopicResponseAction.Drop;
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