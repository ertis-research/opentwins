using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsv2.Things.Interfaces;
using OpenTwinsv2.Things.Models;
using OpenTwinsv2.Things.Services;

[ApiController]
[Route("things")]
public class ThingsController : ControllerBase
{
    private const string ActorType = "ThingActor";
    private readonly IActorProxyFactory _actorProxyFactory;

    public ThingsController(IActorProxyFactory actorProxyFactory)
    {
        _actorProxyFactory = actorProxyFactory;
    }

    [HttpPost("")]
    public async Task<IActionResult> CreateThing([FromBody] JsonElement value)
    {
        var deserializedValue = JsonSerializer.Deserialize<ThingDescription>(value.GetRawText());
        if (deserializedValue is null)
        {
            return BadRequest("Body cannot be empty");
        }
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(deserializedValue.Id), ActorType);
        var td = await actor.SetThingDescriptionAsync(deserializedValue);
        return Ok(td);
    }

    [HttpGet("{thingId}")]
    public async Task<IActionResult> GetThingDescription(string thingId)
    {
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
        string td = await actor.GetThingDescriptionAsync();
        return Content(td, "application/td+json");
    }

    [HttpGet("{thingId}/state")]
    public async Task<IActionResult> GetCurrentState(string thingId)
    {
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
        Dictionary<string, object?> state = await actor.GetCurrentStateAsync();
        if (state is null) return NotFound();
        return Content(JsonSerializer.Serialize(state), "application/td+json");
    }

    [HttpPost("{thingId}/action/{actionName}/execute")]
    public async Task<IActionResult> ExecuteAction(string thingId, string actionName, [FromBody] JsonElement body)
    {
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(thingId), ActorType);
        await actor.InvokeAction(actionName, body);
        return Ok();
    }



}
