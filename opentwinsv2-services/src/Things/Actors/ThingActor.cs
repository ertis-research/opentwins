using Dapr.Actors.Runtime;
using Dapr.Client;
using OpenTwinsV2.Things.Models;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Infrastructure.Database;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Actors.Services;

namespace OpenTwinsV2.Things.Actors
{
    internal class ThingActor : Actor, IThingActor, IRemindable
    {
        private static readonly DaprClient _daprClient = new DaprClientBuilder().Build();
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ThingStateManager _stateManager;
        private readonly ThingDescriptionManager _descriptionManager;
        private readonly ThingLogicManager _logic;

        public ThingActor(ActorHost host, IDbConnectionFactory connectionFactory)
        : base(host)
        {
            _connectionFactory = connectionFactory;

            var thingId = Id.GetId();
            _stateManager = new ThingStateManager(_daprClient, thingId);
            _descriptionManager = new ThingDescriptionManager(_daprClient, _connectionFactory, thingId);
            _logic = new ThingLogicManager(_daprClient, thingId, _descriptionManager, _stateManager);
        }

        protected override async Task OnActivateAsync()
        {
            ActorLogger.Info(Id.GetId(), "Activating actor");
            try
            {
                await _descriptionManager.LoadAsync();
                await _stateManager.LoadAsync();
            }
            catch (Exception exc)
            {
                ActorLogger.Info(Id.GetId(), "There is no data about the thing: its new. " + exc.Message);
            }
        }

        protected override async Task OnDeactivateAsync()
        {
            ActorLogger.Info(Id.GetId(), "Deactivating actor");
            await Task.CompletedTask;
        }

        public async Task<string> SetThingDescriptionAsync(string newThingDescription)
        {
            return await _logic.SetThingDescriptionAsync(newThingDescription);
        }

        public Task<string?> GetThingDescriptionAsync()
        {
            return _logic.GetThingDescriptionAsync();
        }

        public Task<string> GetCurrentStateAsync()
        {
            return Task.FromResult(_logic.GetCurrentState());
        }

        public async Task OnEventReceived(MyCloudEvent<string> eventRecv)
        {
            await _logic.ApplyEventAsync(eventRecv);
        }

        public async Task InvokeAction(string action, string parameters)
        {
            await _logic.ApplyInvokeAction(action, parameters);
        }

        public Task SendEvent(MyCloudEvent<string> evnt)
        {
            throw new NotImplementedException();
        }


        public async Task RegisterReminder()
        {
            await this.RegisterReminderAsync(
                "MyReminder",              // The name of the reminder
                null,                      // User state passed to IRemindable.ReceiveReminderAsync()
                TimeSpan.FromSeconds(5),   // Time to delay before invoking the reminder for the first time
                TimeSpan.FromSeconds(5));  // Time interval between reminder invocations after the first invocation
        }

        public async Task<IActorReminder> GetReminder()
        {
            return await this.GetReminderAsync("MyReminder");
        }

        public Task UnregisterReminder()
        {
            Console.WriteLine("Unregistering MyReminder...");
            return UnregisterReminderAsync("MyReminder");
        }

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            Console.WriteLine("ReceiveReminderAsync is called!");
            return Task.CompletedTask;
        }

        public Task RegisterTimer()
        {
            return RegisterTimerAsync(
                "MyTimer",                  // The name of the timer
                nameof(this.OnTimerCallBack),       // Timer callback
                null,                       // User state passed to OnTimerCallback()
                TimeSpan.FromSeconds(5),    // Time to delay before the async callback is first invoked
                TimeSpan.FromSeconds(5));   // Time interval between invocations of the async callback
        }

        public Task UnregisterTimer()
        {
            Console.WriteLine("Unregistering MyTimer...");
            return this.UnregisterTimerAsync("MyTimer");
        }

        private Task OnTimerCallBack(byte[] data)
        {
            Console.WriteLine("OnTimerCallBack is called!");
            return Task.CompletedTask;
        }
    }
}