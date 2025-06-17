using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsv2.Things.Models;
using Shared.Models;

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
        //Console.WriteLine(value.GetRawText());
        var deserializedValue = JsonSerializer.Deserialize<ThingDescription>(value.GetRawText());
        if (deserializedValue is null)
        {
            return BadRequest("Body cannot be empty");
        }
        IThingActor actor = _actorProxyFactory.CreateActorProxy<IThingActor>(new ActorId(deserializedValue.Id), ActorType);
        var td = await actor.SetThingDescriptionAsync(value.GetRawText()); //MIRA ESTO LO HAGO Y NO PASO DIRECTAMENTE SERIALIZADO PORQUE ME CAGO EN TODO LO QUE ME HA COSTADO ESTO DIOS MIO SI LO PASO SERIALIZADO NO SE PASA BIEN NO PUEDO MAS SON LAS 9:30 QUIERO CENAR
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
