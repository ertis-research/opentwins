using System.Threading.Channels;
using Dapr.Actors;
using Dapr.Actors.Client;
using OpenTwinsv2.Things.Models;
using Shared.Models;

namespace Events.Handlers
{
    public class ActorEventQueue
    {
        private readonly Channel<MyCloudEvent<string>> _channel = Channel.CreateUnbounded<MyCloudEvent<string>>();
        private readonly IThingActor _actorProxy;
        private readonly string _actorId;

        public ActorEventQueue(string actorId, string actorType)
        {
            _actorId = actorId;
            _actorProxy = ActorProxy.Create<IThingActor>(new ActorId(actorId), actorType);

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
                try
                {
                    await _actorProxy.OnEventReceived(cloudEvent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Actor {_actorId} falló: {ex.Message}");
                    // Opcional: retry/backoff o dead-letter
                }
            }
        }
    }
}