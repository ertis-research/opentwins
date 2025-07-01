using System.Collections.Concurrent;
using Shared.Models;

namespace Events.Handlers
{
    public class ActorEventRouter
    {
        private readonly ConcurrentDictionary<string, ActorEventQueue> _actorQueues = new();

        public async Task SendToActorAsync(string actorId, string actorType, MyCloudEvent<string> evt)
        {
            var queue = _actorQueues.GetOrAdd(actorId, id => new ActorEventQueue(id, actorType));
            await queue.EnqueueAsync(evt);
        }
    }
}