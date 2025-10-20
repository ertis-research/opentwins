using OpenTwinsV2.Twins.Builders;
using OpenTwinsV2.Twins.Services;
using OpenTwinsV2.Shared.Models;

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

        public async Task HandleAddLinkAsync(string thingIdSource, Link link)
        {
            var thingIdTarget = link.Href.ToString();
            var uids = await _dgraphService.GetUidsByThingIdsAsync([thingIdTarget, thingIdSource]);
            if (uids.TryGetValue(thingIdTarget, out var uidTarget) && uids.TryGetValue(thingIdSource, out var uidSource))
            {
                _logger.LogDebug("uidTarget: {uidTarget}, uidSource: {uidSource}", uidTarget, uidSource);
                var mutation = ThingBuilder.BuildRelation(link, uidTarget, uidSource);
                await _dgraphService.AddEntitiesAsync([mutation]);
            }
        }

        public async Task HandleUpdateLinkAsync(string thingIdSource, string thingIdTarget, string relName, Link link)
        {
            var uid = await _dgraphService.GetRelationUidByThingIdsAsync(thingIdSource, thingIdTarget, relName);
            if (uid == null)
            {
                _logger.LogWarning("No existing relation found between {thingIdSource} and {thingIdTarget} with name {relName}", thingIdSource, thingIdTarget, relName);
                return;
            }
            var newTarget = link.Href.ToString();
            var uids = await _dgraphService.GetUidsByThingIdsAsync([newTarget, thingIdSource]);
            if (uids.TryGetValue(thingIdTarget, out var uidTarget) && uids.TryGetValue(thingIdSource, out var uidSource))
            {
                _logger.LogDebug("uidTarget: {uidTarget}, uidSource: {uidSource}", uidTarget, uidSource);
                var mutation = ThingBuilder.BuildRelation(link, uidTarget, uidSource);
                mutation["uid"] = uid;
                await _dgraphService.AddEntitiesAsync([mutation]);
            }
        }

        public async Task HandleDeleteLinkAsync(string thingIdSource, string thingIdTarget, string relName)
        {
            if (string.IsNullOrEmpty(thingIdSource) || string.IsNullOrEmpty(thingIdTarget) || string.IsNullOrEmpty(relName))
            {
                throw new ArgumentException("thingIdSource, thingIdTarget, and relName cannot be null or empty.");
            }
            var uid = await _dgraphService.GetRelationUidByThingIdsAsync(thingIdSource, thingIdTarget, relName);
            if (uid != null)
            {
                var mutation = ThingBuilder.BuildDeleteRelation(uid);
                await _dgraphService.DeleteEntitiesAsync([mutation]);
            }
        }
    }
}