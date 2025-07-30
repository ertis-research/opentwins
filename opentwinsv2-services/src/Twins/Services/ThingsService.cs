using System.Text.Json;
using System.Text.Json.Nodes;
using Api;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Twins.Services
{
    public class ThingsService
    {
        private readonly DaprClient _daprClient;
        private readonly string _thingServiceAppId = "things-service";
        private const string ActorType = Actors.ThingActor;

        public ThingsService()
        {
            _daprClient = new DaprClientBuilder().Build();
        }

        public async Task<bool> CreateThingAsync(JsonNode newThing)
        {
            var client = DaprClient.CreateInvokeHttpClient();
            var cts = new CancellationTokenSource();
            var response = await client.PostAsJsonAsync($"http://{_thingServiceAppId}/things", newThing, cts.Token);

            return response.IsSuccessStatusCode;
            //var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        }

        public async Task<ThingDescription> GetThingAsync(string thingId)
        {
            var proxy = ActorProxy.Create<IThingActor>(new ActorId(thingId), ActorType);
            var thingDescriptionJson = await proxy.GetThingDescriptionAsync() ?? throw new KeyNotFoundException($"Thing with ID '{thingId}' was not found.");

            ThingDescription? td = JsonSerializer.Deserialize<ThingDescription>(thingDescriptionJson);
            if (td == null || string.IsNullOrEmpty(td.Id)) throw new InvalidDataException("ThingDescription is invalid or missing ID.");

            return td;
        }
    }
}