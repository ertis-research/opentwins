using OpenTwinsV2.Twins.Services;
using Microsoft.AspNetCore.Mvc;
using Api;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("all")]
    public class AllController : ControllerBase
    {
        private readonly DGraphService _dgraphService;

        public AllController(DGraphService dgraphService)
        {
            _dgraphService = dgraphService;
        }

        [HttpGet("")]
        public async Task<IActionResult> GetEverything()
        {
            try
            {
                var result = await _dgraphService.GetEverythingAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve data.", details = ex.Message });
            }
        }

        [HttpPut("init")]
        public async Task<IActionResult> InitSchema()
        {
            try
            {
                Payload result = await _dgraphService.InitSchemaAsync();
                return Ok(new { message = "Init schema successfully.", result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to init schema", details = ex.Message });
            }
        }

        [HttpDelete("")]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                Payload result = await _dgraphService.DropAllAsync();
                return Ok(new { message = "All data dropped successfully.", result });
            }
            catch (Exception ex)
            {
                // Manejo de errores: puedes registrar el error si es necesario
                return StatusCode(500, new { error = "Failed to drop all data.", details = ex.Message });
            }
        }
    }
}