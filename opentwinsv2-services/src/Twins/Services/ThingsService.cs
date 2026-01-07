using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Shared.Models;

namespace OpenTwinsV2.Twins.Services
{
    /// <summary>
    /// Manages petitions that require Things Project.
    /// </summary>
    public class ThingsService
    {
        private readonly DaprClient _daprClient;
        private readonly string _thingServiceAppId = "things-service";
        private const string ActorType = Actors.ThingActor;

        public ThingsService()
        {
            _daprClient = new DaprClientBuilder().Build();
        }

        /// <summary>
        /// Sends petition for creating a new thing.
        /// </summary>
        /// <param name="newThing">The Json Node of the thing to create.</param>
        /// <returns>
        /// Returns true if the operation was successfull<br/>
        /// Returns false if there was any issue while performing the operation.
        /// </returns>
        public async Task<bool> CreateThingAsync(JsonNode newThing)
        {
            var client = DaprClient.CreateInvokeHttpClient();
            var cts = new CancellationTokenSource();
            var response = await client.PostAsJsonAsync($"http://{_thingServiceAppId}/things", newThing, cts.Token);

            return response.IsSuccessStatusCode;
            //var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        }

        /// <summary>
        /// Gets the ThingDescription of the Thing.
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>Returns The Thing Description of the Thing.</returns>
        /// <exception cref="KeyNotFoundException">Is thrown if the Thing could not be found by its identifier.</exception>
        /// <exception cref="InvalidDataException">Is thrown if the ThingDescription obtaind is not valid.</exception>
        public async Task<ThingDescription> GetThingAsync(string thingId)
        {
            var proxy = ActorProxy.Create<IThingActor>(new ActorId(thingId), ActorType);
            var thingDescriptionJson = await proxy.GetThingDescriptionAsync() ?? throw new KeyNotFoundException($"Thing with ID '{thingId}' was not found.");

            ThingDescription? td = JsonSerializer.Deserialize<ThingDescription>(thingDescriptionJson, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            if (td == null || string.IsNullOrEmpty(td.Id)) throw new InvalidDataException("ThingDescription is invalid or missing ID.");

            return td;
        }

        /// <summary>
        /// Gets the state of the Thing.
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>Returns the state of the Thing in Json format.</returns>
        /// <exception cref="KeyNotFoundException">Is thrown if the Thing could not be found by its identifier.</exception>
        public async Task<JsonElement> GetThingState(string thingId)
        {
            var proxy = ActorProxy.Create<IThingActor>(new ActorId(thingId), ActorType);
            var stateJson = await proxy.GetCurrentStateAsync() ?? throw new KeyNotFoundException($"Thing with ID '{thingId}' was not found.");

            using var doc = JsonDocument.Parse(stateJson);
            return doc.RootElement.Clone();
        }

        /// <summary>
        /// Gets the states of each Thing in the list provided.
        /// </summary>
        /// <param name="thingIds">The list of identifiers of Things.</param>
        /// <returns>Returns a Dictionary with the states of each Thing, being the key the identifier of each Thing.</returns>
        public async Task<Dictionary<string, JsonElement>> GetThingsStatesAsync(List<string> thingIds)
        {
            var result = new Dictionary<string, JsonElement>();

            foreach (var id in thingIds)
            {
                try
                {
                    var state = await GetThingState(id);
                    result[id] = state;
                }
                catch (KeyNotFoundException)
                {
                    Console.WriteLine($"ThingId '{id}' state not found.");
                }
            }

            return result;
        }

    }
}