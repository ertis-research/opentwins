using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Shared.Utilities;
using OpenTwinsV2.Things.Services;
using OpenTwinsV2.Things.Models;
using Json.More;
using System.Text.Json.Nodes;
using System.Net;
using Dapr;
using System.Text.Json.Serialization;
using System.Data.SqlTypes;

[ApiController]
[Route("things")]
public class ThingsController : ControllerBase
{
    private record MultiStatusResponse (
            [property: JsonPropertyName("Id")] string Id,
            [property: JsonPropertyName("Status")] HttpStatusCode Status,
            [property: JsonPropertyName("Message")] string Message
        );
    private const string ActorType = Actors.ThingActor;
    private readonly IActorProxyFactory _actorProxyFactory;
    private readonly ILogger<ThingsController> _logger;
    private readonly ThingsQueryService _thingsQueryService;
    private readonly StatusManager _statusManager;
    private readonly ThingsManagerService _thingsManager;

    public ThingsController(IActorProxyFactory actorProxyFactory, ThingsQueryService thingsQueryService, StatusManager statusManager, ThingsManagerService thingsManager, ILogger<ThingsController> logger)
    {
        _actorProxyFactory = actorProxyFactory;
        _thingsQueryService = thingsQueryService;
        _logger = logger;
        _statusManager = statusManager;
        _thingsManager = thingsManager;
    }

