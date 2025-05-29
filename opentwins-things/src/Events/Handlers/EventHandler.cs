using System.Text;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Dapr.Messaging.PublishSubscribe;
using Events.Services;
using OpenTwinsv2.Things.Interfaces;

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
                string evntData = Encoding.UTF8.GetString(message.Data.Span);
                Console.WriteLine($"Received event in topic '{topic}':");
                Console.WriteLine(evntData);

                using var doc = JsonDocument.Parse(evntData);
                JsonElement root = doc.RootElement;

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
                        await proxy.OnEventReceived(root.ToString());
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