using OpenTwinsv2.Twins.Services;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsv2.Things.Models;
using Dapr.Actors;
using Dapr.Actors.Client;
using System.Text.Json;
using OpenTwinsv2.Twins.Models;
using Dapr.Client;

namespace OpenTwinsv2.Twins.Controllers
{
    [ApiController]
    [Route("twins")]
    public class GraphController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private const string ActorType = "ThingActor";

        public GraphController(DGraphService dgraphService)
        {
            _dgraphService = dgraphService;
        }
        /*
        {
          "@context": [
            "https://www.w3.org/2019/wot/td/v1",
            {
              "@language": "en"
            }
          ],
          "id": "",
          "title": "",
          "securityDefinitions": {
            "nosec_sc": {
              "scheme": "nosec"
            }
          },
          "security": ["nosec_sc"],
          "properties": {},
          "actions": {},
          "events": {}
        }
        */

        [HttpPost("{twinId}")]
        public async Task<IActionResult> CreateTwin([FromBody] string twinId)
        {
            Console.WriteLine(twinId);
            var client = DaprClient.CreateInvokeHttpClient();
            var cts = new CancellationTokenSource();
            var payload = new
            {
                @context = new object[]
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
        }


        [HttpPost("node")]
        public async Task<IActionResult> CreateNode([FromBody] string thingId)
        {
            Console.WriteLine(thingId);
            var proxy = ActorProxy.Create<IThingActor>(new ActorId(thingId), ActorType);
            Console.WriteLine("asas");
            var thingDescription = await proxy.GetThingDescriptionAsync();
            Console.WriteLine(thingDescription);
            if (thingDescription != null)
            {
                ThingDescription? td = JsonSerializer.Deserialize<ThingDescription>(thingDescription);
                if (td != null)
                {
                    var response = await _dgraphService.AddThingAsync(ThingMapper.MapToThingNode(td));
                    return Ok(response);
                }
                return BadRequest();
            }
            return NotFound();
        }

        [HttpPost("edge")]
        public async Task<IActionResult> CreateEdge([FromQuery] string fromUid, [FromQuery] string toUid, [FromQuery] string predicate)
        {
            var response = await _dgraphService.LinkNodesAsync(fromUid, predicate, toUid);
            return Ok(response);
        }
    }
}