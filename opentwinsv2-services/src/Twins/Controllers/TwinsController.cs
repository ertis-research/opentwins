using OpenTwinsV2.Twins.Services;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Things.Models;
using Dapr.Actors;
using Dapr.Actors.Client;
using System.Text.Json;
using OpenTwinsV2.Twins.Models;
using Dapr.Client;
using OpenTwinsV2.Shared.Constants;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("twins")]
    public class GraphController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private const string ActorType = Actors.ThingActor;

        public GraphController(DGraphService dgraphService)
        {
            _dgraphService = dgraphService;
        }

        [HttpPost("{twinId}")]
        public async Task<IActionResult> CreateTwin(string twinId)
        {
            Console.WriteLine(twinId);
            if (!await _dgraphService.ExistsThingByIdAsync(twinId))
            {
                var response = await _dgraphService.AddThingAsync(new ThingNode(twinId, twinId, true));
                return Ok(response);
                /*
                var client = DaprClient.CreateInvokeHttpClient();
                var cts = new CancellationTokenSource();
                var payload = new
                {
                    @context = new string[]
                    {
                    "https://www.w3.org/2019/wot/td/v1"
                    },
                    id = twinId,
                    title = "",
                    securityDefinitions = new
                    {
                        nosec_sc = new { scheme = "nosec" }
                    },
                    security = new[] { "nosec_sc" },
                    properties = new { },
                    actions = new { },
                    events = new { }
                };
                var response = await client.PostAsJsonAsync($"http://things-service/things", payload, cts.Token);
                return Ok(response);
                */


            }
            return Conflict("There is already a twin with this id");
        }

        [HttpGet("{twinId}")]
        public async Task<IActionResult> GetTwin(string twinId)
        {
            var response = await _dgraphService.GetThingsInTwinAsync(twinId);
            return Ok(response);
        }


        [HttpPost("{twinId}/thing/{thingId}")]
        public async Task<IActionResult> AddThingToTwin(string twinId, string thingId)
        {
            string response = "";

            if (!await _dgraphService.ExistsThingByIdAsync(thingId))
            { // Add to twinId list
                var proxy = ActorProxy.Create<IThingActor>(new ActorId(thingId), ActorType);
                Console.WriteLine("asas");
                var thingDescription = await proxy.GetThingDescriptionAsync();
                Console.WriteLine(thingDescription);
                if (thingDescription != null)
                {
                    ThingDescription? td = JsonSerializer.Deserialize<ThingDescription>(thingDescription);
                    if (td != null && td.Id != null)
                    {
                        response += await _dgraphService.AddThingAsync(ThingMapper.MapToThingNode(td));
                        return Ok(response);
                    }
                    return BadRequest();
                }
                return NotFound();
            }

            response += await _dgraphService.AddThingToTwinAsync(twinId, thingId);
            return Ok(response);
        }

        [HttpGet("{twinId}/thing/{thingId}")]
        public async Task<IActionResult> GetThingInTwin(string twinId, string thingId)
        {
            var response = await _dgraphService.GetThingInTwinAsync(twinId, thingId);
            return Ok(response);
        }

        [HttpDelete("{twinId}/thing/{thingId}")]
        public async Task<IActionResult> DeleteThingFromTwin(string twinId, string thingId)
        {
            return Ok(await _dgraphService.RemoveThingFromTwinAsync(twinId, thingId));
        }

        [HttpPost("edge")]
        public async Task<IActionResult> CreateEdge([FromQuery] string fromUid, [FromQuery] string toUid, [FromQuery] string predicate)
        {
            var response = await _dgraphService.LinkNodesAsync(fromUid, predicate, toUid);
            return Ok(response);
        }
    }
}