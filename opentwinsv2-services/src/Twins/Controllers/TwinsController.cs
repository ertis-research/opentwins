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
using Twins.Services;
using Api;
using VDS.RDF.Query.Expressions.Functions.Sparql.Boolean;
using System.Net;
using System.Runtime.CompilerServices;
using System.Configuration.Internal;
using Lucene.Net.Util;
using Dapr.Actors;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using AngleSharp.Common;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("twins")]
    public class TwinsController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;
        private readonly ExportService _exportService;
        private readonly IJsonNquadsConverter _converter;
        private readonly ILogger<TwinsController> _logger;
        private readonly InstanciationService _instanciationService;

        public TwinsController(DGraphService dgraphService, ThingsService thingsService, IJsonNquadsConverter converter, ILogger<TwinsController> logger, ExportService exportService, InstanciationService instanciationService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
            _converter = converter;
            _logger = logger;
            _exportService = exportService;
            _instanciationService = instanciationService;
        }

        /// <summary>
        /// Gets all Twins with an optional filter.
        /// </summary>
        /// <param name="page">The desired page number.</param>
        /// <param name="pageSize">The size of the pages.</param>
        /// <param name="search">The optional string filter to serach with. If not specified, no filter will be applied.</param>
        /// <response code="200">The page of Twins.</response>
        /// <response code="500">Something went wrong while retrieving the Twins. </response>
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
        /// <param name="graph">The Json Graph of the Twin.</param>
        /// <param name="shapeId">OPTIONAL. The identifier of the Shape Graph.</param>
        /// <response code="200">A sucess message.</response>
        /// <response code="409">There is already a Twin with the provided identifier.</response>
        /// <response code="500">There was an issue creating the Thing.</response>
        [HttpPost("{twinId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateTwin(string twinId, [FromBody] JsonElement graph, string? shapeId = null)
        {

            // if shapeId is not null, validate graph with the corresponding shapeGraph
            if (await _dgraphService.ExistsTwinAsync(twinId))
                return Conflict("There is already a twin with this id");
            
            if(!(graph.TryGetProperty("@graph", out var graphEl) && graphEl.AsNode() is JsonArray graphArr))
                return BadRequest("The graph is of bad format.");
            
            if(_instanciationService.AreThereConflictingIdsOnSubGraph(graphArr))
                return BadRequest("There are repeated ids on the provided graph");

            if(graphArr.Any(thing => (thing!["@id"] ?? thing["id"]) is null))
                return BadRequest("At least one element in the graph provided does not have an identifier");

            if(!string.IsNullOrWhiteSpace(shapeId) && !await _dgraphService.ExistsShapeGraphByIdAsync(shapeId))
                return NotFound("There is no Shape Graph with the provided identifier.");

            Dictionary<string, ThingDescription> thingDescriptions = [];
            //Is graph valid and every Thing in it exists in Twins. Otherwise: error (Before making any changes)
            //At the same time, load already the TD of the Thing to avoid repeating requests

            try
            {
                var idList = _instanciationService.GetIdsFromGraph(graphArr);
                
                foreach(var id in idList)
                {
                    var td = await _thingsService.GetThingAsync(id);
                    thingDescriptions[id] = td;
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound($"There is a Thing in the provided graph that does not exist: {ex.Message}");
            }
            catch (InvalidOperationException)
            {
                return StatusCode(102, $"At least one thing of the graph provided is not available as of now");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error has been encountered while obtaining the things' Thing Descriptions: {ex.Message}");
            }
            
            try
            {
                await _thingsService.GetThingAsync(twinId);
                if(await _dgraphService.ExistsThingByIdAsync(twinId))
                {
                    if(await _dgraphService.IsThingAPlaceholderAsync(twinId))
                    
                        //Manage Placeholder completion    
                        await _dgraphService.InstanciateAPlaceHolderThing(twinId);
                    
                    //Add Twin Type. It's not already a Twin because it would've failed by now
                    await _dgraphService.AddNQuadTripleAsync([$"<{(await _dgraphService.GetUidsByThingIdsAsync([twinId]))[twinId]}> <dgraph.type> \"Twin\" ."]);
                }
                else
                {
                    //Just create in DGraph with the same id
                    await _dgraphService.AddThingAsync(ThingBuilder.BuildTwin(twinId));
                }
            }
            catch (KeyNotFoundException)
            {
                if(await _dgraphService.ExistsThingByIdAsync(twinId))
                    if(await _dgraphService.IsThingAPlaceholderAsync(twinId))
                    {
                        //Manage Placeholder completion
                        await _dgraphService.InstanciateAPlaceHolderThing(twinId);
                        await _dgraphService.AddThingTypeIntoThing(twinId, "Twin");          
                    }
                    else
                        return Conflict("The Thing cannot be instanciated");
                else
                {
                    //basic case: it doesn't exist in either: create in both
                    await _instanciationService.CreateInstanciationTwin(twinId);
                }
            }catch(InvalidOperationException ex)
            {
                return StatusCode(102, $"The Thing is not available as of now: {ex.Message}");
            }
            
            if(!string.IsNullOrWhiteSpace(shapeId))
                try
                {
                    string? report = await _instanciationService.ValidateGraphThroughShapeGraph(twinId, graph, shapeId);
                    if(!string.IsNullOrWhiteSpace(report)){
                        await DeleteTwin(twinId);
                        return BadRequest(new {Message=$"Could not instanciate because the given graph does not validate {shapeId} Shape Graph", Report=report});
                    }
                }catch(Exception ex)
                {
                    return StatusCode(500, $"Something went wrong while validating the Graph: {ex.Message}");
                }

            //Up to this point, if it needed validation it is already validated, go on with instanciation

            try
            {
                await _instanciationService.InstanciateThingGraph(graphArr, twinId, thingDescriptions);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while instanciating the Twin and its Things: {ex.Message}");
            }

            return Ok(new { message = "Twin created successfully" });
        }

        /// <summary>
        /// Retrieves the NQuads of the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <response code="200">The NQuads.</response>
        /// <response code="404">Either the Twin was not found or no Things were found associated to the Twin.</response>
        /// <response code="500">There was an issue while retieving the Twin.</response>
        [HttpGet("{twinId}")]
        [ProducesResponseType(typeof(ContentResult), StatusCodes.Status200OK, "application/n-quads")]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound, "application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError, "application/json")]
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

                if (thingIds is null || thingIds.Count == 0)
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

        /// <summary>
        /// Deletes a twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <response code="204">The Twin was deleted successfully.</response>
        /// <response code="404">The Twin could not be found.</response>
        /// <response code="500">An issue was encountered while deleting the Twin.</response>
        [HttpDelete("{twinId}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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

                await _dgraphService.DeleteThingAsync(twinId);

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
        /// <response code="200">The list of Things.</response>
        /// <response code="500">There was an issue retrieving the Things.</response>
        [HttpGet("{twinId}/things")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(List<JsonElement>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="200">The ThingDescription.</response>
        /// <response code="404">Either the Twin was not found, no Thing with the provided identifier was found associated to the Twin or the ThingDescription could not be obtained.</response>
        [HttpGet("{twinId}/things/{thingId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
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
            }catch (InvalidOperationException ex)
            {
                return StatusCode(102, $"The Thing is not available as of now: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the node of a Thing from the Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <response code="200">The Thing node.</response>
        /// <response code="404">The Thing node was not obtained.</response>
        [HttpGet("{twinId}/things/{thingId}/node")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        /// <response code="200">The current state of the Thing.</response>
        /// <response code="404">Either the Twin was not found ot no Thing with the provided identifier was found associated to it.</response>
        [HttpGet("{twinId}/things/{thingId}/state")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
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
        /// <response code="200">The single successful response obtained.</response>
        /// <response code="207">The obtained responses for each id.</response>
        /// <response code="400">The list is not of appropiate format.</response>
        /// <response code="404">The Twin was not found.</response>
        /// <response code="500">There was an uncontrolled issue while adding the Things into the Twin.</response>
        [HttpPut("{twinId}/things/{thingIds}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status207MultiStatus)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddThingToTwin(string twinId, string thingIds)
        {
            try
            {
                var thingIdList = thingIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);                

                if(!await _dgraphService.ExistsTwinAsync(twinId))
                    return NotFound($"There is no Twin with id {twinId}");

                var twinUid = await _dgraphService.GetUidsByThingIdsAsync([twinId]);
                
                if (twinUid == null || twinUid.Count < 1) return NotFound("TwinId not found");

                var responses = new List<dynamic>();
                JsonArray payload = [];
                
                var relativeUids = new Dictionary<string, string>();

                var idDict = (await Task.WhenAll(
                thingIdList.Select(async id =>
                    {
                        var uidDict = await _dgraphService.GetUidsByThingIdsAsync([id]);
                        //Manage Placeholder
                        var isPlaceholder = uidDict.TryGetValue(id, out _) && await _dgraphService.IsThingAPlaceholderAsync(id);
                        return (
                            Key: id,
                            Value: uidDict.TryGetValue(id, out var uid) ? (Exists: true, Placeholder: isPlaceholder, Uid: uid) : (Exists: false, Placeholder: isPlaceholder, Uid: $"_:relativeUid{id}")
                        );
                    })
                )).ToDictionary(x => x.Key, x => x.Value);

                List<string> thingsCreated = []; 

                int relationCounter = 0, targetCounter = 0;

                foreach((var thingId, (var exists, var isPlaceholder, var uid)) in idDict)
                {
                    if(exists)
                    {
                        _logger.LogDebug("Thing {ThingId} already exists -> add twin relation only", thingId);
                        if(isPlaceholder)
                            await _dgraphService.InstanciateAPlaceHolderThing(thingId);
                        var responseTwinAnexion = await _dgraphService.AddThingToTwinAsync(thingId, twinId);
                        responses.Add(new
                        {
                            Id = thingId,
                            Status = HttpStatusCode.OK,
                            Message = responseTwinAnexion.ToSafeString()
                        });
                    }
                    else
                    {
                        try
                        {
                            ThingDescription td = await _thingsService.GetThingAsync(thingId);
                            var hrefs = td.Links?.Select(l => l.Href.ToString()).Distinct() ?? [];

                            var hrefsUids = hrefs.Select(href => (Key: href, Value: idDict.TryGetValue(href, out var value) ? value.Uid : null)).Where(x=> x.Value is not null).ToDictionary(x=> x.Key, x=> x.Value!);

                            payload.AddRange(ThingBuilder.BuildPayloadWithLinks(td, twinUid[twinId], hrefsUids, ref relationCounter, ref targetCounter, uid: uid).Select(js => js!.DeepClone()));
                            thingsCreated.Add(thingId);
                        }
                        catch (KeyNotFoundException)
                        {
                            responses.Add(new
                            {
                                Id = thingId,
                                Status = HttpStatusCode.NotFound,
                                Message = "The Thing does not exist"
                            });
                            continue;
                        }
                        catch (InvalidOperationException)
                        {
                            responses.Add(new
                            {
                                Id = thingId,
                                Status = HttpStatusCode.Processing,
                                Message = "The Thing is not available"
                            });
                            continue;
                        }
                        catch (ActorMethodInvocationException ex)
                        {
                            if (ex.Message.Contains("InvalidOperationException"))
                                responses.Add(new
                                {
                                    Id = thingId,
                                    Status = HttpStatusCode.NotFound,
                                    Message = "The Thing is not available"
                                });
                        
                            if (ex.Message.Contains("KeyNotFoundException"))
                                responses.Add(new
                                {
                                    Id = thingId,
                                    Status = HttpStatusCode.Processing,
                                    Message = "The Thing does not exist"
                                });
                            continue;
                        }
                    }
                }

                Console.WriteLine($"PAYLOAD AL ANEXAR AL TWINNNN:\n{JsonSerializer.Serialize(payload)}");

                var response = await _dgraphService.AddEntitiesAsync(payload);
                foreach(var thingCreated in thingsCreated)
                    responses.Add(new
                    {
                        Id = thingCreated,
                        Status = HttpStatusCode.OK,
                        Message = response.ToSafeString()
                    });

                if(responses.Count == 1)
                {
                    var resp = responses.First();
                    if(resp.Status == HttpStatusCode.OK)
                        return Ok(resp);
                    else
                        return NotFound(resp);
                }else
                    return StatusCode(207, responses);

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
        /// <response code="200">The response of deleting the thing.</response>
        /// <response code="404">Either if the Twin was not found or no Thing with the provided identifier was associated to the Twin.</response>
        /// <response code="500">There was any issue while deleting the Thing.</response>
        [HttpDelete("{twinId}/things/{thingId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Response), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="200">The JSON of the Twin.</response>
        /// <response code="404">The Twin was not found.</response>
        /// <response code="500">There was an issue while generating the Twin JSON.</response>
        [HttpGet("{twinId}/export/Json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportTwinInJsonFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId) && !await _dgraphService.IsThingAPlaceholderAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            try
            {
                var json = await _exportService.GetJsonWithoutNamespace(twinId);
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
        /// <response code="200">The JSON-LD of the Twin.</response>
        /// <response code="404">The Twin was not found.</response>
        /// <response code="500"> There was an issue while generating either the JSON or the JSON-LD of the Twin.</response>
        [HttpGet("{twinId}/export/JsonLd")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportTwinInJsonLdFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId) && !await _dgraphService.IsThingAPlaceholderAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            JsonObject json;
            try
            {
                json = await _exportService.GetJsonWithoutNamespace(twinId) ?? throw new Exception($"Obtained null value from the Json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n {ex.Message}");
            }
            try
            {
                return Ok(ExportService.GetJsonLDFromRegularJson(json, twinId, twin:true) ?? throw new Exception("Obtained null value from the JsonLd"));
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
        /// <response code="200">The TTL File of the Twin.</response>
        /// <response code="404">The Twin was not found.</response>
        /// <response code="500">There was an issue while obtaining either the JSON or the TTL File of the Twin.</response>
        [HttpGet("{twinId}/export/TTL")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/octet-stream")]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound, "application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError, "application/json")]
        public async Task<IActionResult> ExportTwinInTTLFormat(string twinId)
        {
            var check = await _dgraphService.ExistsThingByIdAsync(twinId) && !await _dgraphService.IsThingAPlaceholderAsync(twinId);
            if (!check)
            {
                return NotFound(new { message = $"Twin '{twinId}' does not exist" });
            }
            JsonObject json;
            try
            {
                json = await _exportService.GetJsonWithoutNamespace(twinId) ?? throw new Exception("Obtained null value");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the Twin JSON:\n {ex.Message}");
            }

            try
            {
                return File(FormatService.GetTTLFileFromRegularJson(twinId, json, twin:true), "text/turtle", $"{twinId}.ttl");
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
        /// <response code="200">The results of the query.</response>
        /// <response code="204">The query was successfully run but no results were obtained.</response>
        /// <response code="400">Either the query was void or null, the query was not of SELECT or similar type, or its format was not valid</response>
        /// <response code="404">The Twin was not found.</response>
        /// <response code="500">There was an issue while processing or running the query on the Twin.</response>
        [HttpPost("{twinId}/query")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SparQLQueryInTwin(string twinId, [FromForm] string stringQuery)
        {
            //check if the stringQuery is empty
            if (string.IsNullOrWhiteSpace(stringQuery))
            {
                return BadRequest($"The SparQL query cannot be empty");
            }
            var check = await _dgraphService.ExistsThingByIdAsync(twinId) && !await _dgraphService.IsThingAPlaceholderAsync(twinId);
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
                var results = await _exportService.RunSparQLQuery(twinId, null, query);
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