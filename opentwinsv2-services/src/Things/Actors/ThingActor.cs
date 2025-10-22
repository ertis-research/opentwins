using Dapr.Actors.Runtime;
using Dapr.Client;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Infrastructure.Database;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Actors.Services;

namespace OpenTwinsV2.Things.Actors
{
    internal class ThingActor : Actor, IThingActor, IRemindable
    {
        private readonly DaprClient _daprClient = new DaprClientBuilder().Build();
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ThingStateManager _stateManager;
        private readonly ThingDescriptionManager _descriptionManager;
        private readonly ThingLogicManager _logic;
        private readonly string _thingId;

        public ThingActor(ActorHost host, IDbConnectionFactory connectionFactory)
        : base(host)
        {
            _connectionFactory = connectionFactory;

            _thingId = Id.GetId();
            _stateManager = new ThingStateManager(_daprClient, _thingId);
            _descriptionManager = new ThingDescriptionManager(_daprClient, _connectionFactory, _thingId);
            _logic = new ThingLogicManager(_daprClient, _thingId, _descriptionManager, _stateManager);
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

        public async Task<string?> GetThingDescriptionAsync()
        {
            return await _logic.GetThingDescriptionAsync();
        }

        public async Task<bool> DeleteThingAsync()
        {
            try
            {
                if (_descriptionManager.ThingDescription == null) return false;

                await _descriptionManager.DeleteAsync();
                ActorLogger.Info(_thingId, "Thing description deleted successfully.");

                await _stateManager.DeleteAsync();
                ActorLogger.Info(_thingId, "Thing state deleted successfully.");

                return true;
            }
            catch (Exception ex)
            {
                ActorLogger.Error(_thingId, $"Error while deleting Thing: {ex.Message}");
                return false;
            }
        }

        public async Task<string> AddLinkAsync(string v)
        {
            return await _descriptionManager.AddLinkAsync(v);
        }

        public async Task<string> UpdateLinkAsync(string targetId, string relName, string newLink)
        {
            return await _descriptionManager.UpdateLinkAsync(targetId, relName, newLink);
        }

        public async Task RemoveLinkAsync(string href, string relName)
        {
            await _descriptionManager.RemoveLinkAsync(href, relName);
        }

        public async Task<string> AddSubscriptionAsync(string v)
        {
            var res = await _descriptionManager.AddSubscriptionAsync(v);
            await _logic.UpdateSubscribedEvents();
            return res;
        }

        public async Task<string> RemoveSubscriptionAsync(string eventName)
        {
            var res = await _descriptionManager.RemoveSubscriptionAsync(eventName);
            await _logic.UpdateSubscribedEvents();
            return res;
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