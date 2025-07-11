using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Events.Services;
using OpenTwinsv2.Things.Models;
using Shared.Models;

namespace Events.Handlers
{
    public class ActorEventQueue
    {
        private readonly Channel<MyCloudEvent<string>> _channel = Channel.CreateUnbounded<MyCloudEvent<string>>();
        private readonly FastEventConfig _fastEventConfig;
        private readonly IThingActor _actorProxy;
        private readonly string _actorId;
        private readonly DaprClient _daprClient;

        public ActorEventQueue(string actorId, string actorType, FastEventConfig fastEventConfig)
        {
            _actorId = actorId;
            _actorProxy = ActorProxy.Create<IThingActor>(new ActorId(actorId), actorType);
            _daprClient = new DaprClientBuilder().Build();
            _fastEventConfig = fastEventConfig;

            _ = Task.Run(ProcessQueueAsync); // inicia procesamiento en background
        }

        public async Task EnqueueAsync(MyCloudEvent<string> cloudEvent)
        {
            await _channel.Writer.WriteAsync(cloudEvent);
        }

        private async Task ProcessQueueAsync()
        {
            await foreach (var cloudEvent in _channel.Reader.ReadAllAsync())
            {
                //Console.WriteLine("[DEBUG] ProcessQueueAsync: " + cloudEvent.Data ?? "NO DATA");
                try
                {
                    if (_fastEventConfig.IsFastEvent(_actorId, cloudEvent.Type))
                    {
                        var transformed = TransformFastEvent(cloudEvent);
                        await _daprClient.PublishEventAsync(
                            pubsubName: "mypubsub",
                            topicName: "thing.state.changed",
                            data: transformed);
                        //Console.WriteLine("[DEBUG] PublishEventAsync: " + transformed ?? "NO DATA");
                        //Console.WriteLine("PUBLICADO: " + JsonSerializer.Serialize(transformed));
                    }
                    else
                    {
                        await _actorProxy.OnEventReceived(cloudEvent);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Actor {_actorId} fails: {ex.Message}");
                    // Opcional: retry/backoff o dead-letter
                }
            }
        }

        private JsonNode? TransformFastEvent(MyCloudEvent<string> cloudEvent)
        {
            JsonNode dataNode = new JsonObject();
            if (cloudEvent.Data != null)
            {
                var parse = JsonNode.Parse(cloudEvent.Data);
                if (parse != null) dataNode = parse;
            }

            dataNode["thingId"] = _actorId;
            return dataNode;
        }
    }
}