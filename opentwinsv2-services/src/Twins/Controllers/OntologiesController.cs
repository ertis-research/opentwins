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
using OpenTwinsV2.Shared.Models;
using Twins.Models;


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
        /// <param name="page">The number of the desired page of Ontologies.</param>
        /// <param name="pageSize">The size of the pages.</param>
        /// <param name="search">The optional string filter to apply to the search. If not specified, no filter will be applied.</param>
        /// <response code="200">List of the ids of the ontologies stored on the DataBase.</response>
        [HttpGet("")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PagedResult<JsonElement>), StatusCodes.Status200OK)]
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
        /// <response code="200">The list of NQuads triples that were sent to the DataBase.</response>
        /// <response code="400">The file provided is void or not of TTL Extension.</response>
        /// <response code="409">An ontology with the same identifier already exists on the DataBase.</response>
        /// <response code="500">There was an issue while parsing the Ontology or while uploading it into the DataBase.</response>
        [HttpPost("{ontologyId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
                try
                {
                    nquads = await _importService.GetFullOntologyNQuadsFromFile(ontologyId, ontologyFile, shapeGraph) ?? throw new Exception("The obtained NQuads list of the Ontology was null");
                }
                catch(Exception ex)
                {
                    return StatusCode(500, $"Something wrong happened while importing the Ontology to DGraph:\n{ex.GetType}: {ex.Message}");
                }
                
                //---------------------------------------------------------------------------------
                //Upload the triples as a mutation to DGraph

                var response = await _dgraphService.AddNQuadTripleAsync(nquads.ToList());
                // foreach(var nq in nquads)
                //     Console.WriteLine(nq);
                return Ok($"{response} {nquads.ToArray().Length} triples added to DGraph successfully.{(shapeGraph.Count>0 ? $" Created Shape Graph with id {ontologyId}_defaultshapegraph." : "")}");
                // return Ok(nquads);
            }
            return Conflict("There is already an ontology with this id");
        }

        /// <summary>
        /// Retrieves the List of Things with its Attributes and Relations that belong to the Ontology with the provided identifier.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <response code="200">The List of default Shape Graphs and Things.</response>
        /// <response code="404">The Ontology was not found.</response>
        /// <response code="500">There was an issue retrieving the things from the DataBase.</response>
        [HttpGet("{ontologyId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="200">The Thing information.</response>
        /// <response code="404">The Ontology or the Thing were not found.</response>
        /// <response code="500">There was an issue while retrieving the Thing Information from the DataBase.</response>
        [HttpGet("{ontologyId}/things/{thingId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="200">List of pairs of the Relation.</response>
        /// <response code="404">Either the Ontology was not found or there was no Relation with the specified name in the Ontology.</response>
        /// <response code="500">There was an issue while retrieving the Relation pairs from the DataBase.</response>
        [HttpGet("{ontologyId}/relations/{relationName}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="200">List of values of the Attribute.</response>
        /// <response code="404">Either the Ontology was not found or there was no Attribute with the name provided in the Ontology.</response>
        /// <response code="500">There was an issue while retrieving the Attribute values from the DataBase.</response>
        [HttpGet("{ontologyId}/attributes/{attributeName}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// Retrieves the list of thingId of the Things that inherit from the Thing with the provided identifier in the Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <response code="200">List of identifiers of the Thing's inheritance children.</response>
        /// <response code="204">The Thing does not have children.</response>
        /// <response code="404">Either the Ontology or the Thing in said Ontology could not be found.</response>
        /// <response code="500">An issue was encountered while retrieving the children list.</response>
        [HttpGet("{ontologyId}/things/{thingId}/children")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetChildrenOfThingInOntology(string ontologyId, string thingId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if(!check)
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });

            check = await _dgraphService.ExistsThingInOntologyByIdAsync(ontologyId, thingId);
            if(!check)
                return NotFound(new { message = $"There is no Thing with id {thingId} in {ontologyId} Ontology."});

            try
            {
                var children = await _dgraphService.GetThingsChildrenInOntology(ontologyId, thingId);
                if(!children.Any())
                    return NoContent();
                else
                    return Ok(children);
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while obtaining the Thing's children: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the information of the Thing that the provided Thing inheritd from..
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <response code="200">Information of the Thing's parent.</response>
        /// <response code="204">The Thing does not inherit from any Thing.</response>
        /// <response code="404">Either the Ontology or the Thing in said Ontology could not be found.</response>
        /// <response code="500"> An issue was encountered while retrieving the parent.</response>
        [HttpGet("{ontologyId}/things/{thingId}/parent")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetParentOfThingInOntology(string ontologyId, string thingId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if(!check)
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });

            check = await _dgraphService.ExistsThingInOntologyByIdAsync(ontologyId, thingId);
            if(!check)
                return NotFound(new { message = $"There is no Thing with id {thingId} in {ontologyId} Ontology."});

            try
            {
                var parent = await _dgraphService.GetThingsParentInOntology(ontologyId, thingId);
                if(parent is null)
                    return NoContent();
                else
                    return Ok(await _dgraphService.GetThingInOntologyByIdAsync(ontologyId, parent)); //ASK: restricted to only ontology, is that right?

            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while obtaining the Thing's parent: {ex.Message}");
            }
        }

        /// <summary>
        /// Deleted an ontology by its identifier.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <response code="204">The Ontology was successfully deleted.</response>
        /// <response code="404">The Ontology was not Found.</response>
        /// <response code="500">There was an issue while deleting the Ontology from the DataBase.</response>
        [HttpDelete("{ontologyId}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="204">The Thing was successfully deleted from the Ontology and DataBase.</response>
        /// <response code="404">Either the Ontology was not found or there was no Thing with the provided identifier in the Ontology.</response>
        /// <response code="500"> There was an issue while deleting the Thing from the DataBase.</response>
        [HttpDelete("{ontologyId}/things/{thingId}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="200">The Thing's dependencies.</response>
        /// <response code="404">Either the Ontology or the Thing was not found.</response>
        /// <response code="500"> Something went wrong while retrieving the dependencies from DGraph or formatting them.</response>
        [HttpGet("{ontologyId}/things/{thingId}/relations/dependencies")]
        [Produces("application/json")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ProducesResponseType(typeof(ThingDependency), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// <response code="200">A message of success.</response>
        /// <response code="404">Either the Ontology was not found or there was no Thing with the provided id in the Ontology.</response>
        /// <response code="409">There is already an instanciated Thing with the provided identifier.</response>
        /// <response code="500">There was an issue while instanciatinf the Thing.</response>
        [HttpPost("{ontologyId}/things/{thingId}/instanciate/{id}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
            }catch(InvalidOperationException){}

            if (!conflict) // Check if the id is already on use in thingsService 
            {
                try
                {
                    await _instanciationService.InstanciateThingGraph(ontologyId, [new JsonObject{["@id"] = id, ["id"] = id, ["@type"] = thingId}]);
                }catch(Exception ex)
                {
                    return StatusCode(500, $"Something went wrong while instanciating the thing {thingId}: {ex.Message}");
                }
                return Ok(new { message = "Thing created successfully" });
            }
            return Conflict($"There is already an instanced thing with the id {id}");
        }

        /// <summary>
        /// Retrieves the list of identifiers of the default Shape Graphs of an Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <response code="200">The list of identifiers of default Shape Graphs of the Ontology.</response>
        /// <response code="204">The Ontology does not have any default Shape Graphs.</response>
        /// <response code="404">No Ontology with the provided identifier could be found.</response>
        /// <response code="500">An issue was encountered while obtaining the default Shape Graphs of the Ontology.</response>
        [HttpGet("{ontologyId}/defaultShapeGraphs")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDefaultShapeGraphsOfOntology(string ontologyId)
        {
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");

            try
            {
                return Ok(await _dgraphService.GetOntologysDefaultShapeGraphs(ontologyId));
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while obtaining the defaut Shape Graphs of the Ontology:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Adds a Shape Graph as defualt in an Ontology. 
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <response code="200">A success message.</response>
        /// <response code="409">The Shape Graph is already default in the Ontology.</response>
        /// <response code="404">The Ontology or the Shape Graph do not exist.</response>
        /// <response code="500">An issue was encountered while uploading the changes.</response>
        [HttpPut("{ontologyId}/defaultShapeGraphs/{shapeId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddDefaultShapeGraphToOntology(string ontologyId, string shapeId)
        {
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");

            if(!await _dgraphService.ExistsShapeGraphByIdAsync(shapeId))
                return NotFound($"There is no Shape Graph with id {shapeId}");

            if((await _dgraphService.GetOntologysDefaultShapeGraphs(ontologyId)).Contains(shapeId))
                return Conflict($"The {shapeId} Shape Graph is already default in the {ontologyId} Ontology.");

            if(await _dgraphService.AddDefaultShapeGraphInOntology(ontologyId, shapeId))
                return Ok("Shape Graph successfully added");
            else
                return StatusCode(500, $"Something went wrong while adding the defaut Shape Graph {shapeId} in the Ontology and was not added");
        }

        /// <summary>
        /// Unlinks the given Shape Graph from the defaults of the Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <response code="200">A success message.</response>
        /// <response code="400">The provided Shape Graph is not a default Shape Graph in the given Ontology.</response>
        /// <response code="404">Either the Ontology or the Shape Graph do not exist.</response>
        /// <response code="500">An issue was encountered while unlinking the Shape Graph from the Ontology.</response>
        [HttpDelete("{ontologyId}/defaultShapeGraphs/{shapeId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UnlinkDefaultShapeGraphInOntology(string ontologyId, string shapeId)
        {
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");

            if(!await _dgraphService.ExistsShapeGraphByIdAsync(shapeId))
                return NotFound($"There is no Shape Graph with id {shapeId}");

            if(!(await _dgraphService.GetOntologysDefaultShapeGraphs(ontologyId)).Contains(shapeId))
                return BadRequest($"The {shapeId} Shape Graph is not marked as default in the {ontologyId} Ontology.");

            if(await _dgraphService.DeleteDefaultShapeGraphInOntology(ontologyId, shapeId))
                return Ok("Shape Graph successfully removed");
            else
                return StatusCode(500, $"Something went wrong while removing the defaut Shape Graph {shapeId} in the Ontology and changes were not applied.");
        }

        /// <summary>
        /// Unlinks all default Shape Graphs from the Ontology. 
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <response code="200">A success message.</response>
        /// <response code="404">The Ontology could not be found.</response>
        /// <response code="500">An issue was encountered while unlinking the Shape Graphs.</response>
        [HttpDelete("{ontologyId}/defaultShapeGraphs/")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UnlinkDefaultShapeGraphsInOntology(string ontologyId)
        {
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");

            var shapeGraphs = await _dgraphService.GetOntologysDefaultShapeGraphs(ontologyId);
            foreach(var shapeId in shapeGraphs)
                if(!await _dgraphService.DeleteDefaultShapeGraphInOntology(ontologyId, shapeId))
                    return StatusCode(500, $"Something went wrong while removing the defaut Shape Graph {shapeId} in the Ontology and changes were not applied.");    
            return Ok($"{ontologyId}'s default Shape Graphs were removed");
        }

        /// <summary>
        /// Instanciates the Things defined by the provided Graph following the Ontology structure requirements.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="subgraph">The subgraph of the Things to be instanciated.</param>
        /// <response code="200">A success message.</response>
        /// <response code="204">The Graph provided was empty, therefore nothing was instanciated.</response>
        /// <response code="400">Either the identifier of the Ontology or the provided Graph are null or of bad format, or any Ontology dependecy was not met.</response>
        /// <response code="404">There is no Ontology with such identifier.</response>
        /// <response code="409">There are repited identifiers in the Graph.</response>
        /// <response code="500">An issue was encountered while validating the Graph or instanciating the Things.</response>
        [HttpPut("{ontologyId}/instanciate")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InstanciateSubGraphOfOntology(string ontologyId, [FromBody] JsonElement subgraph)
        {
            if(string.IsNullOrWhiteSpace(ontologyId))
                return BadRequest("The Ontology id provided is either null or empty");
            
            if(!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
                return NotFound($"There is no Ontology with id {ontologyId}");

            if(subgraph.AsNode() is null)
                return BadRequest("The provided Json Graph is not valid: obtained null");

            if(!(subgraph.TryGetProperty("@graph", out var graphElement) && graphElement.AsNode() is JsonArray graphArr))
                return BadRequest("The Json provided in the Body does not belong to a Json-Ld Graph.");

            if(graphArr.Count()==0)
                return NoContent();

            if(_instanciationService.AreThereConflictingIdsOnSubGraph(graphArr))
                return Conflict($"There are at least one conflicting id between some nodes of the subgraph provided.");

            Dictionary<string, (string, JsonObject)> infoDict = []; 

            try
            {
                await _instanciationService.InstanciateThingGraph(ontologyId, graphArr);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch(Exception ex)
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
        /// <response code="200">A success message.</response>
        /// <response code="204">The Graph provided was empty, therefore nothing was instanciated.</response>
        /// <response code="400">Either the identifier of the Ontology or the provided Graph are null or of bad format, or any Ontology dependecy was not met.</response>
        /// <response code="404">There is no Ontology with such identifier.</response>
        /// <response code="409">There are repited identifiers in the Graph or there is already a Twin with the identifier provided.</response>
        /// <response code="500">An issue was encountered while validating the Graph or instanciating the Things.</response>
        [HttpPut("{ontologyId}/instanciate/{twinId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
                await _instanciationService.InstanciateThingGraph(ontologyId, graphNode, twinId);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while instanciating the Things: {ex.Message}");
            }

            return Ok("Success");
        }

        /// <summary>
        /// Returns the Ontology in a JSON format.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology</param>
        /// <response code="200">The Ontology's JSON.</response>
        /// <response code="404">The Ontology was not found.</response>
        /// <response code="500">There was an issue while obtaining the namespace or JSON of the Ontology.</response>
        [HttpGet("{ontologyId}/export/Json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// Returns Ontology in a JSON-LD format.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <response code="200">The Ontology's JSON-LD.</response>
        /// <response code="404">The Ontology was not found.</response>
        /// <response code="500">Either the namespace, JSON in regular format or final JSON-LD obtained were null. </response>
        [HttpGet("{ontologyId}/export/JsonLd")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
        /// Returns the Ontology in a TTL Format File. 
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <response code="200">The TTL File of the Ontology.</response>
        /// <response code="404">The Ontology was not found.</response>
        /// <response code="500">Either the namespace or the JSON of the Ontology obtained were null, or there was any issue generating the TTL File.</response>
        [HttpGet("{ontologyId}/export/TTL")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/octet-stream")]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound, "application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError, "application/json")]
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
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> GetShapeGraph(string ontologyId)
        {
            return Ok(await _exportService.BuildShapeGraphFromOntology(ontologyId));
        }

        /// <summary>
        /// Runs a SparQL query on the Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="stringQuery">The SparQL query on a String format.</param>
        /// <response code="200">The results of the query.</response>
        /// <response code="204">The query was successfully run but no results were obtained.</response>
        /// <response code="400">Either the query was void or null, the query was not of SELECT or similar type, or its format was not valid</response>
        /// <response code="404">The Ontology was not found.</response>
        /// <response code="500">There was an issue while processing or running the query on the Ontology.</response>
        [HttpPost("{ontologyId}/query")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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