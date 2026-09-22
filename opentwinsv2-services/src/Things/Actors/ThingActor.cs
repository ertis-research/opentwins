using Dapr.Actors.Runtime;
using Dapr.Client;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Infrastructure.Database;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Actors.Services;
using OpenTwinsV2.Things.Services;
using OpenTwinsV2.Shared.Utilities;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Things.Models;
using System.Text.Json;

namespace OpenTwinsV2.Things.Actors
{
    internal class ThingActor : Actor, IThingActor, IRemindable
    {
        private readonly StateManagerService _stateManager;
        private readonly DescriptionManagerService _descriptionManager;
        private readonly ThingLogicManager _logic;
        // private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _thingId;
        private readonly string _actorId;
        public ThingDescription? ThingDescription { get; private set; }
        public Dictionary<string, PropertyState> CurrentState { get; private set; } = [];
        private string lastAccessStateStore = null!;

        public ThingActor(ActorHost host, StateService stateService, StatusManager statusManager, DescriptionManagerService descriptionManager, StateManagerService stateManager)
        : base(host)
        {
            _actorId = Uri.UnescapeDataString(Id.GetId());
            _thingId =  Uri.UnescapeDataString(Uri.UnescapeDataString(_actorId));
            _stateManager = stateManager;
            _descriptionManager = descriptionManager;
            _logic = new ThingLogicManager(_thingId, ThingDescription, CurrentState, stateService, statusManager, _descriptionManager, _stateManager);
        }
        protected override async Task OnActivateAsync()
        {
            ActorLogger.Info(_thingId, "Activating actor");
            try
            {
                ThingDescription = await _descriptionManager.LoadDescriptionAsync(_thingId);
                await _logic.UpdateCurrentThingDescription(ThingDescription);
                CurrentState = await _stateManager.LoadStateAsync(_thingId);
                await _logic.UpdateCurrentState(CurrentState);
                lastAccessStateStore = DateTime.UtcNow.ToString("o");
            }
            catch (Exception exc)
            {
                ActorLogger.Info(_thingId, "There is no data about the thing: its new. " + exc.Message);
            }
        }

        protected override async Task OnDeactivateAsync()
        {
            ActorLogger.Info(_thingId, "Deactivating actor");
            await Task.CompletedTask;
        }

        // public async Task<string> SetThingDescriptionAsync(string newThingDescription, string? operationid = null)
        // {
        //     // return await _logic.SetThingDescriptionAsync(newThingDescription, operationid);
        //     return "success";
        // }

        public async Task<string?> GetThingDescriptionAsync()
        {
            return await _logic.GetThingDescriptionAsync();
        }

        public async Task<string> GetThingStatusAsync()
        {
            return await _logic.GetThingStatusAsync();
        }

        public async Task<string> GetCurrentStateAsync()
        {
            CurrentState = await _logic.GetCurrentState();
            string status = await _logic.GetThingStatusAsync();

            if(status == Status.DeleteStatus) throw new KeyNotFoundException();
            
            return JsonSerializer.Serialize(new StateWrapper<Dictionary<string, PropertyState>>(CurrentState, lastAccessStateStore, isUpdating: status == Status.UpdateStatus));
        }

        public async Task OnEventReceived(MyCloudEvent<string> eventRecv)
        {
            var updated = await _logic.ApplyEventAsync(eventRecv);
            if (updated)
            {
                lastAccessStateStore = DateTime.UtcNow.ToString("o");
                CurrentState =  await _logic.GetCurrentState();
            }
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