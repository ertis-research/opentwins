using Dapr.Client;
using System.Text.Json;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Shared.Utilities;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Things.Actors.Services
{
    internal class ThingStateManager
    {
        private const string CurrentStateKey = "CS_";
        private readonly DaprClient _daprClient;
        private readonly string _thingId;
        private const string StateStoreName = "actorstatestore";

        public Dictionary<string, PropertyState> CurrentState { get; private set; } = [];

        public ThingStateManager(DaprClient daprClient, string thingId)
        {
            _daprClient = daprClient;
            _thingId = thingId;
        }

        public async Task LoadAsync()
        {
            var bulkStateItems = await _daprClient.GetBulkStateAsync<Dictionary<string, PropertyState>>(StateStoreName, [CurrentStateKey + _thingId], parallelism: 1);
            foreach (var item in bulkStateItems)
            {
                if (item.Value != null)
                {
                    foreach (var kvp in item.Value)
                        CurrentState[kvp.Key] = kvp.Value;
                }
            }
        }

        public async Task SaveAsync()
        {
            await _daprClient.SaveStateAsync("actorstatestore", CurrentStateKey + _thingId, CurrentState);
            ActorLogger.Info(_thingId, "Current state saved to Redis.");
        }

        public async Task DeleteAsync()
        {
            try
            {
                // Eliminar el estado desde el state store de Dapr
                await _daprClient.DeleteStateAsync(StateStoreName, CurrentStateKey + _thingId);
                ActorLogger.Info(_thingId, "Thing state deleted from statestore.");
            }
            catch (Exception ex)
            {
                ActorLogger.Error(_thingId, $"Error while deleting Thing state from statestore: {ex}");
                throw new InvalidOperationException("Error while deleting Thing state from statestore.", ex);
            }
        }

        public async Task UpdateAsync(Dictionary<string, PropertyState> newProperties, Dictionary<string, PropertyAffordance>? infoProperties)
        {
            if (infoProperties == null) return;

            foreach (var kvp in newProperties)
            {
                string propName = kvp.Key;
                PropertyState newValue = kvp.Value;

                if (!infoProperties.TryGetValue(propName, out var affordance))
                {
                    ActorLogger.Warn(_thingId, $"Property '{propName}' does not exist in the ThingDescription");
                    continue;
                }

                if (!SchemaValidator.IsTypeCompatible(affordance.DataType, newValue.Value))
                {
                    ActorLogger.Warn(_thingId, $"Value for '{propName}' is not of the expected type: '{affordance.DataType}'");
                    continue;
                }

                CurrentState[propName] = newValue;
            }

            await SaveAsync();
        }

        public async Task InitializeFromDescription(Dictionary<string, PropertyAffordance>? props)
        {
            if (props is null)
            {
                CurrentState = [];
                return;
            }

            var newState = CurrentState.Where(x => props.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value);

            foreach (var prop in props)
            {
                if (!(newState.TryGetValue(prop.Key, out PropertyState? value) &&
                        SchemaValidator.IsTypeCompatible(prop.Value.DataType, value.Value)))
                {
                    newState[prop.Key] = new PropertyState();
                }
            }

            CurrentState = newState;
            await SaveAsync();
        }
    }
}