using System.Text.Json.Nodes;
using Dapr.Client;
using OpenTwinsV2.Shared.Constants;

namespace OpenTwinsV2.Shared.Utilities
{
    public class StatusManager
    {
        private readonly DaprClient _daprClient;
        public StatusManager(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }
        private async Task SaveThingStatus(string id, string state, string operationId)
        {
            await _daprClient.SaveStateAsync(StateStore.Name, Status.ThingStatusKey + id, new JsonObject{["status"]=state, ["operationId"]=operationId});
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
            await SaveThingStatus(id, Status.DeleteStatus, operationId);
        } 

        public async Task<(string? Status, string OperationId)> GetThingStatus(string id)
        {
            var state = await _daprClient.GetStateAsync<JsonObject>(StateStore.Name, Status.ThingStatusKey + id);
            return (state?["status"]?.GetValue<string>(), state?["operationId"]?.GetValue<string>() ?? "");
        }

        

        public async Task  DeleteThingStatus(string id)
        {
            await _daprClient.DeleteStateAsync(StateStore.Name,  Status.ThingStatusKey + id);
        }
    }
}