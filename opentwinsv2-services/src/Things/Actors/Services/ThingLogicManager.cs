using System.Text.Json;
using OpenTwinsV2.Shared.Models;
using Json.Logic;
using Json.More;
using System.Text.Json.Nodes;
using Dapr.Client;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Models;
using Dapr;
using OpenTwinsV2.Things.Services;
using OpenTwinsV2.Shared.Utilities;
using OpenTwinsV2.Shared.Constants;

namespace OpenTwinsV2.Things.Actors.Services
{
    internal class ThingLogicManager
    {
        private readonly DescriptionManagerService _descManager;
        private readonly ThingDescription? _thingDescription;
        private Dictionary<string, PropertyState> _currentState;
        private readonly StateManagerService _stateManager;
        private readonly StateService _stateService;
        // private readonly EventsService _eventsService;
        private readonly StatusManager _statusManager;
        private readonly string _thingId;

        public ThingLogicManager(string thingId, ThingDescription? thingDescription, Dictionary<string, PropertyState> currentState, StateService stateService, StatusManager statusManager, DescriptionManagerService descManager, StateManagerService stateManager)
        {
            _descManager = descManager;
            _stateManager = stateManager;
            _thingId = thingId;
            _thingDescription = thingDescription;
            _stateService = stateService;
            // _eventsService = eventsService;
            _statusManager = statusManager;
            _currentState = currentState;
        }

        public async Task<string?> GetThingDescriptionAsync()
        {
            //check thing's availability in state
            var status = (await _statusManager.GetThingStatus(_thingId)).Status;
            if(status is null)
            {
                // Check if it exists on the databases anyway (old things without status), and if it is, save the ok status
                var description = await _descManager.LoadDescriptionAsync(_thingId);
                if(description is null)
                    return null;
                //load an ok status
                await _statusManager.SaveOkThingStatus(_thingId);
                return description.ToString();
            }    
            else if(status == Status.DeleteStatus)
                return null;
            else if (status == Status.UpdateStatus || status == Status.CreateStatus)
                throw new InvalidOperationException();
            else
                if (_thingDescription == null)
                    return (await _descManager.LoadDescriptionAsync(_thingId))?.ToString();

            return _thingDescription?.ToString();
        }

        public async Task<string> GetThingStatusAsync()
        {
            return (await _statusManager.GetThingStatus(_thingId)).Status ?? throw new KeyNotFoundException();
        }

        public string GetCurrentState()
        {
            return JsonSerializer.Serialize(_currentState);
        }

        public void UpdateCurrentState(Dictionary<string, PropertyState> newState)
        {
            _currentState = newState;
        }

        private async Task ApplyLogicToDerivedProperties()
        {
            if (_thingDescription?.Properties is null)
            {
                ActorLogger.Info(_thingId, "ThingDescription has no derived properties, skipping derived logic.");
                return;
            }

            var updated = new Dictionary<string, PropertyState>();

            foreach (var (propName, propDesc) in _thingDescription.Properties)
            {
                if (propDesc is null || propDesc.JsonLogic is null) continue;

                try
                {
                    JsonNode? logic = propDesc.JsonLogic?.AsNode();
                    //ActorLogger.Info(_thingId, $"Applying JsonLogic to property '{propName}' with logic: {JsonSerializer.Serialize(logic)}");

                    var context = new JsonObject();
                    foreach (var (key, state) in _currentState)
                    {
                        if (state?.Value is JsonElement je)
                            context[key] = je.AsNode();
                    }

                    //ActorLogger.Info(_thingId, $"Context for '{propName}': {context.ToJsonString()}");

                    var result = JsonLogic.Apply(logic, context);

                    if (result is JsonValue)
                    {
                        JsonElement jeResult = JsonDocument.Parse(result.ToJsonString()).RootElement;
                        updated[propName] = new PropertyState(jeResult, DateTime.UtcNow);
                        ActorLogger.Info(_thingId, $"Property '{propName}' updated with value: {jeResult}");
                    }
                }
                catch (Exception ex)
                {
                    ActorLogger.Error(_thingId, $"Error applying JsonLogic for property '{propName}': {ex.Message}");
                }
            }

            if (updated.Count > 0)
            {
                await _stateManager.UpdateStateAsync(_thingId, _currentState, updated, _thingDescription?.Properties);
            }
        }

