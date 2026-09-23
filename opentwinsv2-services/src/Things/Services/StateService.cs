using System.Globalization;
using System.Text.Json.Nodes;
using Dapr;
using Dapr.Client;
using Microsoft.Extensions.Options;
using OpenTwinsV2.Shared.Configuration;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Things.Services
{
    public class StateService
    {
        private readonly DaprClient _daprClient;
        private readonly PubSubOptions _pubSub;
        private readonly StateStoreOptions _stateStore;
        private const string ThingDescriptionKey = "TD_";
        private const string CurrentStateKey = "CS_";
        public StateService(DaprClient daprClient, IOptions<PubSubOptions> pubSubOptions, IOptions<StateStoreOptions> stateStoreOptions){
            _daprClient = daprClient;
            _pubSub = pubSubOptions.Value;
            _stateStore = stateStoreOptions.Value;
        }

        #region ThingDescription 

        public async Task<IReadOnlyList<BulkStateItem<string>>> LoadThingDescriptionBulkState(string id)
        {
            return await _daprClient.GetBulkStateAsync<string>(_stateStore.Name, [ThingDescriptionKey + id], parallelism: 1);
        }

        public async Task SaveThingDescriptionState(string id, ThingDescription td)
        {
            await _daprClient.SaveStateAsync(_stateStore.Name, ThingDescriptionKey + id, td.ToString());
        }

        public async Task PublishChangedThingDescriptionEvent(string id, ThingDescription td)
        {
            var metadata = new Dictionary<string, string>() {
                { "cloudevent.source", new Uri(id).ToString() },
                { "cloudevent.type", $"{_pubSub.ThingDescriptionChangesTopic}:" + id}
            };
            await _daprClient.PublishEventAsync(_pubSub.Name, _pubSub.ThingDescriptionChangesTopic, td, metadata);
        }

        public async Task PublishDeletedThingDescriptionEvent(string id)
        {
            var metadata = new Dictionary<string, string>()
            {
                { "cloudevent.source", new Uri(id).ToString() },
                { "cloudevent.type", $"{_pubSub.ThingDescriptionDeletedTopic}:" + id }
            };
            await _daprClient.PublishEventAsync(_pubSub.Name, _pubSub.ThingDescriptionDeletedTopic, id, metadata);
        }

        public async Task DeleteThingDescriptionState(string id)
        {
            await _daprClient.DeleteStateAsync(_stateStore.Name, ThingDescriptionKey + id);
        }

        #endregion

        #region State 

        public async Task<IReadOnlyList<BulkStateItem<Dictionary<string, PropertyState>>>>  LoadThingBulkState(string id)
        {
            return await _daprClient.GetBulkStateAsync<Dictionary<string, PropertyState>>(_stateStore.Name, [CurrentStateKey + id], parallelism: 1);
        }

        public async Task SaveThingState(string id, Dictionary<string, PropertyState> state)
        {
            await _daprClient.SaveStateAsync(_stateStore.Name, CurrentStateKey + id, state);
        }

        public async Task DeleteThingState(string id)
        {
            await _daprClient.DeleteStateAsync(_stateStore.Name, CurrentStateKey + id);
        }

        #endregion

        #region Events

        public async Task PublishEvent(string id, string eventName, JsonObject payload)
        {
            var cloudEvent = new CloudEvent<JsonNode>(payload)
            {
                Source = new Uri(id),
                Type = eventName
            };
            await _daprClient.PublishEventAsync(_pubSub.Name, _pubSub.EventsTopic, cloudEvent);
        }

        #endregion
    }
}