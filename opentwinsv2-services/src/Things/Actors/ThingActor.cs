using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr;
using Dapr.Actors.Runtime;
using Dapr.Client;
using OpenTwinsv2.Things.Interfaces;
using OpenTwinsv2.Things.Models;
using Shared.Models;
using Shared.Utilities;

namespace OpenTwinsv2.Things.Services
{
    internal class ThingActor : Actor, IThingActor, IRemindable
    {
        private const string ThingDescriptionKey = "TD_";
        private const string CurrentStateKey = "CS_";
        private const string RulesKey = "Rules_";
        private Dictionary<string, object?> currentState = [];
        private ThingDescription? thingDescription;

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
            Console.WriteLine($"Activating actor id: {this.Id}");
            try
            {
                await LoadThingDescriptionAsync();
                await LoadStateAsync();
            }
            catch
            {
                Console.WriteLine("ES NUEVO");
            }
        }

        /// <summary>
        /// This method is called whenever an actor is deactivated after a period of inactivity.
        /// </summary>
        protected override async Task OnDeactivateAsync()
        {
            // Provides Opporunity to perform optional cleanup.
            Console.WriteLine($"Deactivating actor id: {this.Id}");
            await SaveThingDescriptionAsync(thingDescription);
            await SaveStateAsync(currentState);

        }

        /// <summary>
        /// Set MyData into actor's private state store
        /// </summary>
        /// <param name="newThingDescription">the user-defined MyData which will be stored into state store as "my_data" state</param>
        public async Task<string> SetThingDescriptionAsync(ThingDescription newThingDescription)
        {
            thingDescription = newThingDescription;
            if (newThingDescription.Events != null) await SubscribeToEvents(newThingDescription.Events);
            await InitializeOrUpdateThingState(newThingDescription.Properties);
            return "Success";
        }

        public async Task<string> GetThingDescriptionAsync()
        {
            // Gets state from the state store.
            return await Task.FromResult(thingDescription != null ? thingDescription.ToString() : "Thing description is not available.");
        }

        public async Task<Dictionary<string, object?>> GetCurrentStateAsync()
        {
            // Gets state from the state store.
            return (currentState is null) ? await StateManager.GetStateAsync<Dictionary<string, object?>>(CurrentStateKey + Id.ToString()) : currentState;
        }

        private async Task LoadThingDescriptionAsync()
        {
            thingDescription = await StateManager.GetStateAsync<ThingDescription>(ThingDescriptionKey + Id.ToString());
        }

        private async Task LoadStateAsync()
        {
            currentState = await StateManager.GetStateAsync<Dictionary<string, object?>>(CurrentStateKey + Id.ToString());
        }

        private async Task SaveThingDescriptionAsync(ThingDescription? newThingDescription)
        {
            thingDescription = newThingDescription;
            await StateManager.SetStateAsync(ThingDescriptionKey + Id.ToString(), newThingDescription);
        }

        private async Task SaveStateAsync(Dictionary<string, object?> newState)
        {
            currentState = newState;
            await StateManager.SetStateAsync(CurrentStateKey + Id.ToString(), newState);
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
                if (!(newState.TryGetValue(property.Key, out object? value) && SchemaValidator.IsTypeCompatible(property.Value.DataType, value)))
                {
                    newState[property.Key] = null;
                }
            }
            currentState = newState;
            await Task.CompletedTask;
        }

        private async Task UpdateState(Dictionary<string, object?> updatedProperties)
        {
            if (thingDescription is null || thingDescription.Properties is null) return;

            foreach (var kvp in updatedProperties)
            {
                string propName = kvp.Key;
                object? newValue = kvp.Value;

                if (!thingDescription.Properties.TryGetValue(propName, out var affordance))
                {
                    Console.WriteLine($"[WARN] Property '{propName}' does not exist in the ThingDescription.");
                    continue;
                }

                if (!SchemaValidator.IsTypeCompatible(affordance.DataType, newValue))
                {
                    Console.WriteLine($"[WARN] Value for '{propName}' is not of the expected type: '{affordance.Type}'.");
                    continue;
                }

                currentState[propName] = newValue;
                Console.WriteLine($"[INFO] Property '{propName}' updated to: {newValue}");
            }

            await Task.CompletedTask;
        }

        private async Task SubscribeToEvents(Dictionary<string, EventAffordance> events)
        {
            string[] eventNames = [.. events.Keys.Where(e => !string.IsNullOrWhiteSpace(e))];
            var client = DaprClient.CreateInvokeHttpClient();
            var cts = new CancellationTokenSource();
            var response = await client.PostAsJsonAsync($"http://events-service/events/things/{Id}", eventNames, cts.Token);
            Console.WriteLine(response.ToString());
        }

        public async Task OnEventReceived(MyCloudEvent<string> eventRecv)
        {
            Console.WriteLine(eventRecv);
            var eventAffordance = IsMyEvent(eventRecv);
            if (eventAffordance is not null) Console.WriteLine("MI EVENTO: " + eventAffordance.ToString());
            await Task.CompletedTask;
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
                return SchemaValidator.IsTypeCompatible(x.Value.Data.Type, SchemaValidator.DetectJsonElementType(msg.Data));
            });

            return (exists.Key is null) ? null : exists.Value;
        }

        public Task InvokeAction(string actionName, object parameters)
        {
            throw new NotImplementedException();
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