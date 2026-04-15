using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Twins.Services;
using System;
using System.IO;
using System.Text;
using static Api.Dgraph;
using System.Threading.Channels;
using Grpc.Core;
using Channel = Grpc.Core.Channel;
using Api;
using Dgraph4Net;
using VDS.Common.Tries;
using VDS.RDF.Query.Algebra;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Cryptography;
using VDS.RDF.Ontology;
using OpenTwinsV2.Twins.Builders;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using System.Text.Json;
using static Lucene.Net.Queries.Function.ValueSources.MultiFunction;
using System.Globalization;
using VDS.RDF.Query.Expressions.Functions.XPath.Cast;
using System.Reflection.Metadata;
using Json.More;
using System.Text.RegularExpressions;
using VDS.RDF.Query.Datasets;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OpenTwinsV2.Twins.Models;
using Google.Rpc;
using Twins.Services;


namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("ontologies")]
    public class OntologiesController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;
        private readonly ExportService _exportService;
        private readonly ImportService _importService;
        private readonly InstanciationService _instanciationService;
        // private const string ActorType = Actors.ThingActor;

        public OntologiesController(DGraphService dgraphService, ThingsService thingsService, ExportService exportService, ImportService importService, InstanciationService instanciationService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
            _exportService = exportService;
            _importService = importService;
            _instanciationService = instanciationService;
        }

        /// <summary>
        /// Consults the Ontologies stored on the DataBase.
        /// </summary>
        /// <param name="page">The number of the desired page of Ontologies. By default: 1.</param>
        /// <param name="pageSize">The size of the pages. By default: 10.</param>
        /// <param name="search">The optional string filter to apply to the search. If not specified, no filter will be applied.</param>
        /// <returns>
        /// Returns a list of the ids of the ontologies stored on the DataBase.
        /// </returns>
        [HttpGet("")]
        public async Task<IActionResult> GetAllOntologiesId(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize=10, 
            [FromQuery] string? search = null)
        {
            try
            {
                return Ok(await _dgraphService.GetAllOntologiesIdsAsync(page, pageSize, search));
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something wrong happened while looking for ontologies in Dgraph:\n{ex.GetType}: {ex.Message}");
            }
        }
        /// <summary>
        /// Creates a new Ontology from the TTL File provided and with the identifier provided.
        /// </summary>
        /// <param name="ontologyFile">The TTL File that defines the Ontology's structure.</param>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <returns>
        /// Returns 200 Ok with the list of NQuads triples that were sent to the DataBase.<br/>
        /// Returns 400 Bad Request if the file provided is void or not of TTL Extension.<br/>
        /// Returns 409 Conflict if an ontology with the same identifier already exists on the DataBase.<br/>
        /// Returns 500 Internal Server Error if there were any issue while parsing the Ontology or while uploading it into the DataBase.
        /// </returns>
        [HttpPost("{ontologyId}")]
        public async Task<IActionResult> UploadOntology(IFormFile ontologyFile, string ontologyId)
        {
            //Check if the file has been uploaded correctly
            if (ontologyFile == null || ontologyFile.Length == 0)
            {
                return BadRequest("Something wrong with the uploaded file");
            }

            //Check if the file is of .ttl type
            var extension = Path.GetExtension(ontologyFile.FileName);
            if (extension == null || extension.ToLower() != ".ttl")
            {
                return BadRequest($"File can only be of .ttl extension, instead recieved a {(extension is null ? "no extension" : extension.ToLower())} file");
            }

            ontologyId = ontologyId.ToLowerInvariant();

            if (!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
            {
                //Parse to rdf
                //first we read the file data and save it in a IGraph variable
                ICollection<string>? nquads = null;
                JsonArray shapeGraph = [];
                // try
                // {
                    nquads = await _importService.GetFullOntologyNQuadsFromFile(ontologyId, ontologyFile, shapeGraph) ?? throw new Exception("The obtained NQuads list of the Ontology was null");
                // }
                // catch(Exception ex)
                // {
                //     return StatusCode(500, $"Something wrong happened while importing the Ontology to DGraph:\n{ex.GetType}: {ex.Message}");
                // }
                
                //---------------------------------------------------------------------------------
                //Upload the triples as a mutation to DGraph

                var response = await _dgraphService.AddNQuadTripleAsync(nquads.ToList());

                return Ok($"{response} {nquads.ToArray().Length} triples added to DGraph successfully");
                // return Ok(nquads);
            }
            return Conflict("There is already an ontology with this id");
        }

        /// <summary>
        /// Retrieves the List of Things with its Attributes and Relations that belong to the Ontology with the provided identifier.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <returns>
        /// Returns 200 Ok with the List of Things.<br/>
        /// Returns 404 Not Found if the Ontology was not found.<br/>
        /// Returns 500 if there were any issue retrieving the things from the DataBase.
        /// </returns>
        [HttpGet("{ontologyId}")]
        public async Task<IActionResult> GetThingsByOntologyId(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            var response = await _dgraphService.GetThingsInOntologyAsync(ontologyId);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves the Thing with its Attributes and Relations that has the thing identifier provided and also belongs to the Ontology specified.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns 200 Ok with the Thing information.<br/>
        /// Returns 404 Not Found if the Ontology or the Thing were not found.<br/>
        /// Returns 500 Internal Server Error if there were any issue while retrieving the Thing Information from the DataBase.
        /// </returns>
        [HttpGet("{ontologyId}/things/{thingId}")]
        public async Task<IActionResult> GetThingByOntologyAndThingId(string ontologyId, string thingId)
        {
            var check = await _dgraphService.ThingBelongsToOntologyAsync(ontologyId, thingId);
            if (!check)
            {
                return NotFound(new { message = $"Thing '{thingId}' does not belong to Ontology '{ontologyId}' or not exists" });
            }
            try
            {
                var result = await _dgraphService.GetThingInOntologyByIdAsync(ontologyId, thingId);
                return Ok(result);

            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }

        }

        /// <summary>
        /// Retrieves the list of all the pairs of Things related to each other with the Relation specified.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="relationName">The name of the Relation.</param>
        /// <returns>
        /// Returns 200 Ok with the list of pairs of the Relation.<br/>
        /// Returns 404 Not Found if either the Ontology was not found or there was no Relation with the specified name in the Ontology.<br/>
        /// Returns 500 Internal Server Error if there were any issue while retrieving the Relation pairs from the DataBase.
        /// </returns>
        [HttpGet("{ontologyId}/relations/{relationName}")]
        public async Task<IActionResult> GetThingsByRelationName(string ontologyId, string relationName)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            try
            {
                var result = await _dgraphService.GetRelationByName(ontologyId, relationName);
                if (result == null)
                    throw new KeyNotFoundException("Relation could not be found");
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }

        }

        /// <summary>
        /// Retrieves the list of values the specified Attribute of the Ontology takes and the thing associated to that value.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="attributeName">The name of the Attribute.</param>
        /// <returns>
        /// Returns 200 Ok with the list of values of the Attribute.<br/>
        /// Returns 404 Not Found if either the Ontology was not found or there was no Attribute with the name provided in the Ontology.<br/>
        /// Returns 500 Internal Server Error if there was any issue while retrieving the Attribute values from the DataBase.
        /// </returns>
        [HttpGet("{ontologyId}/attributes/{attributeName}")]
        public async Task<IActionResult> GetThingsByAttributeName(string ontologyId, string attributeName)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            try
            {
                var result = await _dgraphService.GetAttributeByName(ontologyId, attributeName);
                if (result == null)
                    throw new KeyNotFoundException("Attribute could not be found");
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Deleted an ontology by its identifier.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <returns>
        /// Returns 204 No Content if the Ontology was successfully deleted.<br/>
        /// Returns 404 Not Found if the Ontology was not Found.<br/>
        /// Returns 500 Internal Server Error if there was any issue while deleting the Ontology from the DataBase.
        /// </returns>
        [HttpDelete("{ontologyId}")]
        public async Task<IActionResult> DeleteOntology(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            try
            {
                var result = await _dgraphService.DeleteByOntologyId(ontologyId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while deleting the {ontologyId} ontology from DGraph: ${ex.GetType()} {ex.Message}");
            }

        }

        /// <summary>
        /// Deletes an specific Thing from the Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns 204 No Content if the Thing was successfully deleted from the Ontology and DataBase.<br/>
        /// Returns 404 Not Found if wither the Ontology was not found or there was no Thing with the provided identifier in the Ontology.<br/>
        /// Returns 505 Internal Server Error if there was any issue while deleting the Thing from the DataBase.
        /// </returns>
        [HttpDelete("{ontologyId}/things/{thingId}")]
        public async Task<IActionResult> DeleteThingFromOntology(string ontologyId, string thingId)
        {
            if (!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"The {ontologyId} ontology does not exist int DGraph");

            if (!await _dgraphService.ExistsThingInOntologyByIdAsync(ontologyId, thingId))
                return NotFound($"There is no {thingId} thing associated to the {ontologyId} ontology in DGraph, it cannot be deleted");

            try
            {
                var result = await _dgraphService.DeleteByOntologyIdAndThingId(ontologyId, thingId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while deleting the {thingId} thing from the {ontologyId} ontology from DGraph: {ex.GetType()} {ex.Message}");
            }

        }

        /// <summary>
        /// Returns the dependencies of a Thing that belongs to the Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns 200 Ok with the Thing's dependencies.<br/>
        /// Returns 404 Not Found if either the Ontology or the Thing was not found.<br/>
        /// Returns 500 Internal Server Error if something goes wrong while retrieving the dependencies from DGraph or formatting them.
        /// </returns>
        [HttpGet("{ontologyId}/things/{thingId}/relations/dependencies")]
        public async Task<IActionResult> GetThingDependenciesInOntology(string ontologyId, string thingId)
        {
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");
            if(!await _dgraphService.ExistsThingInOntologyByIdAsync(ontologyId, thingId))
                return NotFound($"There is no Thing with id {thingId} in {ontologyId} Ontology");

            try
            {
                return Ok(await _instanciationService.GetThingDependecies(ontologyId, thingId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the dependencies of {thingId} Thing in {ontologyId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Instanciates a Thing from the Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <param name="id">The identifier of the instanciated Thing.</param>
        /// <returns>
        /// Returns 200 Ok with a message of success.<br/>
        /// Returns 404 Not Found if either the Ontology was not found or there was no Thing with the provided id in the Ontology.<br/>
        /// Returns 409 Conflict if there is already an instanciated Thing with the provided identifier.<br/>
        /// Returns 500 Internal Server Error if there was any issue while instanciatinf the Thing.
        /// </returns>
        [HttpPost("{ontologyId}/things/{thingId}/instanciate/{id}")]
        public async Task<IActionResult> InstanciateThingOfOntology(string ontologyId, string thingId, string id)
        {
            //Instance of a Thing which is part of an ontology
            if (!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"The {ontologyId} ontology does not exist int DGraph");

            if (!await _dgraphService.ExistsThingInOntologyByIdAsync(ontologyId, thingId))
                return NotFound($"There is no {thingId} thing associated to the {ontologyId} ontology in DGraph, it cannot be instanciated");

            bool conflict = true;
            try
            {
                //Provisional: If it throws an exception, then the id is unused
                await _thingsService.GetThingAsync(id);
            }
            catch (KeyNotFoundException)
            {
                conflict = false;
            }

            if (!conflict) // Check if the id is already on use in thingsService 
            {
                //Get and format the thing's attributes to WOT
                var props = await _instanciationService.GetWOTThingProperties(ontologyId, thingId);

                var payload = new JsonObject
                {
                    ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                    ["id"] = id,
                    ["title"] = "",
                    ["hasType"] = thingId,
                    ["properties"] = props,
                    ["actions"] = new JsonObject { },
                    ["events"] = new JsonObject { }
                };


                var thingsResponse = await _thingsService.CreateThingAsync(payload);
                if (!thingsResponse) return StatusCode(500, "Failed to create Thing in things service");

                var dgraphResponse = await _dgraphService.AddThingAsync(ThingBuilder.BuildThing(thingId, id));
                bool dgraphOk = dgraphResponse != null && dgraphResponse.Uids != null && dgraphResponse.Uids.Count > 0;
                if (!dgraphOk) return StatusCode(500, "Failed to create thing in DGraph: " + dgraphResponse?.ToString());

                return Ok(new { message = "Thing created successfully" });
            }
            return Conflict($"There is already an instanced thing with the id {id}");
        }

        /// <summary>
        /// Instanciates the Things defined by the provided Graph following the Ontology structure requirements.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="subgraph">The subgraph of the Things to be instanciated.</param>
        /// <returns>
        /// Returns 200 Ok with a success message.<br/>
        /// Returns 204 No Content if the Graph provided was empty, therefore nothing was instanciated.<br/>
        /// Returns 400 BadRequest if either the identifier of the Ontology or the provided Graph are null or of bad format, or any Ontology dependecy was not met.<br/>
        /// Returns 404 Not Found if there is no Ontology with such identifier.<br/>
        /// Returns 409 Conflict if there are repited identifiers in the Graph.<br/>
        /// Returns 500 Internal Server Error if an issue was encountered while validating the Graph or instanciating the Things.
        /// </returns>
        [HttpPut("{ontologyId}/instanciate")]
        public async Task<IActionResult> InstanciateSubGraphOfOntology(string ontologyId, [FromBody] JsonElement subgraph)
        {
            if(string.IsNullOrWhiteSpace(ontologyId))
                return BadRequest("The Ontology id provided is either null or empty");
            
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");

            if(subgraph.AsNode() is null)
                return BadRequest("The provided Json Graph is not valid: obtained null");

            if(!(subgraph.TryGetProperty("@graph", out var graphElement) && graphElement.AsNode() is JsonArray graphNode))
                return BadRequest("The Json provided in the Body does not belong to a Json-Ld Graph.");

            if(graphNode.Count()==0)
                return NoContent();

            if(_instanciationService.AreThereConflictingIdsOnSubGraph(graphNode))
                return Conflict($"There are at least one conflicting id between some nodes of the subgraph provided.");

            try
            {
                await _instanciationService.ValidateGraph(ontologyId, subgraph);
            }catch(InvalidDataException ex)
            {
                return BadRequest(ex.Message);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while validating the Graph: {ex.Message}");
            }

            try
            {
                await _instanciationService.InstanciateThingGraph(ontologyId, graphNode);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while instanciating the Things: {ex.Message}");
            }

            return Ok("Success");
        }

        /// <summary>
        /// Instanciates the Twin and its Things defined by the provided Graph following the Ontology structure requirements.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <param name="subgraph">The subgraph of the Things to be instanciated.</param>
        /// <returns>
        /// Returns 200 Ok with a success message.<br/>
        /// Returns 204 No Content if the Graph provided was empty, therefore nothing was instanciated.<br/>
        /// Returns 400 BadRequest if either the identifier of the Ontology or the provided Graph are null or of bad format, or any Ontology dependecy was not met.<br/>
        /// Returns 404 Not Found if there is no Ontology with such identifier.<br/>
        /// Returns 409 Conflict if there are repited identifiers in the Graph or there is already a Twin with the identifier provided.
        /// Returns 500 Internal Server Error if an issue was encountered while validating the Graph or instanciating the Things.
        /// </returns>
        [HttpPut("{ontologyId}/instanciate/{twinId}")]
        public async Task<IActionResult> InstanciateTwinWithSubGraphOfOntology(string ontologyId, string twinId, [FromBody] JsonElement subgraph)
        {
            if(string.IsNullOrWhiteSpace(ontologyId))
                return BadRequest("The Ontology id provided is either null or empty");
            
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");

            if(await _dgraphService.ExistsTwinAsync(twinId))
                return Conflict($"There is already a Twin with the id {twinId}");

            if(subgraph.AsNode() is null)
                return BadRequest("The provided Json Graph is not valid: obtained null");

            if(!(subgraph.TryGetProperty("@graph", out var graphElement) && graphElement.AsNode() is JsonArray graphNode))
                return BadRequest("The Json provided in the Body does not belong to a Json-Ld Graph.");

            if(graphNode.Count()==0)
                return NoContent();

            if(_instanciationService.AreThereConflictingIdsOnSubGraph(graphNode))
                return Conflict($"There are at least one conflicting id between some nodes of the subgraph provided.");

            try
            {
                await _instanciationService.ValidateGraph(ontologyId, subgraph);
            }catch(InvalidDataException ex)
            {
                return BadRequest(ex.Message);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while validating the Graph: {ex.Message}");
            }

            try
            {
                await _instanciationService.InstanciateThingGraph(ontologyId, graphNode, twinId);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while instanciating the Things: {ex.Message}");
            }

            return Ok("Success");
        }

        /// <summary>
        /// Returns the Ontology in a JSON format.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology</param>
        /// <returns>
        /// Returns 200 Ok with the Ontology's JSON.<br/>
        /// Returns 404 Not Found if the Ontology was not found.<br/>
        /// Returns 500 Internal Server Error if there was any issue while obtaining the namespace or JSON of the Ontology.
        /// </returns>
        [HttpGet("{ontologyId}/export/Json")]
        public async Task<IActionResult> GetAllOntologyNodes(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }

            try
            {
                var ns = await _dgraphService.GetNamespacesInOntologyAsync(ontologyId);
                var json = await _exportService.GetJsonWithNamespace(ontologyId, ns);
                return Ok(json);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Failed to generate the {ontologyId} Ontology JSON: {ex.GetType()} {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the Ontology in a JSON-LD format.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <returns>
        /// Returns 200 Ok with the Ontology's JSON-LD.<br/>
        /// Returns 404 Not FOund if the Ontology was not found.<br/>
        /// Returns 500 Internal Server Error if either the namespace, JSON in regular format or final JSON-LD obtained were null. 
        /// </returns>
        [HttpGet("{ontologyId}/export/JsonLd")]
        public async Task<IActionResult> ExportOntologyInJsonLdFormat(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }

            var ns = await _dgraphService.GetNamespacesInOntologyAsync(ontologyId);
            var ontologyJson = await _exportService.GetJsonWithNamespace(ontologyId, ns);

            if (ontologyJson == null)
            {
                return StatusCode(500, "Something wrong with the Ontology Json:\nNull json recieved");//error 500, json malformado
            }

            if (ontologyJson["namespace"] == null)
            {
                return StatusCode(500, "Something wrong with the Ontology Json:\nNo namespace found");//error 500, json malformado
            }

            var jsonLd = ExportService.GetJsonLDFromRegularJson(ontologyJson, ontologyId);

            if (jsonLd is null)
            {
                return StatusCode(500, "Something went wrong while parsing");//error 500, json malformado
            }

            return Ok(jsonLd);
        }

        /// <summary>
        /// Returns Ontology in a TTL Format File. 
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <returns>
        /// Returns 200 Ok with the TTL File of the Ontology.<br/>
        /// Returns 404 Not Found if the Ontology was not found.<br/>
        /// Returns 500 Internal Server Error if either the namespace or the JSON of the Ontology obtained were null, or there was any issue generating the TTL File.
        /// </returns>
        [HttpGet("{ontologyId}/export/TTL")]
        public async Task<IActionResult> ExportOntologyInTTLFormat(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            
            var ns = await _dgraphService.GetNamespacesInOntologyAsync(ontologyId);
            var ontologyJson = await _exportService.GetJsonWithNamespace(ontologyId, ns);

            if (ontologyJson == null)
            {
                return StatusCode(500, "Something wrong with the Ontology Json:\nNull json recieved");//error 500, json malformado
            }

            if (ontologyJson["namespace"] == null)
            {
                return StatusCode(500, $"Something wrong with the Ontology Json:\nNo namespace found");//error 500, json malformado
            }

            try
            {
                return File(FormatService.GetTTLFileFromRegularJson(ontologyId, ontologyJson), "text/turtle", $"{ontologyId}_ontology.ttl");
            }
            catch (Exception e)
            {
                return StatusCode(500, $"Something wrong while parsing to TTL Format:{e}");
            }
        }

        [HttpGet("{ontologyId}/shapeGraph")]
        public async Task<IActionResult> GetShapeGraph(string ontologyId)
        {
            return Ok(await _exportService.BuildShapeGraphFromOntology(ontologyId));
        }

        /// <summary>
        /// Runs a SparQL query on the Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="stringQuery">The SparQL query on a String format.</param>
        /// <returns>
        /// Returns 200 Ok with the results of the query.<br/>
        /// Returns 204 No Content if the query was successfully run but no results were obtained.<br/>
        /// Returns 400 Bad Request if either the query was void or null, the query was not of SELECT or similar type, or its format was not valid<br/>
        /// Returns 404 Not Found if the Ontology was not found.
        /// Returns 500 Internal Server Error if there was any issue while processing or running the query on the Ontology.
        /// </returns>
        [HttpPost("{ontologyId}/query")]
        public async Task<IActionResult> SparQLQueryInOntology(string ontologyId, [FromForm] string stringQuery)
        {
            //check if the stringQuery is empty
            if (string.IsNullOrWhiteSpace(stringQuery))
            {
                return BadRequest($"The SparQL query cannot be empty");
            }
            //check if the ontology exists
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            //parse the query
            var parser = new SparqlQueryParser();
            try
            {
                var query = parser.ParseFromString(stringQuery);

                //only select queries are supported, anything else is blockes
                if (!(query.QueryType.ToString().StartsWith("Select", StringComparison.OrdinalIgnoreCase) || query.QueryType.Equals(SparqlQueryType.Ask))) //the are severlat select type queries
                {
                    return BadRequest($"Only SELECT queries are available, \"{stringQuery}\" is a \n{query.QueryType.ToString()} query");
                }

                //get the ontologyJson, the namespaces and the RDF graph
                var ns = await _dgraphService.GetNamespacesInOntologyAsync(ontologyId);
                var results = await _exportService.RunSparQLQuery(ontologyId, ns, query);
                if(results is null)
                    throw new Exception("Either the Json or the Graph are null");
                
                //parse results format so it is readable
                if (query.QueryType == SparqlQueryType.Ask)
                    return Ok(results.Result);

                if (results.IsEmpty)
                    return NoContent();

                var jsonResults = results.Select(r =>
                    r.Variables.ToDictionary(v => v, v => r[v]?.ToString())
                );

                return Ok(jsonResults);
            }
            catch (RdfParseException ex)
            {
                return BadRequest($"The format of the SparQL query \"{stringQuery}\" is wrong:\n{ex}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something wrong while processing the SparQl query:\n{ex.GetType}: {ex.Message}");
            }
        }
    }
}