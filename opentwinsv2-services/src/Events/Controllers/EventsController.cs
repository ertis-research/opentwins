using Microsoft.AspNetCore.Mvc;
using Events.Services;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Constants;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    private readonly RoutingService _routingService;
    private readonly SubscriptionManager _subscriptionManager;

    public EventsController(RoutingService routingService, SubscriptionManager subscriptionManager, ILogger<EventsController> logger)
    {
        _routingService = routingService;
        _subscriptionManager = subscriptionManager;
        _logger = logger;
    }

    [HttpPost("things/{idActor}")]
    public async Task<IActionResult> UpdateRoutes(string idActor, [FromBody] List<EventSubscription> events)
    {
        _logger.LogInformation("Updating routes for actor {IdActor} with {Count} events", idActor, events.Count);

        _routingService.UpdateEventsByActor(actor: new ActorIdentity(idActor, Actors.ThingActor), events);
        await _subscriptionManager.RefreshSubscriptionsAsync();

        _logger.LogInformation("Routes correctly updated for actor {IdActor}", idActor);
        return Ok("Routing updated.");
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
        _logger.LogDebug("Obtaining all event subscriptions");

        var subscriptions = _routingService.GetSubscriptions();
        return Ok(subscriptions);
    }
}