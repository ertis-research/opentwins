using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Utilities;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Things.Services
{
    public class StateManagerService
    {
        private readonly StateService _stateService;
        public StateManagerService(StateService stateService)
        {
            _stateService = stateService;
        }

        public async Task<Dictionary<string, PropertyState>> LoadStateAsync(string thingId)
        {
            Dictionary<string, PropertyState> currentState = [];
            var bulkStateItems = await _stateService.LoadThingBulkState(thingId);
            foreach (var item in bulkStateItems)
            {
                if (item.Value != null)
                {
                    foreach (var kvp in item.Value)
                        currentState[kvp.Key] = kvp.Value;
                }
            }
            return currentState;
        }

        public async Task InitializeStateFromDescription(string thingId, Dictionary<string, PropertyAffordance>? props)
        {
            Dictionary<string, PropertyState> currentState = await LoadStateAsync(thingId);
            if (props is null)
            {
                currentState = [];
                return;
            }

            var newState = currentState.Where(x => props.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value);

            foreach (var prop in props)
            {
                if (!(newState.TryGetValue(prop.Key, out PropertyState? value) &&
                        SchemaValidator.IsTypeCompatible(prop.Value.DataType, value.Value)))
                {
                    newState[prop.Key] = new PropertyState();
                }
            }

            currentState = newState;
            await SaveStateAsync(thingId, currentState);
        }

        public async Task SaveStateAsync(string thingId, Dictionary<string, PropertyState> currentState)
        {
            await _stateService.SaveThingState(thingId, currentState);
            ActorLogger.Info(thingId, "Current state saved to Redis.");
        }

        public async Task DeleteStateAsync(string thingId)
        {
            try
            {
                // Eliminar el estado desde el state store de Dapr
                await _stateService.DeleteThingState(thingId);
                // CurrentState.Clear();
                ActorLogger.Info(thingId, "Thing state deleted from statestore.");
            }
            catch (Exception ex)
            {
                ActorLogger.Error(thingId, $"Error while deleting Thing state from statestore: {ex}");
                throw new InvalidOperationException("Error while deleting Thing state from statestore.", ex);
            }
        }

        public async Task DeleteDescriptionStateAsync(string thingId)
        {
            try
            {
                // Eliminar el estado desde el state store de Dapr
                await _stateService.DeleteThingDescriptionState(thingId);
                // CurrentState.Clear();
                ActorLogger.Info(thingId, "Thing Description deleted from statestore.");
            }
            catch (Exception ex)
            {
                ActorLogger.Error(thingId, $"Error while deleting Thing Description from statestore: {ex}");
                throw new InvalidOperationException("Error while deleting Thing Description from statestore.", ex);
            }
        }

        public async Task UpdateStateAsync(string thingId, Dictionary<string, PropertyState> currentState, Dictionary<string, PropertyState> newProperties, Dictionary<string, PropertyAffordance>? infoProperties)
        {
            if (infoProperties == null) return;

            foreach (var kvp in newProperties)
            {
                string propName = kvp.Key;
                PropertyState newValue = kvp.Value;

                if (!infoProperties.TryGetValue(propName, out var affordance))
                {
                    ActorLogger.Warn(thingId, $"Property '{propName}' does not exist in the ThingDescription");
                    continue;
                }

                if (!SchemaValidator.IsTypeCompatible(affordance.DataType, newValue.Value))
                {
                    ActorLogger.Warn(thingId, $"Value for '{propName}' is not of the expected type: '{affordance.DataType}'");
                    continue;
                }

                currentState[propName] = newValue;
            }

            await SaveStateAsync(thingId, currentState);
        }
    }
}