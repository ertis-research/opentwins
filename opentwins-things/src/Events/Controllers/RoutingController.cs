using Microsoft.AspNetCore.Mvc;
using Events.Services;
using Dapr.Actors;

[ApiController]
[Route("[controller]")]
public class RoutingController : ControllerBase
{
    private readonly RoutingService _routingService;

    public RoutingController(RoutingService routingService)
    {
        _routingService = routingService;
    }

    [HttpPost("events/things/{idActor}")]
    public IActionResult UpdateRoutes(string idActor, [FromBody] string[] events)
    {
        _routingService.UpdateEventsByActor(ActorReference.Get(idActor), events);
        return Ok("Routing actualizado.");
    }
}