using Microsoft.AspNetCore.Mvc;
using Events.Services;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Constants;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly RoutingService _routingService;
    private readonly SubscriptionManager _subscriptionManager;

    public EventsController(RoutingService routingService, SubscriptionManager subscriptionManager)
    {
        _routingService = routingService;
        _subscriptionManager = subscriptionManager;
    }

    [HttpPost("things/{idActor}")]
    public async Task<IActionResult> UpdateRoutes(string idActor, [FromBody] List<EventSubscription> events)
    {
        _routingService.UpdateEventsByActor(actor: new ActorIdentity(idActor, Actors.ThingActor), events);
        await _subscriptionManager.RefreshSubscriptionsAsync();
        return Ok("Routing actualizado.");
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        Console.WriteLine("Me ha llegado algo");
        return Ok("Routing actualizado.");
    }

    [HttpGet("")]
    public IActionResult GetSubscriptions()
    {
        var subscriptions = _routingService.GetSubscriptions();
        return Ok(subscriptions);
    }
}