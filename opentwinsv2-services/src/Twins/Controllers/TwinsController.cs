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
using AngleSharp.Dom;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

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

        /// <summary>
        /// Gets all Twins with an optional filter.
        /// </summary>
        /// <param name="page">The desired page number. By default: 1.</param>
        /// <param name="pageSize">The size of the pages. By default: 10.</param>
        /// <param name="search">The optional string filter to serach with. If not specified, no filter will be applied.</param>
        /// <returns>
        /// Returns 200 Ok with the page of Twins.<br/>
        /// Returns 500 Intenral Server Error if something went wrong while retrieving the Twins. 
        /// </returns>
        [HttpGet("")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PagedResult<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllTwins(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null
        )
        {
            try
            {
                var twins = await _dgraphService.GetAllTwinsAsync(page, pageSize, search);
                return Ok(twins);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting all Things: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates an empty Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns 200 Ok with a sucess message.<br/>
        /// Returns 409 Conflict if there is already a Twin with the provided identifier.<br/>
        /// Returns 500 Internal Sevrer Error if there was any issue creating the Thing.
        /// </returns>
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

        /// <summary>
        /// Retrieves the NQuads of the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns 200 Ok with the NQuads.<br/>
        /// Returns 404 Not Found if either the Twin was not found or no Things were found associated to the Twin.<br/>
        /// Returns 500 Internal Server Error if there was any issue while retieving the Twin.
        /// </returns>
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

        [HttpDelete("{twinId}")]
        public async Task<IActionResult> DeleteTwin(string twinId)
        {
            try
            {
                if(!await _dgraphService.ExistsTwinAsync(twinId))
                    return NotFound($"There is no Twin with thingId {twinId}");
                
                //get things
                var things = await _dgraphService.GetThingsInTwinAsync(twinId);

                //unlink things to twin
                foreach(var thing in things)
                    await _dgraphService.RemoveThingFromTwinAsync(thing.GetProperty("thingId").GetString()!, twinId);

                //delete twin thing
                if(!await _thingsService.DeleteThingAsync(twinId))
                    throw new Exception("The Twin's thing could not be deleted successfuly.");

                return NoContent();
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while deleting the Twin: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the list of Things of a Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns 200 Ok with the list of Things.<br/>
        /// Returns 500 Internal Server Error if there was any issue retrieving the Things.
        /// </returns>
        [HttpGet("{twinId}/things")]
        public async Task<IActionResult> GetThingsInTwin(string twinId)
        {
            var response = await _dgraphService.GetThingsInTwinAsync(twinId);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves the ThingDescription (TD) of a Thing in the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns 200 Ok with the ThingDescription.<br/>
        /// Returns 404 Not Found if either the Twin was not found, no Thing with the provided identifier was found associated to the Twin or the ThingDescription could not be obtained.
        /// </returns>
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

        /// <summary>
        /// Retrieves the node of a Thing from the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns 200 Ok with the Thing node.<br/>
        /// Returns 404 Not Found if the Thing node was not obtained.
        /// </returns>
        [HttpGet("{twinId}/things/{thingId}/node")]
        public async Task<IActionResult> GetThingNodeInTwinById(string twinId, string thingId)
        {
            var response = await _dgraphService.GetThingInTwinByIdAsync(twinId, thingId);

            return (response == null) ? NotFound() : Ok(response);
        }

        /// <summary>
        /// Retrieves the state of a Thing in the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns 200 Ok with the current state of the Thing.<br/>
        /// Returns 404 Not Found if either the Twin was not found ot no Thing with the provided identifier was found associated to it.
        /// </returns>
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

        /// <summary>
        /// Adds the Things to the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="thingIds">The list of Thing identifiers separated by commas.</param>
        /// <returns>
        /// Returns 200 Ok with the responses obtained.<br/>
        /// Returns 400 Bad Request if the list is not of appropiate format.<br/>
        /// Returns 404 Not Found if the Twin was not found.<br/>
        /// Returns 500 Internal Server Error if there was any uncontrolled issue while adding the Things into the Twin.
        /// </returns>
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

        /// <summary>
        /// Deletes a Thing from the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns 200 Ok with the response of deleting the thing.<br/>
        /// Returns 404 Not Found either if the Twin was not found or no Thing with the provided identifier was associated to the Twin.<br/>
        /// Returns 500 Internal Server Error if there was any issue while deleting the Thing.
        /// </returns>
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

        /// <summary>
        /// Returns the Twin in a JSON format.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns 200 Ok with the JSON of the Twin.<br/>
        /// Returns 404 Not Found if the Twin was not found.<br/>
        /// Returns 500 Internal Server Error if threre was any issue while generating the Twin JSON.
        /// </returns>
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

        /// <summary>
        /// Returns the Twin in a JSON-LD format.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns 200 Ok with the JSON-LD of the Twin.<br/>
        /// Returns 404 Not Found if the Twin was not found.<br/>
        /// Returns 500 Internal Server Error if there was any issue while generating either the JSON or the JSON-LD of the Twin.
        /// </returns>
        [HttpGet("{twinId}/export/JsonLd")]
        public async Task<IActionResult> ExportTwinInJsonLdFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            JsonObject json;
            try
            {
                json = await _converterService.getJsonWithoutNamespace(twinId) ?? throw new Exception($"Obtained null value from the Json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n {ex.Message}");
            }
            try
            {
                return Ok(_converterService.GetJsonLDFromRegularJson(json, twinId) ?? throw new Exception("Obtained null value from the JsonLd"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JsonLd:\n {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the Twin in a TTl format File.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns 200 Ok with the TTL File of the Twin.<br/>
        /// Returns 404 Not Found if the Twin was not found.<br/>
        /// Returns 500 Internal Server Error if there was any issue while obtaining either the JSON or the TTL File of the Twin.
        /// </returns>
        [HttpGet("{twinId}/export/TTL")]
        public async Task<IActionResult> ExportTwinInTTLFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            JsonObject json;
            try
            {
                json = await _converterService.getJsonWithoutNamespace(twinId) ?? throw new Exception("Obtained null value");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n {ex.Message}");
            }

            try
            {
                return File(_converterService.GetTTLFileFromRegularJson(twinId, json), "text/turtle", $"{twinId}.ttl");
            }
            catch (Exception e)
            {
                return StatusCode(500, $"Something wrong while parsing to TTL Format:{e}");
            }
        }

        /// <summary>
        /// Runs a SparQL query on the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="stringQuery">The SparQL query on a String format.</param>
        /// <returns>
        /// Returns 200 Ok with the results of the query.<br/>
        /// Returns 204 No Content if the query was successfully run but no results were obtained.<br/>
        /// Returns 400 Bad Request if either the query was void or null, the query was not of SELECT or similar type, or its format was not valid<br/>
        /// Returns 404 Not Found if the Twin was not found.
        /// Returns 500 Internal Server Error if there was any issue while processing or running the query on the Twin.
        /// </returns>
        [HttpPost("{twinId}/query")]
        public async Task<IActionResult> SparQLQueryInTwin(string twinId, [FromForm] string stringQuery)
        {
            //check if the stringQuery is empty
            if (string.IsNullOrWhiteSpace(stringQuery))
            {
                return BadRequest($"The SparQL query cannot be empty");
            }
            var check = await _dgraphService.ExistsThingByIdAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            var parser = new SparqlQueryParser();
            try
            {
                var query = parser.ParseFromString(stringQuery);

                //only select queries are supported, anything else is blockes
                if (!(query.QueryType.ToString().StartsWith("Select", StringComparison.OrdinalIgnoreCase) || query.QueryType.Equals(SparqlQueryType.Ask))) //the are severlat select type queries
                {
                    return BadRequest($"Only SELECT queries are available, \"{stringQuery}\" is a \n{query.QueryType.ToString()} query");
                }
                var results = await _converterService.RunSparQLQuery(twinId, null, query);
                if(results is null)
                    throw new Exception("Either the Json or the Graph are null");
                
                //parse results format so it is readable
                if (query.QueryType == SparqlQueryType.Ask)
                    return Ok(results.Result);

                if (results.IsEmpty)
                    return Ok(new List<object>());

                var jsonResults = results.Select(r =>
                    r.Variables.ToDictionary(v => v, v => r[v]?.ToString())
                );

                return Ok(jsonResults);
                
            }catch (RdfParseException ex)
            {
                return BadRequest($"The format of the SparQL query \"{stringQuery}\" is wrong:\n{ex}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something wrong while processing the SparQl query:\n{ex.GetType}: {ex.Message}");
            }
        }
    }
}