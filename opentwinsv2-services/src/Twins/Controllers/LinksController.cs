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
            //await _handler.HandleAsync(link, ceSource, ceType);
            _logger.LogInformation("Event processed successfully.");

            return Ok();
        }

        [HttpPost("things/{source}/links")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleAddLinkDapr(string source, [FromBody] string jlink)
        {
            _logger.LogInformation("Received a new add link event from Dapr.");
            var link = JsonSerializer.Deserialize<Link>(jlink);

            if (string.IsNullOrEmpty(source) || link == null)
            {
                _logger.LogWarning("Event discarded: missing source or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {Source}: {Link}", source, JsonSerializer.Serialize(link));
            await _handler.HandleAddLinkAsync(source, link);
            _logger.LogInformation("Add link event processed successfully.");

            return Ok();
        }

        [HttpPut("things/{source}/links/{relName}/{*target}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleAddLinkDapr(string source, string target, string relName, [FromBody] string jlink)
        {
            _logger.LogInformation("Received a new update link event from Dapr.");
            var link = JsonSerializer.Deserialize<Link>(jlink);

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(relName) || link == null)
            {
                _logger.LogWarning("Event discarded: missing source, target, relation name or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {Source}: {Link}", source, JsonSerializer.Serialize(link));
            await _handler.HandleUpdateLinkAsync(source, target, relName, link);
            _logger.LogInformation("Update link event processed successfully.");

            return Ok();
        }
        
        [HttpDelete("things/{source}/links/{relName}/{*target}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleDeleteLinkDapr(string source, string target, string relName)
        {
            _logger.LogInformation("Received a new delete link event from Dapr.");

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(relName))
            {
                _logger.LogWarning("Event discarded: missing source, target, relation name or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {Source} to {Target}", source, target);
            await _handler.HandleDeleteLinkAsync(source, target, relName);
            _logger.LogInformation("Delete link event processed successfully.");

            return Ok();
        }
    }
}