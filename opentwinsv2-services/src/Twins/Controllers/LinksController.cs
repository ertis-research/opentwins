using System.Text.Json;
using Dapr;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Handlers;

namespace Twins.Controllers
{
    [ApiController]
    [Route("internal")]
    public class LinksController : ControllerBase
    {
        private readonly ILogger<LinksController> _logger;
        private readonly LinkEventsHandler _handler;

        public LinksController(LinkEventsHandler linkEventsHandler, ILogger<LinksController> logger)
        {
            _logger = logger;
            _handler = linkEventsHandler;
        }

        [Topic("kafka-pubsub", "opentwinsv2.links.changesNO")]
        [HttpPost("events/links")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleLinkEvent()
        {
            _logger.LogInformation("Received a new event from Kafka pubsub.");

            using var reader = new StreamReader(HttpContext.Request.Body);
            var body = await reader.ReadToEndAsync();

            var link = JsonSerializer.Deserialize<Link>(body);
            var ceSource = HttpContext.Request.Headers["Cloudevent.source"].FirstOrDefault();
            var ceType = HttpContext.Request.Headers["Cloudevent.type"].FirstOrDefault();

            if (string.IsNullOrEmpty(ceSource) || string.IsNullOrEmpty(ceType) || link == null)
            {
                _logger.LogWarning("Event discarded: missing source, type, or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {Source}: {Link}", ceSource, JsonSerializer.Serialize(link));
            await _handler.HandleAsync(link, ceSource, ceType);
            _logger.LogInformation("Event processed successfully.");

            return Ok();
        }
        
        [HttpPost("events/links/{source}/{type}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleLinkDapr(string source, string type, [FromBody] string newLink)
        {
            _logger.LogInformation("Received a new event from Dapr.");

            using var reader = new StreamReader(HttpContext.Request.Body);
            var body = await reader.ReadToEndAsync();

            var link = JsonSerializer.Deserialize<Link>(newLink);

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(type) || link == null)
            {
                _logger.LogWarning("Event discarded: missing source, type, or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {Source}: {Link}",source, JsonSerializer.Serialize(link));
            await _handler.HandleAsync(link, source, type);
            _logger.LogInformation("Event processed successfully.");

            return Ok();
        }
    }
}