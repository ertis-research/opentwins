using Dapr.Client;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Logging;

namespace OpenTwinsV2.Things.Services
{
    public class EventsService
    {
        private readonly DaprClient _daprClient;

        public EventsService(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }

        public async Task SubscribeToEventsAsync(string thingId, List<EventSubscription> events)
        {
            try
            {
                var escapedId = Uri.EscapeDataString(thingId);
                
                var request = _daprClient.CreateInvokeMethodRequest("events-service", $"events/things/{escapedId}", events);

                using var response = await _daprClient.InvokeMethodWithResponseAsync(request);

                if (response.IsSuccessStatusCode)
                    ActorLogger.Info(thingId, "Successfully subscribed to events.");
                else
                    ActorLogger.Error(thingId, $"events-service returned an error. StatusCode: {(int)response.StatusCode}");
            }
            catch (InvocationException ex)
            {
                ActorLogger.Error(thingId, $"Dapr could not reach 'events-service'. Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ActorLogger.Error(thingId, $"Unexpected error in SubscribeToEventsAsync: {ex.Message}");
            }
        }
    }
}