        public async Task ApplyEventAsync(MyCloudEvent<string> evt)
        {
            var eventType = evt.Type ?? "UNKNOWN";
            ActorLogger.Info(_thingId, $"Applying event with type '{eventType}'");

            if (_thingDescription?.Rules is null)
            {
                ActorLogger.Warn(_thingId, $"No rules defined. Event ignored. Type: {eventType}");
                return;
            }

            if (_thingDescription?.Rules is null) return;

            JsonObject info = [];
            info["eventName"] = evt.Type ?? "";

            JsonNode? payload = null;
            try { payload = evt.Data is null ? null : JsonNode.Parse(evt.Data); }
            catch { }

            JsonNode context = ComposeState(info, payload);

            foreach (var (name, logic) in _thingDescription.Rules)
            {
                var subscribed = _thingDescription.SubscribedEvents?.FirstOrDefault(e => e.Event == eventType);
                if (subscribed?.Source != null && subscribed.Source.Count > 0 && evt.Source is not null && !subscribed.Source.Contains(evt.Source)) continue;

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

            foreach (var (key, val) in _currentState)
                if (val?.Value is JsonElement je)
                    json[key] = je.AsNode();
                else
                    json[key] = null;

            return json;
        }

        private async Task ApplyThenAsync(Then then, JsonNode context)
        {
            Dictionary<string, PropertyState> previousState = await _stateManager.LoadStateAsync(_thingId);
            ActorLogger.Info(_thingId, $"PREVIOUS FIRST");
            foreach(var (k,v) in _currentState)
            {
                ActorLogger.Info(_thingId, $"{k} --> {v}");
                previousState[k] = new PropertyState(v.Value ?? new JsonElement(), v.LastUpdate);
            }
                
            Dictionary<string,PropertyState>? currentState = null;
            if (then.UpdateState != null)
            {
                ActorLogger.Info(_thingId, $"Executing UpdateState action.");
                await HandleUpdateState(then.UpdateState, context);
                currentState = [];
                foreach(var (k,v) in _currentState)
                {
                    currentState[k] = new PropertyState(v.Value ?? new JsonElement(), v.LastUpdate);
                }
            }
            ActorLogger.Info(_thingId, $"PREVIOUS SECOND");
            foreach(var (k,v) in _currentState)
            {
                ActorLogger.Info(_thingId, $"{k} --> {v}");
                // previousState[k] = new PropertyState(v.Value ?? new JsonElement(), v.LastUpdate);
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
                    await HandleEmitEventAsync(evnt, context, previousState, currentState ?? previousState);
                }
            }
        }

        private async Task HandleUpdateState(Dictionary<string, UpdatePropertyState> updates, JsonNode context)
        {
            var updated = new Dictionary<string, PropertyState>();
            //ActorLogger.Info(_thingId, "HandleUpdateState started");

            foreach (var (key, schema) in updates)
            {
                JsonElement? newVal = null;
                DateTime? ts = null;

                //ActorLogger.Info(_thingId, $"Processing property: {key}");

                if (schema.NewValue.HasValue)
                {
                    //ActorLogger.Info(_thingId, $"Applying JsonLogic to NewValue for property: {key}");
                    var res = JsonLogic.Apply(schema.NewValue.Value.AsNode(), context);
                    ActorLogger.Info(_thingId, $"RES OBTAINED {key} - {res} from context: {context}");
                    if (res is JsonValue jv && jv.TryGetValue(out JsonElement je))
                    {
                        newVal = je;
                        ActorLogger.Info(_thingId, $"NewValue resolved for property: {key} - {newVal}");
                    }else if(res is not null)
                    {
                        newVal = JsonDocument.Parse(res.ToJsonString()).RootElement;
                        ActorLogger.Info(_thingId, $"NewValue resolved for property: {key} - {newVal}");
                    }
                }

                if (newVal != null && schema.Timestamp.HasValue)
                {
                    //ActorLogger.Info(_thingId, $"Applying JsonLogic to Timestamp for property: {key}");
                    var res = JsonLogic.Apply(schema.Timestamp.Value.AsNode(), context);
                    if (res is JsonValue jv && jv.TryGetValue(out JsonElement tsJson))
                    {
                        ts = tsJson.ValueKind switch
                        {
                            JsonValueKind.String => DateTime.TryParse(tsJson.GetString(), out var dt) ? dt.ToUniversalTime() : null,
                            JsonValueKind.Number => tsJson.TryGetInt64(out var unix) ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime : null,
                            _ => null
                        };
                        //ActorLogger.Info(_thingId, $"Timestamp resolved for property: {key} - {ts}");
                    }
                }

                if (newVal != null)
                {
                    updated[key] = new PropertyState(newVal.Value, ts);
                    //ActorLogger.Info(_thingId, $"Property state updated: {key} - Value: {newVal}, Timestamp: {ts}");
                }
            }

            await _stateManager.UpdateStateAsync(_thingId, _currentState, updated, _thingDescription?.Properties);
            await ApplyLogicToDerivedProperties();
            ActorLogger.Info(_thingId, $"Ha terminado de actualizarse");
        }

        private async Task HandleInvokeActionAsync(ThenInvokeAction invokeAction, JsonNode data)
        {
            ActorLogger.Warn(_thingId, "INVOKE ACTION. NOT IMPLEMENTED");
            await Task.CompletedTask;
        }

        private ActionAffordance? IsMyAction(string name, string parameters)
        {
            // Validación básica del evento recibido
            if (_thingDescription?.Actions is null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            var exists = _thingDescription.Actions.FirstOrDefault(x =>
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
            await Task.CompletedTask;
            // Falta comprobacion de si la accion es mia jeje
            /*
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
                        if (data is not null) await _stateManager.UpdateAsync(data, _thingDescription?.Properties);
                    }
                    catch { }
                    break;

                default:
                    throw new ArgumentException($"Unsupported action: {action}");
            }
            */
        }

        private async Task HandleEmitEventAsync(ThenEmitEvent emitEvent, JsonNode data, Dictionary<string,PropertyState> previousState, Dictionary<string,PropertyState> currentState) // MODIFICAR
        {
            //Console.WriteLine($"[INFO: {Id}] EMIT EVENT. NOT FULLY IMPLEMENTED");
            JsonObject payload = [];

            payload["thingId"] = _thingId;
            payload["message"] = data["payload"]?.DeepClone();
            payload["previousState"] = JsonSerializer.SerializeToNode(previousState);
            payload["currentState"] = JsonSerializer.SerializeToNode(currentState);

            
            Console.WriteLine(JsonSerializer.Serialize(payload));
            await _stateService.PublishEvent(_thingId, emitEvent.Event, payload);
        }
    }
}
