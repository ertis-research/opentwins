using System.Text.Json;
using Dapr.Actors.Runtime;
using Dapr.Client;
using OpenTwinsv2.Things.Interfaces;
using OpenTwinsv2.Things.Models;

namespace OpenTwinsv2.Things.Services
{
    internal class ThingActor : Actor, IThingActor, IRemindable
    {
        private const string ThingDescriptionKey = "TD_";
        private const string CurrentStateKey = "CS_";
        private const string RulesKey = "Rules_";
        private ThingDescription? td;

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
        protected override Task OnActivateAsync()
        {
            // Provides opportunity to perform some optional setup.
            Console.WriteLine($"Activating actor id: {this.Id}");
            // If no exists, initializ

            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called whenever an actor is deactivated after a period of inactivity.
        /// </summary>
        protected override Task OnDeactivateAsync()
        {
            // Provides Opporunity to perform optional cleanup.
            Console.WriteLine($"Deactivating actor id: {this.Id}");
            if (td is not null)
            {
                return StateManager.SetStateAsync<string>(
                    Id.ToString(),  // state name
                    td.ToString());
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Set MyData into actor's private state store
        /// </summary>
        /// <param name="data">the user-defined MyData which will be stored into state store as "my_data" state</param>
        public async Task<string> SetThingDescriptionAsync(ThingDescription data)
        {
            // Data is saved to configured state store implicitly after each method execution by Actor's runtime.
            // Data can also be saved explicitly by calling this.StateManager.SaveStateAsync();
            // State to be saved must be DataContract serializable.
            Console.WriteLine(data.ToString());
            Console.WriteLine(Id.ToString());
            td = data;
            if (td.Events != null) SubscribeToEvents(td.Events);
            await StateManager.SetStateAsync<string>(
                Id.ToString(),  // state name
                data.ToString());      // data saved for the named state "my_data"

            return "Success";
        }

        private async void SubscribeToEvents(Dictionary<string, EventAffordance> events)
        {
            string[] eventNames = [.. events.Keys.Where(e => !string.IsNullOrWhiteSpace(e))];
            Console.WriteLine(JsonSerializer.Serialize(eventNames));
            // Llama al servicio "events-service" registrado en Dapr
            /*await _daprClient.InvokeMethodAsync(
                HttpMethod.Post,
                "events-service",
                $"events/things/{Id}",
                eventNames
            );*/
            var client = DaprClient.CreateInvokeHttpClient();
            var cts = new CancellationTokenSource();
            //HttpResponseMessage responde = await client.GetAsync("http://events-service/events/test", cts.Token);
            Console.WriteLine($"http://events-service/events/things/{Id}");
            var response = await client.PostAsJsonAsync($"http://events-service/events/things/{Id}", eventNames, cts.Token);

            Console.WriteLine(response.ToString());
        }

        /// <summary>
        /// Get MyData from actor's private state store
        /// </summary>
        /// <return>the user-defined MyData which is stored into state store as "my_data" state</return>
        public async Task<string> GetThingDescriptionAsync()
        {
            // Gets state from the state store.
            return (td is null) ? await StateManager.GetStateAsync<string>(Id.ToString()) : td.ToString();
        }
        /*
                public async Task HandleExternalEventAsync(ICloudEvent evnt)
                {
                    if (td is null || td.Events is null) return;
                    var eventName = evnt.Type;
                    if (!td.Events.TryGetValue(eventName, out var eventAffordance)) return;
                    await ExecuteEventLogicAsync(eventAffordance, evnt.Data);
                }

                private async Task ExecuteEventLogicAsync(EventAffordance eventAffordance, object? eventData)
                {
                    Console.WriteLine(eventAffordance.Description);
                    if (eventAffordance.DataResponse != null)
                    {

                    }
                    // También podrías actualizar propiedades o emitir otros eventos
                    if (eventAffordance. != null)
                    {
                        foreach (var prop in eventAffordance.PropertiesToUpdate)
                        {
                            await UpdatePropertyAsync(prop.Key, prop.Value);
                        }
                    }
                }
        */
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

        public Task OnEventReceived(object evnt)
        {
            Console.WriteLine($"{Id}: ME HA LLEGADO ALGO REY");
            Console.WriteLine(evnt);
            return Task.CompletedTask;
        }

        public Task InvokeAction(string actionName, object parameters)
        {
            throw new NotImplementedException();
        }

        public Task SendEvent(ICloudEvent evnt)
        {
            throw new NotImplementedException();
        }
    }
}