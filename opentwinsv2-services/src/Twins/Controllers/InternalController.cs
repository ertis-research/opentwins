using System.Text.Json;
using Dapr;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Handlers;
using OpenTwinsV2.Twins.Services;

namespace Twins.Controllers
{
    [ApiController]
    [Route("internal")]
    public class InternalController : ControllerBase
    {
        private readonly ILogger<InternalController> _logger;
        private readonly LinkEventsHandler _handler;
        private readonly DGraphService _dgraphService;

        public InternalController(LinkEventsHandler linkEventsHandler, ILogger<InternalController> logger, DGraphService dgraphService)
        {
            _logger = logger;
            _handler = linkEventsHandler;
            _dgraphService = dgraphService;
        }

        [HttpDelete("things/{thingId}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleDeleteThing(string thingId)
        {
            _logger.LogInformation("Received a new delete thing event from Dapr.");

            if (string.IsNullOrEmpty(thingId))
            {
                _logger.LogWarning("Event discarded: missing source or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing delete {thingId}", thingId);
            try
            {
                await _dgraphService.DeleteThingAsync(thingId);
                _logger.LogInformation("Add link event processed successfully.");
                return Ok();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Unexpected error: {ex.Message}");
            }
        }

        [HttpPost("things/{thingId}/links")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleAddLinkDapr(string thingId, [FromBody] string jlink)
        {
            _logger.LogInformation("Received a new add link event from Dapr.");
            var link = JsonSerializer.Deserialize<Link>(jlink);

            if (string.IsNullOrEmpty(thingId) || link == null)
            {
                _logger.LogWarning("Event discarded: missing source or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {Source}: {Link}", thingId, JsonSerializer.Serialize(link));
            await _handler.HandleAddLinkAsync(thingId, link);
            _logger.LogInformation("Add link event processed successfully.");

            return Ok();
        }

        [HttpPut("things/{thingId}/links/{relName}/{*target}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleAddLinkDapr(string thingId, string target, string relName, [FromBody] string jlink)
        {
            _logger.LogInformation("Received a new update link event from Dapr.");
            var link = JsonSerializer.Deserialize<Link>(jlink);

            if (string.IsNullOrEmpty(thingId) || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(relName) || link == null)
            {
                _logger.LogWarning("Event discarded: missing source, target, relation name or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {thingId}: {Link}", thingId, JsonSerializer.Serialize(link));
            await _handler.HandleUpdateLinkAsync(thingId, target, relName, link);
            _logger.LogInformation("Update link event processed successfully.");

            return Ok();
        }
        
        [HttpDelete("things/{thingId}/links/{relName}/{*target}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> HandleDeleteLinkDapr(string thingId, string target, string relName)
        {
            _logger.LogInformation("Received a new delete link event from Dapr.");

            if (string.IsNullOrEmpty(thingId) || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(relName))
            {
                _logger.LogWarning("Event discarded: missing thingId, target, relation name or link data.");
                return Ok();
            }

            _logger.LogDebug("Processing Link from {thingId} to {Target}", thingId, target);
            await _handler.HandleDeleteLinkAsync(thingId, target, relName);
            _logger.LogInformation("Delete link event processed successfully.");

            return Ok();
        }
    }
}