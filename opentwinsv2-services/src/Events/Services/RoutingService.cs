using System.Collections.Concurrent;
using Events.Handlers;
using OpenTwinsV2.Shared.Models;

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
        private readonly ILogger<RoutingService> _logger;
        //
        private readonly ConcurrentDictionary<string, List<ActorIdentity>> _events = [];
        private readonly ConcurrentDictionary<string, List<string>> _routes = new();

        private readonly FastEventConfig _fastEventConfig;
        private readonly RoutingRepository _repository;

        public RoutingService(FastEventConfig fastEventConfig, RoutingRepository repository, ILogger<RoutingService> logger)
        {
            _fastEventConfig = fastEventConfig;
            _repository = repository;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            var topicsEvents = await _repository.GetTopicsEventsAsync();
            foreach (var (topic, evt) in topicsEvents)
            {
                _routes.AddOrUpdate(topic,
                    (_) => new List<string> { evt },
                    (_, list) =>
                    {
                        lock (list)
                        {
                            if (!list.Contains(evt))
                                list.Add(evt);
                        }
                        return list;
                    });
            }

            // Inicializar _events
            var eventsThings = await _repository.GetEventsThingsAsync();
            foreach (var (evt, actor) in eventsThings)
            {
                _events.AddOrUpdate(evt,
                    (_) => new List<ActorIdentity> { actor },
                    (_, list) =>
                    {
                        lock (list)
                        {
                            if (!list.Any(a => a.Equals(actor)))
                                list.Add(actor);
                        }
                        return list;
                    });
            }
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

        public async void UpdateEventsByActor(ActorIdentity actor, List<EventSubscription> updatedEvents)
        {
            _logger.LogInformation("Starting UpdateEventsByActor for ActorId={ActorId} with {EventCount} updated events", actor.ActorId, updatedEvents.Count);

            var updatedEventNames = updatedEvents.Select(e => e.EventId).ToHashSet();
            var removeEvents = _events.Where(x => !updatedEventNames.Contains(x.Key) && x.Value.Any(a => a.Equals(actor))).ToDictionary();
            //var removeTopics = _routes.Where(x => x.Value.Count == 1 && x.Value.Any(y => removeEvents.ContainsKey(y))).ToDictionary();

            _logger.LogDebug("Found {RemoveCount} events to remove for ActorId={ActorId}", removeEvents.Count, actor.ActorId);

            // 1. Eliminar el actor de los tópicos que ya no están en events
            foreach (var (evt, actors) in removeEvents)
            {
                bool removed = actors.RemoveAll(a => a.Equals(actor)) > 0;
                if (removed)
                {
                    _logger.LogInformation("Removing ActorId={ActorId} from EventId={EventId}", actor.ActorId, evt);
                    await _repository.UnlinkEventThingAsync(evt, actor.ActorId);

                    if (actors.Count == 0)
                    {
                        _logger.LogInformation("EventId={EventId} has no more actors. Removing from _events dictionary", evt);
                        _events.TryRemove(evt, out _);
                    }
                }
            }

            // Eliminamos topicos si solo tenian ese evento
            foreach (var (topic, events) in _routes)
            {
                if (events.Count == 1 && events.Any(y => removeEvents.ContainsKey(y)))
                {
                    _logger.LogInformation("Removing Topic={Topic} because it was linked only to removed EventId={EventId}", topic, events[0]);
                    await _repository.UnlinkTopicEventAsync(topic, events[0]);
                    _routes.TryRemove(topic, out _);
                }
            }

            // 2. Añadir el actor a los tópicos nuevos (si no esta ya)
            foreach (var evt in updatedEvents)
            {
                if (evt.IsFastPath)
                {
                    _logger.LogDebug("Registering FastPath event: EventId={EventId} for ActorId={ActorId}", evt.EventId, actor.ActorId);
                    _fastEventConfig.RegisterFastEvent(actor.ActorId, evt.EventId);
                }

                _events.AddOrUpdate(evt.EventId,
                    (_) => // Si no existe la clave, crea lista con el actor
                    {
                        _logger.LogInformation("Adding new EventId={EventId} with default topic for ActorId={ActorId}", evt.EventId, actor.ActorId);
                        AddEvent(defaultTopic, evt.EventId); /// Por ahora solo defaultTopic
                        return [actor];
                    },
                    (_, actors) => // Si existe la clave, actualiza la lista (añadir si no está)
                    {
                        if (!actors.Any(a => a.Equals(actor)))
                        {
                            _logger.LogInformation("Adding ActorId={ActorId} to existing EventId={EventId}", actor.ActorId, evt.EventId);
                            actors.Add(actor);
                        }
                        return actors;
                    });

                await _repository.LinkEventThingAsync(evt.EventId, actor.ActorId);
                _logger.LogDebug("Linked ActorId={ActorId} to EventId={EventId} in repository", actor.ActorId, evt.EventId);

                await _repository.LinkTopicEventAsync(defaultTopic, evt.EventId);
                _logger.LogDebug("Linked EventId={EventId} to default Topic={Topic} in repository", evt.EventId, defaultTopic);

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