    /// <summary>
    /// Retrieves all the Things with their ThingDescriptions.
    /// </summary>
    /// <param name="showConnections">OPTIONAL. Whether or not to include the Connections in the search. By default: false.</param>
    /// <param name="page">The number of the current page of Things.</param>
    /// <param name="pageSize">The size of the pages of Things.</param>
    /// <param name="search">The optional string filter applied to the identifier of the Things.</param>
    /// <response code="200">All the Things.</response>
    /// <response code="500">Error while retrieving the Things.</response>
    [HttpGet("")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PagedResult<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllThings(
        [FromQuery] bool showConnections = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        try
        {
            // Pasamos el search al servicio
            var result = await _thingsQueryService.GetAllThingsAsync(page, pageSize, search, showConnections);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve things list.");
            return StatusCode(500, "Internal server error.");
        }
    }

    /// <summary>
    /// Returns a lightweight list of things (ID and Title only), optimized for dropdowns/lists.
    /// </summary>
    /// <param name="page">The number of the current page of Things.</param>
    /// <param name="pageSize">The size of the pages of Things.</param>
    /// <param name="search">The optional string filter applied to the identifier of the Things.</param>
    /// <response code="200">The lightweight list of Things.</response>
    /// <response code="500">Error while retrieving the Things.</response>
    [HttpGet("summary")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PagedResult<ThingSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetThingsSummary(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, // Default más alto para listas
        [FromQuery] string? search = null)
    {
        try
        {
            var result = await _thingsQueryService.GetThingsSummaryAsync(page, pageSize, search);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve things summary.");
            return StatusCode(500, "Internal server error.");
        }
    }

    /// <summary>
    /// Creates a new Thing using the provided Thing Description JSON.
    /// </summary>
    /// <param name="value">A JSON object containing the Thing Description.</param>
    /// <response code="200">The Thing has been successfully created, and returns the ThingDescription.</response>
    /// <response code="400">Invalid input or malformed ThingDescription.</response>
    /// <response code="500">Error while creating the Thing or retreaving its ThingDescription afterwards.</response>
    [HttpPost("")]
    [SwaggerExample(ThingsAPIExamples.CreateExample)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateThing([FromBody] JsonElement value)
    {
        string rawJson = value.GetRawText();

        if (string.IsNullOrWhiteSpace(rawJson))
            return BadRequest("ThingDescription JSON string cannot be empty.");

        string id;
        try
        {
            id = SchemaValidator.ExtractIdFromJson(rawJson);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON format.");
        }

        string td;
        try
        {
            // td = await actor.SetThingDescriptionAsync(rawJson, null);
            td = await _thingsManager.SaveThing(id, rawJson);
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("InvalidOperationException"))
                return BadRequest(ex.Message);

            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound(ex.Message);

            return StatusCode(500, $"Internal error: {ex.Message}");
        }

        return Ok(td);
    }

    /// <summary>
    /// Creates or updates a Thing with the specified <paramref name="thingId"/> using the provided Thing Description JSON.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing to create or update. Must match the ID in the Thing Description.</param>
    /// <param name="value">A JSON object containing the Thing Description.</param>
    /// <returns>
    /// Returns 200 OK with the created or updated Thing Description.<br/>
    /// Returns 400 Bad Request if the JSON is invalid or missing required fields.<br/>
    /// Returns 409 Conflict if the <paramref name="thingId"/> does not match the ID in the JSON.
    /// </returns>
    /// <response code="200">The Thing has been successfully created or updated. It returns the final ThingDescription.</response>
    /// <response code="400">Invalid input or malformed JSON or ThingDescription.</response>
    /// <response code="409"><paramref name="thingId"/>does not match the id field in <paramref name="value"/>.</response>
    /// <response code="500">Error while committing changes or retrieving the ThingDescription afterwards.</response>
    [HttpPut("{thingId}")]
    [SwaggerExample(ThingsAPIExamples.CreateExample)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateThing(string thingId, [FromBody] JsonElement value)
    {
        string rawJson = value.GetRawText();

        if (string.IsNullOrWhiteSpace(rawJson))
            return BadRequest("ThingDescription JSON string cannot be empty.");

        if (string.IsNullOrWhiteSpace(thingId))
            return BadRequest("thingId cannot be null or empty.");

        string id;
        try
        {
            id = SchemaValidator.ExtractIdFromJson(rawJson);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON format.");
        }

        if (id != thingId) return Conflict("The 'id' in ThingDescription does not match the 'thingId' in the URL.");

        string td;
        try
        {
            // td = await actor.SetThingDescriptionAsync(rawJson, null);
            td = await _thingsManager.SaveThing(thingId, rawJson);
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("InvalidOperationException"))
                return BadRequest(ex.Message);

            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound(ex.Message);

            return StatusCode(500, $"Internal error: {ex.Message}");
        }

        return Ok(td);
    }

    [HttpPost("internal/create")]
    [Topic(PubSub.Name, PubSub.ThingUpdateTopic)]
    [ApiExplorerSettings(IgnoreApi =true)]
    public async Task<IActionResult> CreateThingsFromTopic([FromBody] JsonElement payload)
    {
        var data = payload.GetProperty("data");
        if (!data.TryGetProperty("operationId", out var operationId))
            return BadRequest("Missing operation ID");
        
        return await CreateThings(data.GetProperty("data"), operationId.GetString());
    }

    [HttpPost("internal/delete")]
    [Topic(PubSub.Name, PubSub.ThingDeleteTopic)]
    [ApiExplorerSettings(IgnoreApi =true)]
    public async Task<IActionResult> DeleteThingsFromTopic([FromBody] JsonElement payload)
    {
        var data = payload.GetProperty("data");
        if (!data.TryGetProperty("operationId", out var operationId))
            return BadRequest("Missing operation ID");

        return await DeleteThings(data.GetProperty("data"), operationId.GetString());
    }


    /// <summary>
    /// Creates or updates the Things using the provided Thing Descriptions JSONs.
    /// </summary>
    /// <param name="graph">A JSON array containing the Thing Descriptions objects.</param>
    /// <param name="operationid">OPTIONAL. The operation id that requested it.</param>
    /// <response code="200"><paramref name="graph"/> only contains one Thing and its creation or update was successful.</response>
    /// <response code="207">List of final status codes for each Thing in <paramref name="graph"/></response>
    /// <response code="400">The <paramref name="graph"/> is invalid or malformed, or it only contains one Thing and its ThingDescription is invalid or malformed.</response>
    /// <response code="500">Error while reading <paramref name="graph"/> or saving the new ThingDescriptions.</response>
    [HttpPut("")]
    [SwaggerExample(ThingsAPIExamples.CreateBulkExample)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<MultiStatusResponse>), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateThings([FromBody] JsonElement graph, string? operationid=null)
    {
        if(graph.AsNode() is not JsonArray)
            return BadRequest("JSON is not an Array");

        var responses = new List<dynamic>();

        var list = graph.EnumerateArray().ToList();

        foreach(var thing in list)
        {
            string rawJson = thing.GetRawText();
            string id;
            try
            {
                id = SchemaValidator.ExtractIdFromJson(rawJson);
            }
            catch (ArgumentException ex)
            {
                responses.Add(new MultiStatusResponse
                (
                    thing.TryGetProperty("id", out var id1) ? id1.GetString()! : thing.TryGetProperty("@id", out var id2) ? id2.GetString()! : $"Thing in position {list.IndexOf(thing)}",
                    HttpStatusCode.BadRequest,
                    ex.Message
                ));
                continue;
            }
            catch (JsonException)
            {
                responses.Add(new MultiStatusResponse(
                
                    thing.TryGetProperty("id", out var id1) ? id1.GetString()! : thing.TryGetProperty("@id", out var id2) ? id2.GetString()! : $"Thing in position {list.IndexOf(thing)}",
                    HttpStatusCode.BadRequest,
                    "Invalid JSON format."
                ));
                continue;
            }
            responses.Add(new MultiStatusResponse(
            
                id,
                HttpStatusCode.OK,
                rawJson
            ));
        }

        try
        {
            await _thingsQueryService.SaveInBulkToPostgreSqlAsync(list.Select(tdesc => JsonSerializer.Deserialize<ThingDescription>(tdesc)
                ?? throw new InvalidOperationException("Invalid ThingDescription")));
            
            foreach(var thing in list)
                await _statusManager.SaveOkThingStatus(thing.GetProperty("id").GetString()!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing bulk: {ex.Message}");
            if(list.Count==1)
                return StatusCode(500, responses.FirstOrDefault() ?? ex.Message);
            else
                return StatusCode(207, responses.Select(resp => resp.Status == HttpStatusCode.Accepted ? new {resp.Id, Status = HttpStatusCode.InternalServerError, resp.Message} : resp));
        }
       

        if(responses.Count == 1)
        {
            var resp = responses.First();
            if(resp.Status == HttpStatusCode.Accepted)
                return Ok(resp);
            else if(resp.Status == HttpStatusCode.BadRequest)
                return BadRequest(resp);
            else
                return StatusCode(500, resp);
        }else
            return StatusCode(207, responses);
    }


    /// <summary>
    /// Retrieves the Thing Description (TD) for the specified <paramref name="thingId"/>.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <response code="200">The Thing's ThingDescription.</response>
    /// <response code="404">The Thing was not found.</response>
    /// <response code="500">Error while retrieving the Thing.</response>
    [HttpGet("{thingId}")]
    [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetThingDescription(string thingId)
    {
        try
        {
            IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
            string? td = await actor.GetThingDescriptionAsync();
            if (string.IsNullOrWhiteSpace(td))
            {
                return NotFound($"ThingDescription with ID '{thingId}' was not found.");
            }
            return Content(td, "application/td+json");
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("InvalidOperationException"))
                return StatusCode(500, $"Error retrieving ThingDescription: {ex.Message}");

            return StatusCode(500, $"Internal error: {ex.Message}");
        }

    }

    /// <summary>
    /// Retrieves the Thing's status.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <response code="200">The Thing's status.</response>
    /// <response code="404">The Thing was not found.</response>
    /// <response code="500">Error while consulting the Thing's status.</response>
    [HttpGet("{thingId}/status")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetThingStatus(string thingId)
    {
        try
        {
            IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
            return Ok(await actor.GetThingStatusAsync());
        }catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("InvalidOperationException"))
                return StatusCode(500, $"Error retrieving ThingDescription: {ex.Message}");

            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound($"Thing with ID '{thingId}' was not found.");

            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a Thing by its identifier.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing to delete.</param>
    /// <response code="204">The Thing has successfully deleted.</response>
    /// <response code="404">The Thing was not found.</response>
    /// <response code="500">Error while deleting the Thing.</response>
    [HttpDelete("{thingId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteThing(string thingId)
    {
        if (string.IsNullOrWhiteSpace(thingId))
            return BadRequest("The 'thingId' cannot be null or empty.");

        try
        {
            IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);

            // bool isDeleted = await actor.DeleteThingAsync(null);
            bool isDeleted = await _thingsManager.DeleteThingAsync(thingId);

            if (!isDeleted)
            {
                return NotFound($"Thing with ID '{thingId}' not found.");
            }

            return NoContent(); // 204 No Content si la eliminación es exitosa.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Thing with ID '{ThingId}'", thingId);
            return StatusCode(500, "Internal server error while deleting the Thing.");
        }
    }

    /// <summary>
    /// Deletes the Things with the identifiers of each JSON object in the list provided.
    /// </summary>
    /// <param name="graph">The list with the objects with the identifiers.</param>
    /// <param name="operationid">OPTIONAL. The identifier of the operation.</param>
    /// <response code="204">The Things has been successfully deleted.</response>
    /// <response code="400">At least one Thing does not have an identifier.</response>
    /// <response code="500">Error while bulk deleting Things.</response>
    [HttpDelete("")]
    [SwaggerExample(ThingsAPIExamples.DeleteBulkExample)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteThings([FromBody] JsonElement graph, string? operationid=null)
    {
        if(graph.AsNode() is not JsonArray)
            return BadRequest("JSON is not an Array");

        // var responses = new List<dynamic>();

        var list = graph.EnumerateArray().ToList();
        // var options = new ParallelOptions { MaxDegreeOfParallelism = 150 };
        var idList = new List<string>();

        // await _globalSemaphore.WaitAsync();
        foreach(var thing in list)
                try{
                    idList.Add((thing.TryGetProperty("@id", out var id1) ? id1.GetString() : thing.TryGetProperty("id", out var id2) ? id2.GetString() : thing.TryGetProperty("thingId", out var id3) ? id3.GetString() : throw new ArgumentException("The 'thingId' cannot be null or empty.")) ?? "");
                }
                catch(ArgumentException)
                {
                    // responses.Add(new
                    // {
                    //     Id = $"Thing in position {list.IndexOf(thing)}",
                    //     Status = HttpStatusCode.BadRequest,
                    //     ex.Message
                    // });
                    return BadRequest($"the Thign in position {list.IndexOf(thing)} does not have an id.");
                }

        try
        {
            await _thingsQueryService.DeleteInBulkFromPostgreSqlAsync(idList);
            foreach(var thing in idList)
                await _statusManager.DeleteThingStatus(thing);
        }catch(Exception ex)
        {
            return StatusCode(500, $"Internal server error while deleting the Thing: {ex.Message}");
        }
        
        return NoContent();
    }

    /// <summary>
    /// Retrieves the current state of the specified Thing.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <response code="200">The Thing's current state.</response>
    /// <response code="404">The Thing was not found.</response>
    [HttpGet("{thingId}/state")]
    [ProducesResponseType(typeof(Dictionary<string, PropertyState>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentState(string thingId)
    {
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
        string state = await actor.GetCurrentStateAsync();
        if (state is null) return NotFound();
        return Content(state, "application/td+json");
    }


    /// <summary>
    /// Send a "api.update" event to update the current state of the specified Thing.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="newState">A JSON object containing the new state.</param>
    /// <response code="204">The update was successful.</response>
    /// <response code="400">The state was null or undefined.</response>
    /// <response code="500">Error while updating the state.</response>
    [HttpPut("{thingId}/state")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PutCurrentState(string thingId, [FromBody] JsonElement newState)
    {
        if (newState.ValueKind == JsonValueKind.Undefined || newState.ValueKind == JsonValueKind.Null)
            return BadRequest("State cannot be null or undefined.");

        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);

        var cloudEvent = new MyCloudEvent<string>(
            id: Guid.NewGuid().ToString(),
            source: $"http",
            type: "api:update",
            specVersion: "1.0",
            time: DateTime.UtcNow,
            dataContentType: "application/json",
            data: newState.GetRawText()
        );

        try
        {
            await actor.OnEventReceived(cloudEvent);
            _logger.LogInformation("Sent api.update event to thing {ThingId}", thingId);
            return NoContent(); // 204 cuando la actualización es correcta
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send api.update event for {ThingId}", thingId);
            return StatusCode(500, "Internal server error");
        }
    }


    /// <summary>
    /// Executes the specified action on a Thing.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="actionName">The name of the action to execute.</param>
    /// <param name="body">Optional parameters for the action, passed as JSON.</param>
    /// <response code="200">The action was executed successfully.</response>
    [HttpPost("{thingId}/action/{actionName}/execute")]
    public async Task<IActionResult> ExecuteAction(string thingId, string actionName, [FromBody] string body)
    {
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
        await actor.InvokeAction(actionName, body);
        return Ok();
    }


    /// <summary>
    /// Adds a new link to the specified Thing.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="links">A JSON list containing the links to add.</param>
    /// <response code="200">The links have successfully been added. Returns the updated ThingDescription.</response>
    /// <response code="400">At least one of the links provided in <paramref name="links"/> is invalid.</response>
    /// <response code="404">The Thing was not found.</response>
    [HttpPost("{thingId}/links")]
    [Produces("application/td+json")]
    [SwaggerExample(ThingsAPIExamples.CreateLinks)]
    [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddLink(string thingId, [FromBody] JsonElement links)
    {
        if (links.ValueKind == JsonValueKind.Undefined || links.ValueKind == JsonValueKind.Null)
            return BadRequest("The link cannot be null or undefined.");

        try
        {
            string updatedTd = await _thingsManager.AddLinkAsync(thingId, links.GetRawText());
            return Content(updatedTd, "application/td+json");
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound($"Thing with ID '{thingId}' not found.");

            if (ex.Message.Contains("InvalidOperationException"))
                return BadRequest(ex.Message);

            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing link of the specified Thing.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="href">The target ID of the link to update.</param>
    /// <param name="rel">The relationship of the link to update.</param>
    /// <param name="link">A JSON object containing the updated link.</param>
    /// <response code="200">The link has been successfully been updated. Returns the updated ThingDescription.</response>
    /// <response code="400">The <paramref name="link"/> is invalid or malformed.</response>
    /// <response code="404">The Thign was not found or it has no link with name <paramref name="rel"/> or target <paramref name="href"/>.</response>
    [HttpPut("{thingId}/links/{rel}/{*href}")]
    [SwaggerExample(ThingsAPIExamples.UpdateLink)]
    [Produces("application/td+json")]
    [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLink(string thingId, string href, string rel, [FromBody] JsonElement link)
    {
        if (link.ValueKind == JsonValueKind.Undefined || link.ValueKind == JsonValueKind.Null)
            return BadRequest("The link cannot be null or undefined.");

        try
        {
            string updatedTd = await _thingsManager.UpdateLinkAsync(thingId, href, rel, link.GetRawText());
            return Content(updatedTd, "application/td+json");
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound($"Thing with ID '{thingId}' or link {rel} with '{href}' not found.");

            if (ex.Message.Contains("InvalidOperationException"))
                return BadRequest(ex.Message);

            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }


    /// <summary>
    /// Removes a link from the specified Thing by its href.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="href">The href of the link to remove.</param>
    /// <param name="rel">The relationship of the link to update.</param>
    /// <response code="204">The link has been successfully deleted.</response>
    /// <response code="404">The Thing was not found or it has no link with name <paramref name="rel"/> and target <paramref name="href"/>.</response>
    [HttpDelete("{thingId}/links/{rel}/{*href}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLink(string thingId, string href, string rel)
    {
        if (string.IsNullOrWhiteSpace(href))
            return BadRequest("Target cannot be null or undefined.");

        try
        {
            await _thingsManager.RemoveLinkAsync(thingId, href, rel);
            return NoContent();
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound($"Thing with ID '{thingId}' or link '{href}' not found.");

            if (ex.Message.Contains("InvalidOperationException"))
                return BadRequest(ex.Message);

            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a new subscription to the specified Thing.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="subscription">A JSON object containing the subscription to add.</param>
    /// <response code="200">The subscription has been successfully added. Returns the updated ThingDescription.</response>
    /// <response code="400">The <paramref name="subscription"/> is invalid or malformed.</response>
    /// <response code="404">The Thing was not found.</response>
    [HttpPut("{thingId}/subscriptions")]
    [SwaggerExample(ThingsAPIExamples.CreateSubscription)]
    [Produces("application/td+json")]
    [ProducesResponseType(typeof(ThingDescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddSubscription(string thingId, [FromBody] JsonElement subscription)
    {
        if (subscription.ValueKind == JsonValueKind.Undefined || subscription.ValueKind == JsonValueKind.Null)
            return BadRequest("The subscription cannot be null or undefined.");

        try
        {
            string updatedTd = await _thingsManager.AddSubscriptionAsync(thingId, subscription.GetRawText());
            return Content(updatedTd, "application/td+json");
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound($"Thing with ID '{thingId}' not found.");

            if (ex.Message.Contains("InvalidOperationException"))
                return BadRequest(ex.Message);

            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes a subscription from the specified Thing by its identifier or target URI.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="subscriptionId">The identifier or target URI of the subscription to remove.</param>
    /// <response code="204">Subscription successfully deleted.</response>
    /// <response code="404">The Thing was not found or it didn't have a subscription with identifier <paramref name="subscriptionId"/>.</response>
    [HttpDelete("{thingId}/subscriptions/{*subscriptionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSubscription(string thingId, string subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return BadRequest("The 'subscriptionId' parameter cannot be empty.");

        try
        {
            await _thingsManager.RemoveSubscriptionAsync(thingId, subscriptionId);
            return NoContent();
        }
        catch (ActorMethodInvocationException ex)
        {
            if (ex.Message.Contains("KeyNotFoundException"))
                return NotFound($"Thing with ID '{thingId}' not found or subscription '{subscriptionId}' not found.");

            if (ex.Message.Contains("InvalidOperationException"))
                return BadRequest(ex.Message);

            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }
}
