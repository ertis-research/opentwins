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
using Microsoft.OpenApi.Expressions;
using OpenTwinsV2.Orchestration.Formatters;
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
        /// <returns>
        /// Returns 200 Ok with a success message and the job identifier of the Pod.<br/>
        /// Returns 500 Internal Server Error if any issue is encountered while creating the configMap or the Pod itself.
        /// </returns>
        [HttpPost("")]
        [Consumes("text/plain")]
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
        /// <returns>
        /// Returns 204 No Content if the Pod is deleted successfully.<br/>
        /// Returns 404 Not Found if there is no Benthos Pod with such job identifier.<br/>
        /// Returns 500 Internal Server Error if any issue is encountered while deleting the pod or its configMap.
        /// </returns>
        [HttpDelete("{jobId}")]
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
        /// Returns all Connectors with their job identifier and Thing Description.
        /// </summary>
        /// <param name="page">The number of current page of Connectors.</param>
        /// <param name="pageSize">The size of the pages of Connectors.</param>
        /// <param name="filter">The optional string filter applied to Connectors. If not specified no filter will be applied.</param>
        /// <returns>
        /// Returns 200 Ok with the list of Connectors.<br/>
        /// Returns 500 Internal Server Error if any issue is encountered while obtaining the Connectors.
        /// </returns>
        [HttpGet("/connections/")]
        public async Task<IActionResult> GetAllConnectors(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null
        )
        {
            try
            {
                var list = await _benthosService.GetConnectors(page, pageSize, search);
                return Ok(list);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Error getting the Connectors: {ex.GetType} --> {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a Connector and its Benthos Pod with the thingDescription provided.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector.</param>
        /// <param name="thingDescription">The ThingDescription of the Connector.</param>
        /// <returns>
        /// Returns 200 Ok with a success message and the job identifier of the Pod.<br/>
        /// Returns 400 Bad Request if the thingId is empty or null, or if the ThingDescription provided to Things is of bad format.<br/>
        /// Returns 409 Conflict if there is already a Thing or a Benthos Pod with that identifier.<br/>
        /// Returns 500 Internal Server Error if any issue was encountered while connecting to Things, creating the Thing, obtaining the default ThingDescription or creating the Pod or 8its configMap.
        /// </returns>
        [HttpPost("/connections/")]
        public async Task<IActionResult> CreateConnector([FromBody] JsonNode thingDescription)
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

            if(await _benthosService.ExistsPod(jobId) || await _benthosService.ExistsConfigMap(thingId))
                return Conflict($"There is already a Benthos Pod with the id {thingId}");

            //first, check the health of things service
            if(!await _benthosService.CheckThingsHealth())
                return StatusCode(500, $"Something went wrong while creating the Connector: Thing Service is not available");


            if(await _benthosService.ExistsThing(thingId))
                return Conflict($"There is already a Thing with the thingId {thingId}.");
            
            try
            {
                await _benthosService.CreateConnector(thingId, thingDescription);
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
                return StatusCode(500, $"Something went wrong while creating the connector: {ex.GetType} --> {ex.Message}");
            }

            //once created, return success message
            return Ok(new {Message="Connector created successfully", JobId = jobId});
        }

        /// <summary>
        /// Creates a new Connector or modifies an existing one and its Benthos Pod.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="thingDescription">The ThingDescription of the Connector Thing.</param>
        /// <returns>
        /// Returns 200 Ok with a success message and the job identifier of the Pod.<br/>
        /// Returns 400 Bad Request if the thingId is empty or null, the ThingDescription provided to Things is of bad format or if the thingId of the request and the ThingDescription does not match.<br/>
        /// Returns 409 Conflict if there is already a Thing or a Benthos Pod with that identifier.<br/>
        /// Returns 500 Internal Server Error if any issue was encountered while connecting to Things, creating the Thing, obtaining the default ThingDescription or creating the Pod or 8its configMap.
        ///</returns>
        [HttpPut("/connections/{thingId}")]
        public async Task<IActionResult> CreateConnector(string thingId, [FromBody] JsonNode thingDescription)
        {
            if(thingDescription is null)
                return BadRequest("The Thing Description cannot be null.");

            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty");

            if(!await _benthosService.CheckThingsHealth())
                return StatusCode(500, $"Something went wrong while creating the Connector: Thing Service is not available");

            //Check if the thingId provided matched the one on the ThingDescription

            if(!thingDescription["id"]!.GetValue<string>().Equals(thingId))
                return BadRequest("The id in the ThingDescription doesn't match the provided thingId");

            var jobId = BenthosConfigParser.GetJobIdFromThingId(thingId);

            var modifies = await _benthosService.ExistsConnector(thingId);
            try
            {
                await (modifies ? _benthosService.ModifyConnector(thingId, thingDescription) : _benthosService.CreateConnector(thingId, thingDescription));
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
                return StatusCode(500, $"Something went wrong while {(modifies ? "modifying" : "creating")} the Connector: {ex.Message}");
            }
            return Ok(new {Message=$"Connector {(!modifies ? "created" : "modified")} successfully", JobId = jobId});
        }

        /// <summary>
        /// Deletes a Connector, including its Thing and its Benthos Pod.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector.</param>
        /// <returns>
        /// Returns 204 No Content if the Connector was deleted successfully.<br/>
        /// Returns 400 Bad Request if the identifier is empty or null.<br/>
        /// Returns 404 Not Found if the Thing or the Benthos Pod could not be found.<br/>
        /// Returns 500 Internal Server Error if any issue was encountered while connecting to Things, deleting the Pod, its configuration or its Thing. 
        /// </returns>
        [HttpDelete("/connections/{thingId}")]
        public async Task<IActionResult> DeleteConnector(string thingId)
        {

            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty.");

            if(!await _benthosService.ExistsPod(BenthosConfigParser.GetJobIdFromThingId(thingId),connector:true))
                return NotFound($"The Connector with id {thingId} was not found");

            if(!await _benthosService.ExistsThing(thingId))
                return NotFound($"There is no Thing Description with thingId {thingId}.");

            try
            {
                await _benthosService.DeleteConnector(thingId);
                return NoContent();
            }catch(Exception ex)
            {
                return StatusCode(500, $"Error cleaning up Benthos: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the Thing Description and job identifier of the Connector.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector</param>
        /// <returns>
        /// Returns 200 Ok with the Thing Description and the job identifier of the Connector.<br/>
        /// Returns 400 Bad Request if the identifier is null or empty.<br/>
        /// Returns 404 Not Found if the Thing or the Benthos Pod could not be found.<br/>
        /// Returns 500 Internal Server Error if any issue is encountered while connecting to Things, obtaining the ThingDescription or the Benthos Pod.
        /// </returns>
        [HttpGet("/connections/{thingId}")]
        public async Task<IActionResult> GetConnector(string thingId)
        {

            if(string.IsNullOrWhiteSpace(thingId))
                return BadRequest("The thingId cannot be null or empty.");

            //Response: ThingDescription + JobId
            
            if(!await _benthosService.ExistsThing(thingId))
                return NotFound($"There is no Thing with thingId {thingId}.");

            try
            {
                var (td, jobId) = await _benthosService.GetConnectorByThingId(thingId);
                return Ok(new {Message = $"Succesfully got {thingId} Connector info", ThingDescription = td, JobId = jobId});
            }
            catch (InvalidOperationException)
            {
                return NotFound($"No Connector with thingId {thingId} was found running");
            }            
            catch(Exception ex)
            {
                return StatusCode(500, $"Error getting the Connector {thingId}: {ex.GetType} --> {ex.Message}");
            }
        }
    }
}