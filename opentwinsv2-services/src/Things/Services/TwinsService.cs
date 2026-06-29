using System.Text.Json;
using Dapr.Client;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Logging;

namespace OpenTwinsV2.Things.Services
{
    public class TwinsService
    {
        private readonly DaprClient _daprClient;

        public TwinsService(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }
        
        public async Task<bool> ExistsThingInTwins(string id)
        {
            return await _daprClient.InvokeMethodAsync<bool>(HttpMethod.Get, "twins-service", $"internal/things/{Uri.EscapeDataString(id)}");
        }

        public async Task RemoveLinkInTwins(string id, Link link)
        {
            await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{Uri.EscapeDataString(id)}/links/{link.Rel}/{link.Href}");
        }

        public async Task RemoveLinksInTwins(string id, IEnumerable<Link> links)
        {
            await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{Uri.EscapeDataString(id)}/links", JsonSerializer.Serialize(links));
        }

        public async Task AddLinksInTwins(string id, IEnumerable<Link> links)
        {
            await _daprClient.InvokeMethodAsync(HttpMethod.Post, "twins-service", $"internal/things/{Uri.EscapeDataString(id)}/links", JsonSerializer.Serialize(links));
        }

        public async Task UpdateLinkInTwins(string id, string targetId, string relName, Link newLink)
        {
            await _daprClient.InvokeMethodAsync(HttpMethod.Put, "twins-service", $"internal/things/{Uri.EscapeDataString(id)}/links/{relName}/{targetId}", JsonSerializer.Serialize(newLink));
        }

        public async Task RemoveTypeInTwins(string id, string type)
        {
            await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{Uri.EscapeDataString(id)}/type/{Uri.EscapeDataString(type)}");
        }

        public async Task AddTypeInTwins(string id, string type)
        {
            await _daprClient.InvokeMethodAsync(HttpMethod.Post, "twins-service", $"internal/things/{Uri.EscapeDataString(id)}/type/{Uri.EscapeDataString(type)}");
        }

        public async Task DeleteThingInTwins(string id)
        {
            await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{id}");
        }

        public async Task UpdateThingInTwins(ThingDescription newTd, ThingDescription? prevTd)
        {
            var thingId = newTd.Id!;
            var existsInDGraph = await ExistsThingInTwins(thingId);
            if(prevTd is not null)
            {
                var newLinks = newTd.Links ?? Enumerable.Empty<Link>();
                var oldLinks = prevTd.Links ?? Enumerable.Empty<Link>();

                var linksToDelete = oldLinks.Except(newLinks);
                var linksToAdd = newLinks.Except(oldLinks);

                // foreach(var link in linksToDelete)
                //     await RemoveLinkInTwins(thingId, link);
                
                // foreach(var link in linksToAdd)
                //     await RemoveLinkInTwins(thingId, link);

                await RemoveLinksInTwins(thingId, linksToDelete);
                await AddLinksInTwins(thingId, linksToAdd);

                var newTypes = newTd.TypeAnnotation ?? Enumerable.Empty<string>();
                var oldTypes = prevTd.TypeAnnotation ?? Enumerable.Empty<string>();

                var typesToDelete = oldTypes.Except(newTypes);
                var typesToAdd = newTypes.Except(oldTypes);

                if (existsInDGraph)
                {
                    foreach(var type in typesToDelete)
                        await RemoveTypeInTwins(thingId, type);
                    foreach(var type in typesToAdd)
                        await AddTypeInTwins(thingId, type);
                }
            }
            else
                try
                {
                    if(existsInDGraph)
                        await AddLinksInTwins(thingId, newTd.Links ?? []);
                        
                }catch(Exception ex)
                {
                    ActorLogger.Warn(thingId, $"Tried to add a link, but failed: {ex.Message}");
                }
            
        }
    }
}