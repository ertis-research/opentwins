using OpenTwinsV2.Twins.Services;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Models;
using System.Text.Json.Nodes;
using OpenTwinsV2.Twins.Builders;
using System.Text;
using System.Text.Json;
using System.Configuration;
using Json.More;
using VDS.RDF;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("twins")]
    public class TwinsController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;
        private readonly ConverterService _converterService;
        private readonly IJsonNquadsConverter _converter;
        private readonly ILogger<TwinsController> _logger;

        public TwinsController(DGraphService dgraphService, ThingsService thingsService, IJsonNquadsConverter converter, ILogger<TwinsController> logger, ConverterService converterService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
            _converter = converter;
            _logger = logger;
            _converterService = converterService;
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

        [HttpGet("{twinId}")]
        public async Task<IActionResult> GetTwin(string twinId)
        {
            try
            {
                var rawJson = await _dgraphService.GetThingsInTwinNQUADSAsync(twinId);

                if (string.IsNullOrWhiteSpace(rawJson))
                    return NotFound($"No things found for twin {twinId}");

                Console.WriteLine(rawJson);

                using var doc = JsonDocument.Parse(rawJson);
                var json = doc.RootElement;

                var thingIds = json.GetProperty("things").EnumerateArray().SelectMany(t => t.GetProperty("~twins").EnumerateArray())
                    .Where(t => t.TryGetProperty("thingId", out var id) && id.ValueKind == JsonValueKind.String)
                    .Select(t => t.GetProperty("thingId").GetString()!).Distinct().ToList();

                if (thingIds is null)
                    return NotFound($"No things found for twin {twinId}");

                var states = await _thingsService.GetThingsStatesAsync(thingIds);
                //Console.WriteLine(JsonSerializer.Serialize(states));

                var nquads = _converter.JsonToNquads(rawJson, states);

                // Return as NQUADS (plain text)
                return Content(nquads, "application/n-quads", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving twin {twinId}: {ex.Message}");
            }
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


        [HttpPut("{twinId}/things/{thingIds}")]
        public async Task<IActionResult> AddThingToTwin(string twinId, string thingIds)
        {
            try
            {
                var thingIdList = thingIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var twinUid = await _dgraphService.GetUidsByThingIdsAsync([twinId]);
                if (twinUid == null || twinUid.Count < 1) return NotFound("TwinId not found");

                var responses = new List<object>();

                foreach (var thingId in thingIdList)
                {
                    if (!await _dgraphService.ExistsThingByIdAsync(thingId))
                    {
                        ThingDescription td = await _thingsService.GetThingAsync(thingId);
                        //var thing = ThingBuilder.MapToThing(td);
                        //thing = ThingBuilder.AddTwinToThing(thing, twinUid[twinId]);

                        var hrefs = td.Links?.Select(l => l.Href.ToString()).Distinct();
                        var uidTargets = await _dgraphService.GetUidsByThingIdsAsync(hrefs ?? []);

                        var payload = ThingBuilder.BuildPayloadWithLinks(td, twinUid[twinId], uidTargets);
                        Console.WriteLine(JsonSerializer.Serialize(payload));

                        var response = await _dgraphService.AddEntitiesAsync(payload);
                        responses.Add(response);
                    }
                    else
                    {
                        _logger.LogDebug("Thing {ThingId} already exists -> add twin relation only", thingId);
                        var response = await _dgraphService.AddThingToTwinAsync(thingId, twinId);
                        responses.Add(response);
                    }
                }

                return Ok(responses.Count == 1 ? responses.First() : responses);
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

        [HttpGet("{twinId}/export/Json")]
        public async Task<IActionResult> ExportTwinInJsonFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            try
            {
                var json = await _converterService.getJsonWithoutNamespace(twinId);
                return Ok(json);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n {ex.Message}");
            }

        }

        [HttpGet("{twinId}/export/JsonLd")]
        public async Task<IActionResult> ExportTwinInJsonLdFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            JsonObject json = null;
            try
            {
                json = await _converterService.getJsonWithoutNamespace(twinId);
                if (json is null)
                    return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n Obtained null value ");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n {ex.Message}");
            }
            try
            {
                return Ok(await _converterService.GetJsonLDFromRegularJson(json, twinId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JsonLd:\n {ex.Message}");
            }
        }

        [HttpGet("{twinId}/export/TTL")]
        public async Task<IActionResult> ExportTwinInTTLFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            JsonObject json = null;
            try
            {
                json = await _converterService.getJsonWithoutNamespace(twinId);
                if (json is null)
                    throw new Exception("Obtained null value");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n {ex.Message}");
            }

            try
            {
                return File(await _converterService.GetTTLFileFromRegularJson(twinId, json), "text/turtle", $"{twinId}.ttl");
            }
            catch (Exception e)
            {
                return StatusCode(500, $"Something wrong while parsing to TTL Format:{e}");
            }
        }
    }
}