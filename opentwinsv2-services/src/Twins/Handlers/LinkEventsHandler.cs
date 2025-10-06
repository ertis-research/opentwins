using OpenTwinsV2.Twins.Builders;
using OpenTwinsV2.Twins.Services;
using Dapr.Messaging.PublishSubscribe;
using System.Text.Json;
using OpenTwinsV2.Shared.Models;
using System.Text;
using System.Text.Json.Nodes;
using Dapr;
using Json.More;

namespace OpenTwinsV2.Twins.Handlers
{
    public class LinkEventsHandler
    {
        private readonly ILogger<LinkEventsHandler> _logger;
        private readonly DGraphService _dgraphService;

        public LinkEventsHandler(ILogger<LinkEventsHandler> logger, DGraphService dgraphService)
        {
            _logger = logger;
            _dgraphService = dgraphService;
        }

        public async Task HandleAsync(Link link, string source, string eventType)
        {
            string thingIdTarget = link.Href.ToString();
            string thingIdSource = source;

            JsonArray mutations = [];

            switch (eventType.ToLowerInvariant())
            {
                case "link.added":
                    var addResult = await HandleAddLink(link, thingIdSource, thingIdTarget);
                    if (addResult != null) mutations.Add(addResult);
                    break;
                case "link.updated":
                    var delUpdate = await HandleDeleteLink(link, thingIdSource, thingIdTarget);
                    if (delUpdate != null) mutations.Add(delUpdate);
                    var addUpdate = await HandleAddLink(link, thingIdSource, thingIdTarget);
                    if (addUpdate != null) mutations.Add(addUpdate);
                    break;
                case "link.removed":
                    var delResult = await HandleDeleteLink(link, thingIdSource, thingIdTarget);
                    if (delResult != null) mutations.Add(delResult);
                    break;
                default:
                    _logger.LogWarning("Unknown event type: {EventType}", eventType);
                    break;
            }

            _logger.LogDebug("Mutations: {Mutations}", JsonSerializer.Serialize(mutations));

            await _dgraphService.AddEntitiesAsync(mutations);
            _logger.LogInformation("Link event processed successfully");
        }

        private async Task<JsonObject?> HandleAddLink(Link link, string thingIdSource, string thingIdTarget)
        {
            var uids = await _dgraphService.GetUidsByThingIdsAsync([thingIdTarget, thingIdSource]);
            if (uids.TryGetValue(thingIdTarget, out var uidTarget) && uids.TryGetValue(thingIdSource, out var uidSource))
            {
                _logger.LogDebug("uidTarget: {uidTarget}, uidSource: {uidSource}", uidTarget, uidSource);
                return ThingBuilder.BuildRelation(link, uidTarget, uidSource);
            }
            return null;
        }

        private async Task<JsonObject?> HandleDeleteLink(Link link, string thingIdSource, string thingIdTarget)
        {
            if (link.Rel != null)
            {
                var uid = await _dgraphService.GetRelationUidByThingIdsAsync(thingIdSource, thingIdTarget, link.Rel);
                if (uid != null)
                {
                    return ThingBuilder.BuildDeleteRelation(uid);
                }
            }
            return null;
        }
    }
}