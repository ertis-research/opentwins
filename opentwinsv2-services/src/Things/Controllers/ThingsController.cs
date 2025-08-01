using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Things.Models;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Shared.Utilities;

[ApiController]
[Route("things")]
public class ThingsController : ControllerBase
{
    private const string ActorType = Actors.ThingActor;
    private readonly IActorProxyFactory _actorProxyFactory;

    public ThingsController(IActorProxyFactory actorProxyFactory)
    {
        _actorProxyFactory = actorProxyFactory;
    }

    // Tendria que cambiarlo para que no actualice??
    /// <summary>
    /// Creates a new Thing using the provided Thing Description JSON.
    /// </summary>
    /// <param name="value">A JSON object containing the Thing Description.</param>
    /// <returns>
    /// Returns 200 OK with the created Thing Description,
    /// 400 Bad Request if the input is invalid or JSON format is incorrect.
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

    [HttpGet("{thingId}/state")]
    public async Task<IActionResult> GetCurrentState(string thingId)
    {
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
        string state = await actor.GetCurrentStateAsync();
        if (state is null) return NotFound();
        return Content(state, "application/td+json");
    }

    [HttpPost("{thingId}/action/{actionName}/execute")]
    public async Task<IActionResult> ExecuteAction(string thingId, string actionName, [FromBody] string body)
    {
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
        await actor.InvokeAction(actionName, body);
        return Ok();
    }



}
