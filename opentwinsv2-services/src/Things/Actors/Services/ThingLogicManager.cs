using System.Text.Json;
using OpenTwinsV2.Shared.Models;
using Json.Logic;
using Json.More;
using System.Text.Json.Nodes;
using Dapr.Client;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Things.Actors.Services
{
    internal class ThingLogicManager
    {
        private readonly ThingDescriptionManager _descManager;
        private readonly ThingStateManager _stateManager;
        private readonly DaprClient _daprClient;
        private readonly string _thingId;

        public ThingLogicManager(DaprClient daprClient, string thingId, ThingDescriptionManager descManager, ThingStateManager stateManager)
        {
            _descManager = descManager;
            _stateManager = stateManager;
            _daprClient = daprClient;
            _thingId = thingId;
        }

        public async Task<string> SetThingDescriptionAsync(string json)
        {
            var td = JsonSerializer.Deserialize<ThingDescription>(json)
                ?? throw new InvalidOperationException("Invalid ThingDescription");

            await _descManager.SaveAsync(td);
            await _stateManager.InitializeFromDescription(td.Properties);

            var events = GetSubscribedEvents(td.Links);
            if (events.Count > 0)
                await SubscribeToEventsAsync(events);

            return "Success";
        }

        private List<EventSubscription> GetSubscribedEvents(List<Link>? links)
        {
            if (links is null) return [];

            List<EventSubscription> subscriptions = [];

            foreach (Link link in links)
            {
                if (link.Rel != null &&
                    link.Rel.Equals("subscribeEvent", StringComparison.OrdinalIgnoreCase))
                {
                    var segments = link.Href.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 4 && segments[0] == "things" && segments[2] == "events")
                    {
                        string thingName = segments[1];
                        string eventName = segments[3];
                        subscriptions.Add(new EventSubscription($"{thingName}:{eventName}", link.EmitStateOnReceive));
                    }
                }
            }
            //Console.WriteLine($"[INFO] Thing with ID {Id} is subscribed to: {string.Join(", ", subscriptions)}");
            return subscriptions;
        }

        private async Task SubscribeToEventsAsync(List<EventSubscription> events)
        {
            var client = DaprClient.CreateInvokeHttpClient();
            var cts = new CancellationTokenSource();
            var response = await client.PostAsJsonAsync($"http://events-service/events/things/{_thingId}", events, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                ActorLogger.Info(_thingId, $"Successfully subscribed to events.");
            }
            else
            {
                ActorLogger.Error(_thingId, $"Failed to subscribe to events. StatusCode: {(int)response.StatusCode}, Reason: {response.ReasonPhrase}");
            }
        }

        public async Task<string?> GetThingDescriptionAsync()
        {
            if (_descManager.ThingDescription == null)
                await _descManager.LoadAsync();

            return _descManager.ThingDescription?.ToString();
        }

        public string GetCurrentState()
        {
            return JsonSerializer.Serialize(_stateManager.CurrentState);
        }

        public async Task ApplyEventAsync(MyCloudEvent<string> evt)
        {
            var eventType = evt.Type ?? "UNKNOWN";
            ActorLogger.Info(_thingId, $"Applying event with type '{eventType}'");

            if (_descManager.ThingDescription?.Rules is null)
            {
                ActorLogger.Warn(_thingId, $"No rules defined. Event ignored. Type: {eventType}");
                return;
            }

            if (_descManager.ThingDescription?.Rules is null) return;

            JsonObject info = [];
            info["eventName"] = evt.Type ?? "";

            JsonNode? payload = null;
            try { payload = evt.Data is null ? null : JsonNode.Parse(evt.Data); }
            catch { }

            JsonNode context = ComposeState(info, payload);

            foreach (var (name, logic) in _descManager.ThingDescription.Rules)
            {
                ActorLogger.Info(_thingId, $"Evaluating rule '{name}'. EventType: {eventType}");
                if (logic.If?.AsNode() is JsonNode cond)
                {
                    bool match = JsonLogic.Apply(cond, context)?.GetValue<bool>() == true;
                    if (match && logic.Then != null)
                    {
                        ActorLogger.Info(_thingId, $"Rule '{name}' matched. Applying 'then' logic. EventType: {eventType}");
                        await ApplyThenAsync(logic.Then, context);
                    }
                }
            }

            ActorLogger.Info(_thingId, $"Event processing completed. EventType: {eventType}");
        }

        private JsonNode ComposeState(JsonObject info, JsonNode? payload)
        {
            var json = info.DeepClone().AsObject();
            if (payload != null) json["payload"] = payload;

            foreach (var (key, val) in _stateManager.CurrentState)
                if (val?.Value is JsonElement je)
                    json[key] = je.AsNode();

            return json;
        }

        private async Task ApplyThenAsync(Then then, JsonNode context)
        {
            if (then.UpdateState != null)
            {
                ActorLogger.Info(_thingId, $"Executing UpdateState action.");
                await HandleUpdateState(then.UpdateState, context);
            }
            if (then.InvokeAction != null)
            {
                foreach (ThenInvokeAction action in then.InvokeAction)
                {
                    ActorLogger.Info(_thingId, $"Invoking action: {action.Action ?? "(no name)"}");
                    await HandleInvokeActionAsync(action, context);
                }
            }

            if (then.EmitEvent != null)
            {
                foreach (ThenEmitEvent evnt in then.EmitEvent)
                {
                    ActorLogger.Info(_thingId, $"Emitting event: {evnt.Event ?? "(no type)"}");
                    await HandleEmitEventAsync(evnt, context);
                }
            }
        }

        private async Task HandleUpdateState(Dictionary<string, UpdatePropertyState> updates, JsonNode context)
        {
            var updated = new Dictionary<string, PropertyState>();

            foreach (var (key, schema) in updates)
            {
                JsonElement? newVal = null;
                DateTime? ts = null;

                if (schema.NewValue.HasValue)
                {
                    var res = JsonLogic.Apply(schema.NewValue.Value.AsNode(), context);
                    if (res is JsonValue jv && jv.TryGetValue(out JsonElement je))
                        newVal = je;
                }

                if (newVal != null && schema.Timestamp.HasValue)
                {
                    var res = JsonLogic.Apply(schema.Timestamp.Value.AsNode(), context);
                    if (res is JsonValue jv && jv.TryGetValue(out JsonElement tsJson))
                    {
                        ts = tsJson.ValueKind switch
                        {
                            JsonValueKind.String => DateTime.TryParse(tsJson.GetString(), out var dt) ? dt.ToUniversalTime() : null,
                            JsonValueKind.Number => tsJson.TryGetInt64(out var unix) ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime : null,
                            _ => null
                        };
                    }
                }

                if (newVal != null)
                    updated[key] = new PropertyState(newVal.Value, ts);
            }

            await _stateManager.UpdateAsync(updated, _descManager.ThingDescription?.Properties);
        }

        private async Task HandleInvokeActionAsync(ThenInvokeAction invokeAction, JsonNode data)
        {
            ActorLogger.Warn(_thingId, "INVOKE ACTION. NOT IMPLEMENTED");
            await Task.CompletedTask;
        }

        private ActionAffordance? IsMyAction(string name, string parameters)
        {
            // Validación básica del evento recibido
            if (_descManager.ThingDescription?.Actions is null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            var exists = _descManager.ThingDescription.Actions.FirstOrDefault(x =>
            {
                if (x.Key != name) return false;
                return true;
                //Por ahora comentamos
                //if (x.Value.Input is null) return parameters is null;
                //if (parameters is null) return x.Value.Input.Type == "null";
                //return SchemaValidator.IsTypeCompatible(x.Value.Input.Type, SchemaValidator.DetectJsonElementType(parameters));
            });

            return (exists.Key is null) ? null : exists.Value;
        }

        public async Task ApplyInvokeAction(string action, string parameters)
        {
            Console.WriteLine("ACCION INVOCADA: " + action);
            // Falta comprobacion de si la accion es mia jeje
            if (IsMyAction(action, parameters) is null) return;
            switch (action)
            {
                case "updateProperties":
                    try
                    {
                        Dictionary<string, PropertyState>? data = JsonSerializer.Deserialize<Dictionary<string, PropertyState>>(parameters, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (data is not null) await _stateManager.UpdateAsync(data, _descManager.ThingDescription?.Properties);
                    }
                    catch { }
                    break;

                default:
                    throw new ArgumentException($"Unsupported action: {action}");
            }
        }

        private async Task HandleEmitEventAsync(ThenEmitEvent emitEvent, JsonNode data)
        {
            //Console.WriteLine($"[INFO: {Id}] EMIT EVENT. NOT FULLY IMPLEMENTED");
            JsonNode payload;
            //Console.WriteLine(emitEvent.Data);
            if (emitEvent.Data.HasValue &&
                emitEvent.Data.Value.ValueKind == JsonValueKind.String &&
                emitEvent.Data.Value.GetString()?.Trim().ToLowerInvariant() == "state")
            {
                payload = data["payload"] ?? new JsonObject(); // !!CAMBIAR Esto lo cambio en un futuro para que tenga en cuenta un formato, ahora mismo lo envio en el que tiene que ser
                payload["thingId"] = _thingId;
            }
            else
            {
                payload = new JsonObject();
            }
            //Console.WriteLine(JsonSerializer.Serialize(payload));
            await _daprClient.PublishEventAsync("kafka-pubsub", emitEvent.Event, payload);
        }
    }
}
