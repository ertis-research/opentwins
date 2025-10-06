using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Shared.Utilities;

[ApiController]
[Route("things")]
public class ThingsController : ControllerBase
{
    private const string ActorType = Actors.ThingActor;
    private readonly IActorProxyFactory _actorProxyFactory;
    private readonly ILogger<ThingsController> _logger;

    public ThingsController(IActorProxyFactory actorProxyFactory, ILogger<ThingsController> logger)
    {
        _actorProxyFactory = actorProxyFactory;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new Thing using the provided Thing Description JSON.
    /// </summary>
    /// <param name="value">A JSON object containing the Thing Description.</param>
    /// <returns>
    /// Returns 200 OK with the created Thing Description.<br/>
    /// Returns 400 Bad Request if the input is invalid or the JSON format is incorrect.
    /// </returns>
    [HttpPost("")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
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

        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(id), ActorType);

        string td;
        try
        {
            td = await actor.SetThingDescriptionAsync(rawJson);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
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
    [HttpPut("{thingId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
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

        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(id), ActorType);

        string td;
        try
        {
            td = await actor.SetThingDescriptionAsync(rawJson);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok(td);
    }


    /// <summary>
    /// Retrieves the Thing Description (TD) for the specified <paramref name="thingId"/>.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <returns>
    /// Returns 200 OK with the Thing Description.<br/>
    /// Returns 404 Not Found if the Thing was not found.<br/>
    /// Returns 500 Internal Server Error if there was an error retrieving the Thing.
    /// </returns>
    [HttpGet("{thingId}")]
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
        catch (InvalidOperationException ex)
        {
            // Expected issue (e.g., state was not loaded)
            return StatusCode(500, $"Error retrieving ThingDescription: {ex.Message}");
        }

    }


    /// <summary>
    /// Retrieves the current state of the specified Thing.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <returns>
    /// Returns 200 OK with the current state.<br/>
    /// Returns 404 Not Found if the state does not exist.
    /// </returns>
    [HttpGet("{thingId}/state")]
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
    /// <returns>
    /// Returns 204 No Content if the update was successful.<br/>
    /// Returns 400 Bad Request if the state is null or undefined.<br/>
    /// Returns 500 Internal Server Error if the update could not be processed.
    /// </returns>
    [HttpPut("{thingId}/state")]
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
    /// <returns>
    /// Returns 200 OK if the action was executed successfully.
    /// </returns>
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
    /// <param name="link">A JSON object containing the link to add.</param>
    /// <returns>
    /// Returns 200 OK with the updated Thing Description.<br/>
    /// Returns 400 Bad Request if the link is invalid.<br/>
    /// Returns 404 Not Found if the Thing was not found.
    /// </returns>
    [HttpPut("{thingId}/links")]
    [Produces("application/td+json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddLink(string thingId, [FromBody] JsonElement link)
    {
        if (link.ValueKind == JsonValueKind.Undefined || link.ValueKind == JsonValueKind.Null)
            return BadRequest("The link cannot be null or undefined.");

        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);

        try
        {
            string updatedTd = await actor.AddLinkAsync(link.GetRawText());
            return Content(updatedTd, "application/td+json");
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Thing con ID '{thingId}' no encontrado.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }


    /// <summary>
    /// Removes a link from the specified Thing by its href.
    /// </summary>
    /// <param name="thingId">The identifier of the Thing.</param>
    /// <param name="href">The href of the link to remove.</param>
    /// <returns>
    /// Returns 204 No Content if the link was removed successfully.<br/>
    /// Returns 404 Not Found if the Thing or the link does not exist.
    /// </returns>
    [HttpDelete("{thingId}/links/{*href}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveLink(string thingId, string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return BadRequest("El parámetro 'href' no puede ser vacío.");

        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);

        try
        {
            await actor.RemoveLinkAsync(href);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Thing con ID '{thingId}' o link '{href}' no encontrado.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

}
