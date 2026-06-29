using System.Text.Json;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Utilities;
using OpenTwinsV2.Things.Logging;

namespace OpenTwinsV2.Things.Services
{
    public class DescriptionManagerService
    {
        private readonly ThingsQueryService _queryService;
        private readonly StateService _stateService;
        private readonly StatusManager _statusManager;
        private readonly TwinsService _twinsService;

        public DescriptionManagerService(ThingsQueryService queryService, StateService stateService, StatusManager statusManager, TwinsService twinsService)
        {
            _queryService = queryService;
            _stateService = stateService;
            _statusManager = statusManager;
            _twinsService = twinsService;
        }
        public async Task<ThingDescription?> LoadDescriptionAsync(string thingId)
        {
            ThingDescription? td;
            var bulkStateItems = await _stateService.LoadThingDescriptionBulkState(thingId);
            if (bulkStateItems.Count > 0 && !string.IsNullOrEmpty(bulkStateItems[0].Value))
            {
                td = JsonSerializer.Deserialize<ThingDescription>(bulkStateItems[0].Value);
                ActorLogger.Info(thingId, "Thing Description loaded from statestore");
            }
            else
            {
                ActorLogger.Info(thingId, "No ThingDescription found in statestore.");
                td = await _queryService.LoadFromPostgreSqlAsync(thingId);
                if(td is not null)
                    await _stateService.SaveThingDescriptionState(thingId, td); //store in caché
            }

            return td;
        }

        public async Task SaveDescriptionAsync(string thingId, ThingDescription td, bool asyncPersist = true, bool modifyInTwins = true)
        {
            var existingTd = await LoadDescriptionAsync(thingId);
            // ThingDescription = td;
            // await _stateService.SaveThingDescriptionState(thingId, td);
            if(modifyInTwins)
                await _twinsService.UpdateThingInTwins(td, existingTd);

            if (asyncPersist)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _stateService.PublishChangedThingDescriptionEvent(thingId, td);
                        await _queryService.SaveToPostgreSqlAsync(td);
                        await _statusManager.SaveOkThingStatus(thingId);
                        ActorLogger.Info(thingId, $"Thing successfully created");
                    }
                    catch (Exception ex)
                    {
                        ActorLogger.Error(thingId, $"Error while background saving TD: {ex}");
                    }
                });
            }
            else
            {
                await _queryService.SaveToPostgreSqlAsync(td);
                await _statusManager.SaveOkThingStatus(thingId);
                ActorLogger.Info(thingId, $"Thing successfully created");
            }
        }

        public async Task DeleteDescriptionAsync(string thingId, bool asyncPersist = true)
        {
            await _stateService.DeleteThingDescriptionState(thingId);
            ActorLogger.Info(thingId, "Thing Description deleted from statestore.");

            // ThingDescription = null; //delete it from local memory

            await _twinsService.DeleteThingInTwins(thingId);

            if (asyncPersist)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _stateService.PublishDeletedThingDescriptionEvent(thingId);
                        await _queryService.DeleteFromPostgreSqlAsync(thingId);
                        await _statusManager.DeleteThingStatus(thingId);
                        ActorLogger.Info(thingId, $"Thing successfully deleted");
                    }
                    catch (Exception ex)
                    {
                        ActorLogger.Error(thingId, $"Error while background deleting TD: {ex}");
                    }
                });
            }
            else
            {
                await _stateService.PublishDeletedThingDescriptionEvent(thingId);
                await _queryService.DeleteFromPostgreSqlAsync(thingId);
                await _statusManager.DeleteThingStatus(thingId);
                ActorLogger.Info(thingId, $"Thing successfully deleted");
            }
        }
    }
}