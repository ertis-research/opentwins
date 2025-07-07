using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Json.Logic;
using Json.More;
using OpenTwinsv2.Things.Models;
using Shared.Models;
using Shared.Utilities;

namespace OpenTwinsv2.Things.Services
{
    internal class ThingActor : Actor, IThingActor, IRemindable
    {
        private const string ThingDescriptionKey = "TD_";
        private const string CurrentStateKey = "CS_";
        private Dictionary<string, PropertyState> currentState = [];
        private ThingDescription? thingDescription;
        private const string StateStoreName = "actorstatestore";
        private static readonly DaprClient _daprClient = new DaprClientBuilder().Build();

        // The constructor must accept ActorHost as a parameter, and can also accept additional
        // parameters that will be retrieved from the dependency injection container
        //
        /// <summary>
        /// Initializes a new instance of MyActor
        /// </summary>
        /// <param name="host">The Dapr.Actors.Runtime.ActorHost that will host this actor instance.</param>
        public ThingActor(ActorHost host)
            : base(host)
        {
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override async Task OnActivateAsync()
        {
            Console.WriteLine($"[INFO] Activating actor id: {this.Id}");
            try
            {
                await LoadThingDescriptionAsync();
                await LoadStateAsync();
            }
            catch (Exception exc)
            {
                Console.WriteLine("[INFO] There is no data about the thing: its new." + exc.Message);
            }
        }

        /// <summary>
        /// This method is called whenever an actor is deactivated after a period of inactivity.
        /// </summary>
        protected override async Task OnDeactivateAsync()
        {
            // Provides Opporunity to perform optional cleanup.
            Console.WriteLine($"[INFO] Deactivating actor id: {this.Id}");
            await SaveThingDescriptionAsync(thingDescription);
            await SaveStateAsync(currentState);

        }

        /// <summary>
        /// Set MyData into actor's private state store
        /// </summary>
        /// <param name="newThingDescription">the user-defined MyData which will be stored into state store as "my_data" state</param>
        private static ThingDescription? CloneJsonElement(ThingDescription newThingDescription)
        {
            var json = JsonSerializer.Serialize(newThingDescription);
            return JsonSerializer.Deserialize<ThingDescription>(json);
        }

        public async Task<string> SetThingDescriptionAsync(string newThingDescription)
        {
            thingDescription = JsonSerializer.Deserialize<ThingDescription>(newThingDescription);
            await SaveThingDescriptionAsync(thingDescription);
            if (thingDescription?.Links != null)
            {
                //string[] eventNames = [.. newThingDescription.Events.Keys.Where(e => !string.IsNullOrWhiteSpace(e))];
                var eventNames = GetSubscribedEvents(thingDescription?.Links);
                await SubscribeToEvents(eventNames);
            }
            await InitializeOrUpdateThingState(thingDescription?.Properties);
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

        public async Task<string> GetThingDescriptionAsync()
        {
            // Gets state from the state store.
            return await Task.FromResult(thingDescription != null ? JsonSerializer.Serialize(thingDescription, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }) : "Thing description is not available.");
        }

        public Task<string> GetCurrentStateAsync()
        {
            // Gets state from the state store.
            return Task.FromResult(JsonSerializer.Serialize(currentState));
        }

        private async Task LoadThingDescriptionAsync()
        {
            var bulkStateItems = await _daprClient.GetBulkStateAsync<string>(StateStoreName, [ThingDescriptionKey + Id.ToString()], parallelism: 1);

            if (bulkStateItems.Count > 0 && !string.IsNullOrEmpty(bulkStateItems[0].Value))
            {
                thingDescription = JsonSerializer.Deserialize<ThingDescription>(bulkStateItems[0].Value);
            }
            else
            {
                Console.WriteLine("[INFO] No ThingDescription found in statestore.");
            }
        }

        private async Task LoadStateAsync()
        {
            var bulkStateItems = await _daprClient.GetBulkStateAsync<Dictionary<string, PropertyState>>(StateStoreName, [CurrentStateKey + Id.ToString()], parallelism: 1);
            foreach (var item in bulkStateItems)
            {
                if (item.Value != null)
                {
                    foreach (var kvp in item.Value)
                    {
                        currentState[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        private async Task SaveThingDescriptionAsync(ThingDescription? newThingDescription)
        {
            thingDescription = newThingDescription;
            await _daprClient.SaveStateAsync(StateStoreName, ThingDescriptionKey + Id.ToString(), JsonSerializer.Serialize(thingDescription));
            await LoadThingDescriptionAsync();
        }

        private async Task SaveStateAsync(Dictionary<string, PropertyState> newState)
        {
            currentState = newState;
            await _daprClient.SaveStateAsync(StateStoreName, CurrentStateKey + Id.ToString(), newState);
        }

        private async Task InitializeOrUpdateThingState(Dictionary<string, PropertyAffordance>? properties)
        {
            if (properties is null)
            {
                currentState = [];
                return;
            }
            var newState = currentState.Where(x => properties.ContainsKey(x.Key)).ToDictionary();
            foreach (var property in properties)
            {
                if (!(newState.TryGetValue(property.Key, out PropertyState? value) && SchemaValidator.IsTypeCompatible(property.Value.DataType, value.Value)))
                {
                    newState[property.Key] = new PropertyState();
                }
            }
            currentState = newState;
            await SaveStateAsync(newState);
        }

        private async Task UpdateState(Dictionary<string, PropertyState> updatedProperties)
        {
            if (thingDescription is null || thingDescription.Properties is null) return;

            foreach (var kvp in updatedProperties)
            {
                string propName = kvp.Key;
                PropertyState newValue = kvp.Value;

                if (!thingDescription.Properties.TryGetValue(propName, out var affordance))
                {
                    Console.WriteLine($"[WARN] Property '{propName}' does not exist in the ThingDescription.");
                    continue;
                }

                if (!SchemaValidator.IsTypeCompatible(affordance.DataType, newValue.Value))
                {
                    Console.WriteLine($"[WARN] Value for '{propName}' is not of the expected type: '{affordance.DataType}'.");
                    continue;
                }

                currentState[propName] = newValue;
                //Console.WriteLine($"[INFO] Property '{propName}' updated to: {newValue}");
            }

            await SaveStateAsync(currentState);
        }

        private async Task SubscribeToEvents(List<EventSubscription> events)
        {
            var client = DaprClient.CreateInvokeHttpClient();
            var cts = new CancellationTokenSource();
            var response = await client.PostAsJsonAsync($"http://events-service/events/things/{Id}", events, cts.Token);
            Console.WriteLine(response.ToString());
        }

        public async Task OnEventReceived(MyCloudEvent<string> eventRecv)
        {
            //Console.WriteLine($"[INFO: {Id}] Received new event");

            if (eventRecv.Type != null)
            {
                JsonObject info = [];
                info["eventName"] = eventRecv.Type;

                JsonNode? payload;
                try
                {
                    payload = (eventRecv.Data is null) ? null : JsonNode.Parse(eventRecv.Data);
                }
                catch
                {
                    payload = null;
                }

                await CheckRules(info, payload);
            }
            //var eventAffordance = IsMyEvent(eventRecv);
            //if (eventAffordance is not null) Console.WriteLine("MI EVENTO: " + eventAffordance.ToString());
            await Task.CompletedTask;
        }

        private async Task CheckRules(JsonObject info, JsonNode? payload)
        {
            if (thingDescription is null || thingDescription.Rules is null) return;
            JsonNode data = GetCurrentStateAsJsonNode(info, payload);

            foreach (var (name, logic) in thingDescription.Rules)
            {
                //bool res = logic.If.All(rule => JsonLogic.Apply(rule, data)?.GetValue<bool>() == true);
                JsonNode? node = logic.If?.AsNode();
                if (node is null) continue;

                bool res = JsonLogic.Apply(node, data)?.GetValue<bool>() == true;
                //bool res = JsonLogic.Apply(node, data)?.GetValue<bool>() == true;
                if (res && logic.Then != null)
                {
                    //Console.WriteLine("[INFO] Executing rule " + name + "...");
                    await ApplyRule(logic.Then, data);
                }
            }

            await Task.CompletedTask;
        }

        private JsonNode GetCurrentStateAsJsonNode(JsonObject info, JsonNode? payload)
        {
            JsonObject json = info?.DeepClone().AsObject() ?? [];
            if (payload != null) json["payload"] = payload;
            foreach (var (propertyName, value) in currentState)
            {
                JsonNode? node = (value is null || value.Value is null) ? null : value.Value?.AsNode();
                if (node is not null) json[propertyName] = node;
            }
            return json;
        }

        private async Task ApplyRule(Then then, JsonNode data)
        {
            if (then.UpdateState != null) await HandleUpdateStateAsync(then.UpdateState, data);
            if (then.InvokeAction != null)
            {
                foreach (ThenInvokeAction action in then.InvokeAction) await HandleInvokeActionAsync(action, data);
            }

            if (then.EmitEvent != null)
            {
                foreach (ThenEmitEvent evnt in then.EmitEvent) await HandleEmitEventAsync(evnt, data);
            }
        }

        private async Task HandleUpdateStateAsync(Dictionary<string, UpdatePropertyState> howToUpdate, JsonNode data)
        {
            //Console.WriteLine("[INFO] Updating state with received values...");
            Dictionary<string, PropertyState> updatedProperties = [];

            foreach (var (propertyName, schema) in howToUpdate)
            {
                JsonElement? computedNewValue = null;
                DateTime? computedTimestamp = null;

                if (schema.NewValue.HasValue)
                {
                    JsonNode? result = JsonLogic.Apply(schema.NewValue.Value.AsNode(), data);
                    if (result is JsonValue resultValue && resultValue.TryGetValue(out JsonElement jsonValue))
                    {
                        computedNewValue = jsonValue;
                    }
                }

                if (computedNewValue != null && schema.Timestamp.HasValue)
                {
                    JsonNode? tsResult = JsonLogic.Apply(schema.Timestamp.Value.AsNode(), data);
                    if (tsResult is JsonValue tsValue &&
                    tsValue.TryGetValue(out JsonElement tsJson))
                    {
                        switch (tsJson.ValueKind)
                        {
                            case JsonValueKind.String:
                                if (DateTime.TryParse(tsJson.GetString(), out DateTime parsedStringTs))
                                    computedTimestamp = parsedStringTs.ToUniversalTime();
                                break;

                            case JsonValueKind.Number:
                                if (tsJson.TryGetInt64(out long unixTimestamp))
                                {
                                    computedTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                                }
                                break;
                        }
                    }
                }

                if (computedNewValue != null)
                {
                    updatedProperties[propertyName] = new PropertyState(computedNewValue.Value, computedTimestamp);
                }
            }

            //Console.WriteLine("[INFO] New values: " + JsonSerializer.Serialize(updatedProperties));
            await UpdateState(updatedProperties);
        }

        private async Task HandleInvokeActionAsync(ThenInvokeAction invokeAction, JsonNode data)
        {
            Console.WriteLine($"[WARNING: {Id}] INVOKE ACTION. NOT IMPLEMENTED");
            await Task.CompletedTask;
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
                payload["thingId"] = Id.ToString();
            }
            else
            {
                payload = new JsonObject();
            }
            //Console.WriteLine(JsonSerializer.Serialize(payload));
            await _daprClient.PublishEventAsync("kafka-pubsub", emitEvent.Event, payload);
        }

        private EventAffordance? IsMyEvent(MyCloudEvent<string> msg)
        {
            // Validación básica del evento recibido
            if (thingDescription?.Events is null || string.IsNullOrEmpty(msg.Type))
            {
                return null;
            }

            var exists = thingDescription.Events.FirstOrDefault(x =>
            {
                if (x.Key != msg.Type) return false;
                if (x.Value.Data is null) return msg.Data is null;
                if (msg.Data is null) return x.Value.Data.Type == "null";
                return SchemaValidator.IsTypeCompatible(x.Value.Data.Type, SchemaValidator.StringToJsonElement(msg.Data));
            });

            return (exists.Key is null) ? null : exists.Value;
        }

        private ActionAffordance? IsMyAction(string name, string parameters)
        {
            // Validación básica del evento recibido
            if (thingDescription?.Actions is null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            var exists = thingDescription.Actions.FirstOrDefault(x =>
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

        public async Task InvokeAction(string action, string parameters)
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
                        if (data is not null) await UpdateState(data);
                    }
                    catch { }
                    break;

                default:
                    throw new ArgumentException($"Unsupported action: {action}");
            }
        }

        public Task SendEvent(MyCloudEvent<string> evnt)
        {
            throw new NotImplementedException();
        }







        /// <summary>
        /// Register MyReminder reminder with the actor
        /// </summary>
        public async Task RegisterReminder()
        {
            await this.RegisterReminderAsync(
                "MyReminder",              // The name of the reminder
                null,                      // User state passed to IRemindable.ReceiveReminderAsync()
                TimeSpan.FromSeconds(5),   // Time to delay before invoking the reminder for the first time
                TimeSpan.FromSeconds(5));  // Time interval between reminder invocations after the first invocation
        }

        /// <summary>
        /// Get MyReminder reminder details with the actor
        /// </summary>
        public async Task<IActorReminder> GetReminder()
        {
            return await this.GetReminderAsync("MyReminder");
        }

        /// <summary>
        /// Unregister MyReminder reminder with the actor
        /// </summary>
        public Task UnregisterReminder()
        {
            Console.WriteLine("Unregistering MyReminder...");
            return UnregisterReminderAsync("MyReminder");
        }

        // <summary>
        // Implement IRemindeable.ReceiveReminderAsync() which is call back invoked when an actor reminder is triggered.
        // </summary>
        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            Console.WriteLine("ReceiveReminderAsync is called!");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Register MyTimer timer with the actor
        /// </summary>
        public Task RegisterTimer()
        {
            return RegisterTimerAsync(
                "MyTimer",                  // The name of the timer
                nameof(this.OnTimerCallBack),       // Timer callback
                null,                       // User state passed to OnTimerCallback()
                TimeSpan.FromSeconds(5),    // Time to delay before the async callback is first invoked
                TimeSpan.FromSeconds(5));   // Time interval between invocations of the async callback
        }

        /// <summary>
        /// Unregister MyTimer timer with the actor
        /// </summary>
        public Task UnregisterTimer()
        {
            Console.WriteLine("Unregistering MyTimer...");
            return this.UnregisterTimerAsync("MyTimer");
        }

        /// <summary>
        /// Timer callback once timer is expired
        /// </summary>
        private Task OnTimerCallBack(byte[] data)
        {
            Console.WriteLine("OnTimerCallBack is called!");
            return Task.CompletedTask;
        }
    }
}