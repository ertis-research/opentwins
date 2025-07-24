using System.Collections.Concurrent;
using Events.Services;
using OpenTwinsV2.Shared.Models;

namespace Events.Handlers
{
    public class ActorEventRouter
    {
        private readonly ConcurrentDictionary<string, ActorEventQueue> _actorQueues = new();
        private readonly FastEventConfig _fastEventConfig;

        public ActorEventRouter(FastEventConfig fastEventConfig)
        {
            _fastEventConfig = fastEventConfig;
        }

        public async Task SendToActorAsync(string actorId, string actorType, MyCloudEvent<string> evt)
        {
            var queue = _actorQueues.GetOrAdd(actorId, id => new ActorEventQueue(id, actorType, _fastEventConfig));
            await queue.EnqueueAsync(evt);
        }
    }
}