using System.Text.Json.Nodes;
using Dapr.Client;
using Microsoft.Extensions.Options;
using OpenTwinsV2.Shared.Configuration;
using OpenTwinsV2.Shared.Constants;

namespace OpenTwinsV2.Shared.Utilities
{
    public class StatusManager
    {
        private readonly DaprClient _daprClient;
        private readonly StateStoreOptions _stateStoreConfig;
        public StatusManager(DaprClient daprClient, IOptions<StateStoreOptions> stateStoreOptions)
        {
            _daprClient = daprClient;
            _stateStoreConfig = stateStoreOptions.Value;
        }
        private async Task SaveThingStatus(string id, string state, string operationId, bool ttlEnabled = false)
        {
            if (ttlEnabled)
            {
                //Load from appsettings the no of seconds to wait till it's deleted.
                var metadata = new Dictionary<string,string>{ { "ttlInSeconds", _stateStoreConfig.TtlSeconds } };
                await _daprClient.SaveStateAsync(_stateStoreConfig.Name, Status.ThingStatusKey + id, new JsonObject{["status"]=state, ["operationId"]=operationId}, metadata: metadata);
            }else
                await _daprClient.SaveStateAsync(_stateStoreConfig.Name, Status.ThingStatusKey + id, new JsonObject{["status"]=state, ["operationId"]=operationId});
        }

        public async Task SaveOkThingStatus(string id)
        {
            await SaveThingStatus(id, Status.OkStatus, "");
        }
        
        public async Task SaveCreatingThingStatus(string id, string operationId)
        {
            await SaveThingStatus(id, Status.CreateStatus, operationId);
        } 

        public async Task SaveUpdatingThingStatus(string id, string operationId)
        {
            await SaveThingStatus(id, Status.UpdateStatus, operationId);
        } 

        public async Task SaveDeletingThingStatus(string id, string operationId)
        {
            //TODO: EXPIRE PERDIOD
            await SaveThingStatus(id, Status.DeleteStatus, operationId, ttlEnabled: true);
        } 

        public async Task<(string? Status, string OperationId)> GetThingStatus(string id)
        {
            var state = await _daprClient.GetStateAsync<JsonObject>(_stateStoreConfig.Name, Status.ThingStatusKey + id);
            return (state?["status"]?.GetValue<string>(), state?["operationId"]?.GetValue<string>() ?? "");
        }

        

        public async Task  DeleteThingStatus(string id)
        {
            await _daprClient.DeleteStateAsync(_stateStoreConfig.Name,  Status.ThingStatusKey + id);
        }
    }
}