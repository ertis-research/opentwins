using System.Collections.Concurrent;
using Events.Handlers;
using Shared.Models;

namespace Events.Services
{
    public class RoutingService
    {

        /* 
            Lo voy a hacer asi aunque, por el momento, solo vamos a usar un topico para todos los eventos. Pero para tenerlo bien para cuando se cambie.
            _routes = {
                "opentwinsv2-events": {  //topic
                    "device.created": [  //eventType
                        ("DeviceActor", "dev-1"),
                        ("LoggerActor", "log-1")
                    ],
                    "alarm.triggered": [
                        ("AlarmActor", "alarm-3")
                    ]
                }
            }
        */
        private static readonly string defaultTopic = "opentwinsv2.events";
        //
        private readonly ConcurrentDictionary<string, List<ActorIdentity>> _events = [];
        private readonly ConcurrentDictionary<string, List<string>> _routes = new();

        private readonly FastEventConfig _fastEventConfig;

        public RoutingService(FastEventConfig fastEventConfig)
        {
            _fastEventConfig = fastEventConfig;
        }

        public List<ActorIdentity> GetActorsByEventType(string eventType)
        {
            return _events.TryGetValue(eventType, out var actors) ? actors : [];
        }

        public Dictionary<string, List<string>> GetSubscriptions()
        {
            // Retorna un diccionario de topic -> lista de eventos
            return new Dictionary<string, List<string>>(_routes);
        }

        public void UpdateEventsByActor(ActorIdentity actor, List<EventSubscription> updatedEvents)
        {
            var updatedEventNames = updatedEvents.Select(e => e.EventId).ToHashSet();

            var removeEvents = _events.Where(x => !updatedEventNames.Contains(x.Key) && x.Value.Any(a => a.Equals(actor))).ToDictionary();
            var removeTopics = _routes.Where(x => x.Value.Count == 1 && x.Value.Any(y => removeEvents.ContainsKey(y))).ToDictionary();

            // 1. Eliminar el actor de los tópicos que ya no están en events
            foreach (var (topic, actors) in removeEvents)
            {
                bool removed = actors.RemoveAll(a => a.Equals(actor)) > 0;
                // Si la lista quedó vacía, puedes decidir si borrar el topic del diccionario
                if (removed && actors.Count == 0) _routes.TryRemove(topic, out _);
            }

            // Eliminamos topicos si solo tenian ese evento
            foreach (var kvp in _routes)
            {
                if (kvp.Value.Count == 1 && kvp.Value.Any(y => removeEvents.ContainsKey(y)))
                {
                    _routes.TryRemove(kvp.Key, out _);
                }
            }

            // 2. Añadir el actor a los tópicos nuevos (si no esta ya)
            foreach (var evt in updatedEvents)
            {
                if (evt.IsFastPath)
                {
                    _fastEventConfig.RegisterFastEvent(actor.ActorId, evt.EventId);
                }

                _events.AddOrUpdate(evt.EventId,
                    (_) => // Si no existe la clave, crea lista con el actor
                    {
                        AddEvent(defaultTopic, evt.EventId); /// Por ahora solo defaultTopic
                        return [actor];
                    },
                    (_, actors) => // Si existe la clave, actualiza la lista (añadir si no está)
                    {
                        if (!actors.Any(a => a.Equals(actor)))
                        {
                            actors.Add(actor);
                        }
                        return actors;
                    });
            }
        }

        public IEnumerable<string> GetAllTopics() => _routes.Keys;

        public void AddEvent(string topic, string eventType)
        {
            _routes.AddOrUpdate(topic,
                // Si no existe la clave, crea lista con el actor
                (_) => [eventType],
                // Si existe la clave, actualiza la lista (añadir si no está)
                (_, events) =>
                {
                    lock (events)
                    {
                        if (!events.Contains(eventType))
                        {
                            events.Add(eventType);
                        }
                    }
                    return events;
                });
        }

        public bool RemoveEvent(string topic, string eventType)
        {
            if (_routes.TryGetValue(topic, out var eventList))
            {
                lock (eventList)
                {
                    int removedCount = eventList.RemoveAll(e => e == eventType);
                    return removedCount > 0;
                }
            }
            return false;
        }

    }
}