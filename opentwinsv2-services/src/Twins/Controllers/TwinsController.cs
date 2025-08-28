using OpenTwinsV2.Twins.Services;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Models;
using System.Text.Json.Nodes;
using OpenTwinsV2.Twins.Builders;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("twins")]
    public class TwinsController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;

        public TwinsController(DGraphService dgraphService, ThingsService thingsService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
        }

        [HttpPost("{twinId}")]
        public async Task<IActionResult> CreateTwin(string twinId)
        {
            if (!await _dgraphService.ExistsThingByIdAsync(twinId))
            {
                var payload = new JsonObject
                {
                    ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                    ["id"] = twinId,
                    ["title"] = "",
                    ["properties"] = new JsonObject { },
                    ["actions"] = new JsonObject { },
                    ["events"] = new JsonObject { }
                };

                var thingsResponse = await _thingsService.CreateThingAsync(payload);
                if (!thingsResponse) return StatusCode(500, "Failed to create twin in things service");

                var dgraphResponse = await _dgraphService.AddThingAsync(ThingBuilder.BuildTwin(twinId));
                bool dgraphOk = dgraphResponse != null && dgraphResponse.Uids != null && dgraphResponse.Uids.Count > 0;
                if (!dgraphOk) return StatusCode(500, "Failed to create twin in DGraph: " + dgraphResponse?.ToString());

                return Ok(new { message = "Twin created successfully" });
            }
            return Conflict("There is already a twin with this id");
        }

        [HttpGet("{twinId}/things")]
        public async Task<IActionResult> GetThingsInTwin(string twinId)
        {
            var response = await _dgraphService.GetThingsInTwinAsync(twinId);
            return Ok(response);
        }

        [HttpGet("{twinId}/things/{thingId}")]
        public async Task<IActionResult> GetThingDescriptionInTwinById(string twinId, string thingId)
        {
            var check = await _dgraphService.ThingBelongsToTwinAsync(twinId, thingId);
            if (!check)
                return NotFound(new { message = $"Thing '{thingId}' does not belong to Twin '{twinId}' or not exists" });

            try
            {
                var thingDescription = await _thingsService.GetThingAsync(thingId);
                return Ok(thingDescription);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("{twinId}/things/{thingId}/node")]
        public async Task<IActionResult> GetThingNodeInTwinById(string twinId, string thingId)
        {
            var response = await _dgraphService.GetThingInTwinByIdAsync(twinId, thingId);

            return (response == null) ? NotFound() : Ok(response);
        }

        [HttpGet("{twinId}/things/{thingId}/state")]
        public async Task<IActionResult> GetThingStateInTwinById(string twinId, string thingId)
        {
            var check = await _dgraphService.ThingBelongsToTwinAsync(twinId, thingId);
            if (!check)
                return NotFound(new { message = $"Thing '{thingId}' does not belong to Twin '{twinId}' or not exists" });

            try
            {
                var currentState = await _thingsService.GetThingState(thingId);
                return Ok(currentState);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }


        [HttpPut("{twinId}/things/{thingId}")]
        public async Task<IActionResult> AddThingToTwin(string twinId, string thingId)
        {
            try
            {
                if (!await _dgraphService.ExistsThingByIdAsync(thingId))
                {
                    ThingDescription td = await _thingsService.GetThingAsync(thingId);

                    var thing = ThingBuilder.MapToThing(td);

                    var twinUid = await _dgraphService.GetUidByThingIdAsync(twinId);
                    if (twinUid == null) return NotFound("TwinId not found");
                    thing = ThingBuilder.AddTwinToThing(thing, twinUid);

                    var response = await _dgraphService.AddThingAsync(thing);
                    return Ok(response);
                }
                else
                {
                    var response = await _dgraphService.AddThingToTwinAsync(thingId, twinId);
                    return Ok(response);
                }
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Unexpected error: {ex.Message}");
            }
        }

        [HttpDelete("{twinId}/things/{thingId}")]
        public async Task<IActionResult> DeleteThingFromTwin(string twinId, string thingId)
        {
            try
            {
                return Ok(await _dgraphService.RemoveThingFromTwinAsync(twinId, thingId));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Unexpected error: {ex.Message}");
            }
        }
    }
}