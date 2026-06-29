using System.Text.Json;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Utilities;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Things.Services
{
    public class ThingsManagerService
    {
        private readonly StateManagerService _stateManager;
        private readonly StatusManager _statusManager;
        private readonly EventsService _eventsService;
        private readonly TwinsService _twinsService;
        private readonly DescriptionManagerService _descManager;

        public ThingsManagerService(StateManagerService stateManager, StatusManager statusManager, EventsService eventsService, TwinsService twinsService, DescriptionManagerService descManager)
        {
            _statusManager = statusManager;
            _eventsService = eventsService;
            _stateManager = stateManager;
            _twinsService = twinsService;
            _descManager = descManager;
        }

        public async Task<string> SaveThing(string thingId, string json, string? operationId = null)
        {
            var state = await _statusManager.GetThingStatus(thingId);
            if(operationId is not null && state.OperationId != operationId)
            {
                if(state.Status == Status.DeleteStatus)
                    throw new KeyNotFoundException();
                else if (state.Status == Status.UpdateStatus || state.Status == Status.CreateStatus)
                    throw new InvalidOperationException();
            }
            
            var td = JsonSerializer.Deserialize<ThingDescription>(json)
                ?? throw new InvalidOperationException("Invalid ThingDescription");

            await _descManager.SaveDescriptionAsync(thingId, td);
            await _stateManager.InitializeStateFromDescription(thingId, td.Properties);
            await _stateManager.DeleteDescriptionStateAsync(thingId);

            await UpdateSubscribedEvents(thingId, td);

            return "Success";
        }

        public async Task UpdateSubscribedEvents(string thingId, ThingDescription td)
        {
            if (td != null)
            {
                var events = GetSubscribedEvents(td.SubscribedEvents);
                if (events.Count >= 0)
                    await _eventsService.SubscribeToEventsAsync(thingId, events);
            }
        }

        private List<EventSubscription> GetSubscribedEvents(List<SubscribedEvent>? subscribedEvents)
        {
            return subscribedEvents?.Select(ev => new EventSubscription(ev.Event, ev.AutoEmitState)).ToList() ?? [];
        }

        #region Modify

        public async Task<string> AddLinkAsync(string thingId, string linkJson)
        {
            if(string.IsNullOrWhiteSpace(linkJson))
                throw new ArgumentException("Link JSON cannot be null or empty.", nameof(linkJson));

            var td = await _descManager.LoadDescriptionAsync(thingId) ?? throw new KeyNotFoundException("The Thing does not exist");

            List<Link>? newLinks;
            try
            {
                newLinks = JsonSerializer.Deserialize<List<Link>>(linkJson);
            }
            catch (JsonException ex) { throw new ArgumentException("Invalid link JSON format.", ex); }

            if (newLinks is null) throw new ArgumentException("The link is invalid.");

            //everything ok up to this point, delete from caché
            // await _stateManager.DeleteDescriptionStateAsync(thingId);

            td.Links ??= [];

            foreach(var newLink in newLinks)
            {
                if (td.Links.Any(l => l.Href == newLink.Href && l.Rel == newLink.Rel)) throw new InvalidOperationException("A link with the same Href and Rel already exists.");
                td.Links.Add(newLink);
            }

            // await _descManager.SaveDescriptionAsync(thingId, td, modifyInTwins: true);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true, // Adds line breaks and spaces
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            await SaveThing(thingId, JsonSerializer.Serialize(td, options));

            return td.ToString();
        }

        public async Task<string> UpdateLinkAsync(string thingId, string targetId, string relName, string linkJson)
        {
            if(string.IsNullOrWhiteSpace(linkJson))
                throw new ArgumentException("Link JSON cannot be null or empty.", nameof(linkJson));

            var td = await _descManager.LoadDescriptionAsync(thingId) ?? throw new KeyNotFoundException("The Thing does not exist");

            Link? newLink;
            try
            {
                newLink = JsonSerializer.Deserialize<Link>(linkJson);
            }
            catch (JsonException ex) { throw new ArgumentException("Invalid link JSON format.", ex); }

            if (newLink is null) throw new ArgumentException("The link is invalid.");

            int index = td?.Links?.FindIndex(l => l.Href.ToString() == targetId && l.Rel == relName) ?? throw new KeyNotFoundException("Link not found");
            td.Links[index] = newLink;

            await _descManager.SaveDescriptionAsync(thingId, td, modifyInTwins: false);
            await _twinsService.UpdateLinkInTwins(thingId, targetId, relName, newLink);

            ActorLogger.Info(thingId, $"Published update link event.");

            return td.ToString();
        }

        public async Task<string> AddSubscriptionAsync(string thingId, string json)
        {
            if(string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Link JSON cannot be null or empty.", nameof(json));

            var td = await _descManager.LoadDescriptionAsync(thingId) ?? throw new KeyNotFoundException("The Thing does not exist");

            SubscribedEvent? newSubscription;
            try
            {
                newSubscription = JsonSerializer.Deserialize<SubscribedEvent>(json);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid subscription JSON format.", ex);
            }

            if (newSubscription is null)
                throw new ArgumentException("The subscription is invalid.");

            td.SubscribedEvents ??= [];
            
            var existing = td.SubscribedEvents.FirstOrDefault(s => s.Event == newSubscription.Event);
            if (existing != null) td.SubscribedEvents.Remove(existing);

            td.SubscribedEvents.Add(newSubscription);
            await _descManager.SaveDescriptionAsync(thingId, td, modifyInTwins: false);
            ActorLogger.Info(thingId, $"Added '{newSubscription.Event}' subscription event.");

            return td.ToString()!;
        }

        public async Task<string> RemoveSubscriptionAsync(string thingId, string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Subscription ID cannot be null or empty.");

            var td = await _descManager.LoadDescriptionAsync(thingId) ?? throw new KeyNotFoundException("The Thing does not exist");

            if (td.SubscribedEvents is null || td.SubscribedEvents.Count == 0)
                throw new KeyNotFoundException($"No subscriptions found for ThingId {thingId}.");    

            var subscription = td.SubscribedEvents.FirstOrDefault(s => s.Event == eventName) ?? throw new KeyNotFoundException($"Subscription '{eventName}' not found in ThingId {thingId}.");

            td.SubscribedEvents.Remove(subscription);
            await _descManager.SaveDescriptionAsync(thingId, td, modifyInTwins: false);
            ActorLogger.Info(thingId, $"Published 'removed' subscription event for target '{eventName}'.");

            return td.ToString()!;
        }

        public async Task RemoveLinkAsync(string thingId, string targetId, string relName)
        {
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(relName)) throw new ArgumentException("TargetId or Rel cannot be null or empty.");

            var td = await _descManager.LoadDescriptionAsync(thingId) ?? throw new KeyNotFoundException("The Thing does not exist");

            if (td?.Links == null || td.Links.Count == 0) throw new KeyNotFoundException("No links available.");

            int index = td.Links.FindIndex(l => l.Href.ToString() == targetId && l.Rel == relName);
            
            if (index < 0) throw new KeyNotFoundException($"Link with Href='{targetId}' and Rel='{relName}' not found.");

            var link = td.Links[index];
            td.Links.RemoveAt(index);
            await _descManager.SaveDescriptionAsync(thingId, td, modifyInTwins: false);
            await _twinsService.RemoveLinksInTwins(thingId, [link]);

            ActorLogger.Info(thingId, $"Published delete link event.");
        }

        #endregion

        #region Delete

        public async Task<bool> DeleteThingAsync(string thingId, string? operationid = null)
        {
            try
            {
                if (await _descManager.LoadDescriptionAsync(thingId) == null) return false;

                await _descManager.DeleteDescriptionAsync(thingId);
                ActorLogger.Info(thingId, "Thing description deleted successfully.");

                // await _stateManager.DeleteAsync();
                await _stateManager.DeleteStateAsync(thingId);
                ActorLogger.Info(thingId, "Thing state deleted successfully.");

                return true;
            }
            catch (Exception ex)
            {
                ActorLogger.Error(thingId, $"Error while deleting Thing: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}