using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTwinsV2.Orchestration.Error;

namespace OpenTwinsV2.Orchestration.Clients
{
    public class ThingsClient
    {
        private readonly HttpClient _thingsClient;

        public ThingsClient(HttpClient client)
        {
            _thingsClient = client;
        }

        #region Auxiliar

        private StringContent GetJsonStringContentForPost(JsonNode json)
        {
            string payload;

            // Check if the node is actually a simple value (like a String) 
            // instead of an Object or Array.
            if (json is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var extractedString))
            {
                // It's a string wrapper! Use the inner string directly.
                // This prevents double-serialization (avoiding quotes around the whole JSON).
                payload = extractedString;
            }
            else
            {
                // It's a proper Object or Array. Safe to serialize.
                payload = json.ToJsonString();
            }

            return new StringContent(payload, Encoding.UTF8, "application/json");
        }

        private async Task<JsonNode?> GetJsonFromResponse(HttpContent content)
        {
            var jsonString = await content.ReadAsStringAsync();
            return JsonNode.Parse(jsonString);
        }

        private async Task<T?> SendRequestAsync<T>(HttpMethod method, string uri, object? payload = null)
        {
            var request = new HttpRequestMessage(method, uri);
            
            if (payload != null)
            {
                // Assuming you use System.Net.Http.Json
                // request.Content = JsonContent.Create(payload);ç
                request.Content = GetJsonStringContentForPost(JsonNode.Parse(payload.ToString() ?? "") ?? new JsonObject());
            }

            var response = await _thingsClient.SendAsync(request);

            if(response is null)
                throw new ExternalApiException(HttpStatusCode.InternalServerError, "The client couldn't be reached, the response is null.");
            
            if (!response.IsSuccessStatusCode)
            {
                // 1. Read the error message from the other API
                var errorContent = await response.Content.ReadAsStringAsync();
                
                // 2. Throw your custom exception with the exact status code (e.g., 400)
                throw new ExternalApiException(response.StatusCode, errorContent);
            }

            // Return deserialized success data
            return typeof(T) == typeof(bool) ? (T)(object)true : await response.Content.ReadFromJsonAsync<T>();
        }
        
        #endregion

        #region API Methods

        public async Task<bool> CheckHealth()
        {
            var healthResult = await _thingsClient.GetAsync("health");
            return healthResult is not null && healthResult.IsSuccessStatusCode;
        }

        public async Task<bool> ExitsThing(string thingId)
        {
            try
            {
                await SendRequestAsync<JsonNode>(HttpMethod.Get, $"things/{thingId}");
                return true;
            }catch (ExternalApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<bool> CreateThing(JsonNode thingDescription)
        {
            return await SendRequestAsync<bool>(HttpMethod.Post,"things", thingDescription);
        }

        public async Task<bool> CreateThing(string thingId, JsonNode thingDescription)
        {
            return await SendRequestAsync<bool>(HttpMethod.Put, $"things/{thingId}", thingDescription);
        }

        public async Task<bool> DeleteThing(string thingId)
        {
            return await SendRequestAsync<bool>(HttpMethod.Delete, $"things/{thingId}");
        }

        public async Task<JsonNode?> GetThing(string thingId)
        {
            return await SendRequestAsync<JsonNode>(HttpMethod.Get, $"things/{thingId}");
        }
    }

    #endregion
}