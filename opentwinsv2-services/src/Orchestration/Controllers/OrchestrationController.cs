using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Orchestration.Services;
using k8s;
using k8s.Models;
// using Microsoft.OpenApi.Expressions;
using OpenTwinsV2.Orchestration.Formatters;
using OpenTwinsV2.Shared.Models;
using System.Text.Json.Serialization;
// using OpenTwinsV2.Orchestration.Services.BenthosService;

namespace OpenTwinsV2.Orchestration.Controllers
{
    [ApiController]
    [Route("orchestration")]
    public class OrchestrationController : ControllerBase
    {
        private readonly BenthosService _benthosService;

        public OrchestrationController(BenthosService benthosService)
        {
            _benthosService = benthosService;
        }

        /// <summary>
        /// Deploys a Kubernetes Benthos Pod with the provided configuration.
        /// </summary>
        /// <param name="benthosConfig">Configuration of the Pod in yaml plain text.</param>
        /// <response code="200">The pod has been successfully created.</response>
        /// <response code="500">Error while creating the pod or its configMap.</response>
        [HttpPost("")]
        [Consumes("text/plain")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeployBenthos([FromBody] string benthosConfig)
        {
            if (string.IsNullOrWhiteSpace(benthosConfig))
                return BadRequest("Body cannot be empty. Send raw YAML.");

            //it won't return Conflict because the id is random, just randomize until it gets an unused one
            var jobId = "";
            do
            {
                jobId =$"benthos-job-{Guid.NewGuid().ToString().Substring(0, 6)}";
            }while(await _benthosService.ExistsPod(jobId) || await _benthosService.ExistsConfigMap(jobId));

            try
            {
                //create the configMap in k8s
                await _benthosService.CreateConfigMap(_benthosService.ParseConfigMap(jobId, benthosConfig) ?? throw new Exception("The Configuration Mapping went wrong and recieved null"));
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while creating the Config Map: {ex.GetType} --> {ex.Message}");
            }

            try
            {
                //create the k8s pod
                await _benthosService.CreateBenthosPod(jobId);
                
                return Ok(new { Message = $"Benthos worker deployed", JobId = jobId});
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while creating Benthos: {ex.GetType} --> {ex.Message}");
            }
        }

        /// <summary>
        /// Kills the Benthos Pod with the provided job identifier.
        /// </summary>
        /// <param name="jobId">The identifier of the Benthos pod's job.</param>
        /// <response code="204">The pod has been successfully deleted.</response>
        /// <response code="404">There is no pod with the provided identifier.</response>
        /// <response code="500">Error while deleting the pod or its configMap.</response>
        [HttpDelete("{jobId}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> KillJob(string jobId)
        {
            
            if(!await _benthosService.ExistsPod(jobId))
                return NotFound($"The Pod with id {jobId} was not found");
            try
            {
                await _benthosService.DeleteBenthosJob(jobId);

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error cleaning up: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns all Connections with their Thing Description.
        /// </summary>
        /// <param name="page">The number of current page of Connections.</param>
        /// <param name="pageSize">The size of the pages of Connections.</param>
        /// <param name="search">The optional string filter applied to Connections. If not specified no filter will be applied.</param>
        /// <response code="200">All the Connections.</response>
        /// <response code="500">Error while obtaining the Connections.</response>
        [HttpGet("/connections/")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllConnections(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null
        )
        {
            try
            {
                var list = await _benthosService.GetConnections(page, pageSize, search);
                return Ok(list);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Error getting the Connections: {ex.GetType} --> {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a Connection and its Benthos Pod with the thingDescription provided.
        /// </summary>
        /// <param name="thingDescription">The ThingDescription of the Connection.</param>
        /// <response code="200">The Connection has been successfully created.</response>
        /// <response code="400">The provided identifier was null or empty.</response>
        /// <response code="409">There is already a Thing or a pod with the provided identifier</response>
        /// <response code="500">Error while connecting to Things, obtaining the default ThingDescription or creating the Thing, pod or configMap.</response>
        [HttpPost("/connections/")]
        [SwaggerExample(OrchestrationAPIExamples.CreateConnection)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateConnection([FromBody] JsonNode thingDescription)
        {
            if(thingDescription is null)
                return BadRequest("The Thing Description cannot be null.");

            //The id must be of the form urn:connections:{input}:{thingId}
            string thingId="";
            try
            {
                thingId = thingDescription["id"]!.GetValue<string>();
            }
            catch (Exception ex)
            {
                return BadRequest($"The id provided is wrong: {ex.Message}");
            }

            var jobId = BenthosConfigParser.GetJobIdFromThingId(thingId);

            if(await _benthosService.ExistsPod(jobId))
                return Conflict($"There is already a Benthos Pod with the id {thingId}");

            if(await _benthosService.ExistsConfigMap(thingId))
                return Conflict($"There is already a configMap for a Benthos Pod with the id {thingId}");

            //first, check the health of things service
            if(!await _benthosService.CheckThingsHealth())
                return StatusCode(500, $"Something went wrong while creating the Connection: Thing Service is not available");


            if(await _benthosService.ExistsThing(thingId))
                return Conflict($"There is already a Thing with the thingId {thingId}.");
            
            try
            {
                await _benthosService.CreateConnection(thingId, thingDescription);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(Exception ex)
            {
                //The Thing and ConfigMap deletions is already controlled in Service.
                return StatusCode(500, $"Something went wrong while creating the Connection: {ex.GetType} --> {ex.Message}");
            }

            //once created, return success message
            return Ok(new {Message="Connection created successfully", JobId = jobId});
        }

        /// <summary>
        /// Creates a new Connection or modifies an existing one and its Benthos Pod.
        /// </summary>
        /// <param name="thingId">The identifier of the Connection Thing.</param>
        /// <param name="thingDescription">The ThingDescription of the Connection Thing.</param>
        /// <response code="200">The Connection's been successfully edited or created.</response>
        /// <response code="400">The provided identifier was null or empty, the ThingDescription is malformed or if its identifier and the provided one do not match.</response>
        /// <response code="409">There is already a Thing or a Benthos Pod with the provided identifier.</response>
        /// <response code="500">Error while connecting to Things, obtaining the default ThingDescription or creating the Thing, pod or configMap.</response>
        [HttpPut("/connections/{thingId}")]
        [SwaggerExample(OrchestrationAPIExamples.CreateConnection)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateConnection(string thingId, [FromBody] JsonNode thingDescription)
        {
            if(thingDescription is null)
                return BadRequest("The Thing Description cannot be null.");

            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty");

            if(!await _benthosService.CheckThingsHealth())
                return StatusCode(500, $"Something went wrong while creating the Connection: Thing Service is not available");

            //Check if the thingId provided matched the one on the ThingDescription

            if(!thingDescription["id"]!.GetValue<string>().Equals(thingId))
                return BadRequest("The id in the ThingDescription doesn't match the provided thingId");

            var jobId = BenthosConfigParser.GetJobIdFromThingId(thingId);

            var modifies = await _benthosService.ExistsConnection(thingId);
            try
            {
                await (modifies ? _benthosService.ModifyConnection(thingId, thingDescription) : _benthosService.CreateConnection(thingId, thingDescription));
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }catch(InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while {(modifies ? "modifying" : "creating")} the Connection: {ex.Message}");
            }
            return Ok(new {Message=$"Connection {(!modifies ? "created" : "modified")} successfully", JobId = jobId});
        }

        /// <summary>
        /// Deletes a Connection, including its Thing and its Benthos Pod.
        /// </summary>
        /// <param name="thingId">The identifier of the Connection.</param>
        /// <response code="204">The Connection was successfully deleted.</response>
        /// <response code="400">The provided identifier was null or empty.</response>
        /// <response code="404">There is no Connection with the provided identifier.</response>
        /// <response code="500">Error while connecting to Things, deleting the Pod, its configuration or its Thing.</response>
        [HttpDelete("/connections/{thingId}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteConnection(string thingId)
        {

            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty.");

            if(!await _benthosService.ExistsPod(BenthosConfigParser.GetJobIdFromThingId(thingId),Connection:true))
                return NotFound($"The Connection with id {thingId} was not found");

            if(!await _benthosService.ExistsThing(thingId))
                return NotFound($"There is no Thing Description with thingId {thingId}.");

            try
            {
                await _benthosService.DeleteConnection(thingId);
                return NoContent();
            }catch(Exception ex)
            {
                return StatusCode(500, $"Error cleaning up Benthos: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the Thing Description of the Connection.
        /// </summary>
        /// <param name="thingId">The identifier of the Connection</param>
        /// <response code="200">The Connection's ThingDescription.</response>
        /// <response code="400">The provided identifier was null or empty.</response>
        /// <response code="404">There is no Connection with the provided identifier.</response>
        /// <response code="500">Error while connecting to Things, obtaining the ThingDescription or the Benthos Pod.</response>
        [HttpGet("/connections/{thingId}")]
        [Produces(typeof(ThingDescription))]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConnectionThingDescription(string thingId)
        {

            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty.");

            //Response: ThingDescription
            
            if(!await _benthosService.ExistsThing(thingId))
                return NotFound($"There is no Thing with thingId {thingId}.");

            try
            {
                var (td, _) = await _benthosService.GetConnectionByThingId(thingId);
                return Ok(td);
            }
            catch (InvalidOperationException)
            {
                return NotFound($"No Connection with thingId {thingId} was found running");
            }            
            catch(Exception ex)
            {
                return StatusCode(500, $"Error getting the Connection {thingId}: {ex.GetType} --> {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the Connection's k8s pod information.
        /// </summary>
        /// <param name="thingId">The identifier of the Connection.</param>
        /// <response code="200">The pod information of the Connection.</response>
        /// <response code="400">The provided identifier is null or empty.</response>
        /// <response code="404">There is no Connection with the provided identifier.</response>
        [HttpGet("/connections/{thingId}/pod")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Pod), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConnectionPod(string thingId)
        {
            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty.");

            if(!await _benthosService.ExistsThing(thingId))
                return NotFound($"There is no Thing with thingId {thingId}.");
            
            var localOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null, // Keeps PascalCase
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // return Content(JsonSerializer.Serialize(await _benthosService.GetConnectionPodInfo(thingId), localOptions), "application/json");
            // return new JsonResult(await _benthosService.GetConnectionPodInfo(thingId), new JsonSerializerOptions
            // {
            //     PropertyNamingPolicy = null // Keeps PascalCase locally
            // });

            return Ok(await _benthosService.GetConnectionPodInfo(thingId));
        }

        /// <summary>
        /// Retrieves the Connection's k8s pod logs.
        /// </summary>
        /// <param name="thingId">The identifier of the Connection.</param>
        /// <response code="200">The Connection's pod logs.</response>
        /// <response code="400">The provided identifier was null or empty.</response>
        /// <response code="404">There is no Connection with the provided identifier.</response>
        [HttpGet("/connections/{thingId}/logs")]
        [ProducesResponseType(typeof(Pod), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConnectionLogs(string thingId)
        {
            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty.");

            if(!await _benthosService.ExistsThing(thingId))
                return NotFound($"There is no Thing with thingId {thingId}.");


            return Ok(await _benthosService.GetConnectionPodLogs(thingId));
        }

        /// <summary>
        /// Retrieves the Connection's k8s configMap information.
        /// </summary>
        /// <param name="thingId">The identifier of the Connection.</param>
        /// <response code="200">The Connection's configMap information.</response>
        /// <response code="400">The provided identifier was null or empty.</response>
        /// <response code="404">There is no Connection with the provided identifier.</response>
        [HttpGet("/connections/{thingId}/configMap")]
        [ProducesResponseType(typeof(ConfigMap), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConnectionConfigMap(string thingId)
        {
            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty.");

            if(!await _benthosService.ExistsThing(thingId))
                return NotFound($"There is no Thing with thingId {thingId}.");

            return Ok(await _benthosService.GetConfigMapInfoOfThing(thingId));
        }
    }
}