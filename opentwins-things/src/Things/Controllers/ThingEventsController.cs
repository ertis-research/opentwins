using Dapr;
using Dapr.Actors;
using Dapr.Actors.Client;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsv2.Things.Interfaces;
using OpenTwinsv2.Things.Services;

namespace OpenTwinsv2.Things.Controllers
{
    [ApiController]
    public class ThingEventsController : ControllerBase
    {
        [Topic("pubsub", "thing-topic")]
        [HttpPost("events/thing")]
        public async Task<IActionResult> HandleThingEvent([FromBody] IThingEvent evnt)
        {
            IThingActor proxy = ActorProxy.Create<ThingActor>(new ActorId(evnt.EventName), nameof(IThingActor));

            await proxy.HandleExternalEventAsync(evnt);
            return Ok();
        }
    }
}