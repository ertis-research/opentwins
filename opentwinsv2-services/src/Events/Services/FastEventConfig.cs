using System.Collections.Concurrent;

namespace Events.Services
{
    public class FastEventConfig
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _fastEventsPerActor = new();

        public void RegisterFastEvent(string actorId, string eventType)
        {
            var set = _fastEventsPerActor.GetOrAdd(actorId, _ => []);
            lock (set) { set.Add(eventType); }
        }

        public bool IsFastEvent(string actorId, string eventType)
        {
            return _fastEventsPerActor.TryGetValue(actorId, out var set) && set.Contains(eventType);
        }
    }
}