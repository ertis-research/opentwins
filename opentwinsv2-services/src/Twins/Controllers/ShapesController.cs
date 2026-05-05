

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using J2N.Text;
using Json.More;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Services;
using Twins.Services;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
using VDS.RDF.Shacl;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("shapes")]
    public class ShapeController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private readonly ExportService _exportService;
        private readonly ImportService _importService;
        public ShapeController(DGraphService dgraphService, ExportService exportService, ImportService importService)
        {
            _dgraphService = dgraphService;
            _exportService = exportService;
            _importService = importService;
        }

        /// <summary>
        /// Retrieves the Shape Graphs stored in the DataBase.
        /// </summary>
        /// <param name="page">The desired page of ShapeGraphs.</param>
        /// <param name="pageSize">The size of the pages of ShapeGraphs.</param>
        /// <param name="search">The optional string filter for the ShapeGraphs. If not specified, no filter will be applied.</param>
        /// <response code="200"> Ok with the list of Shape Graphs with its identifiers and shapes identifiers.</response>
        /// <response code="500"> Internal Server Error if there was any issue while retrieving the Shape Graphs.</response>
        [HttpGet("")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PagedResult<JsonElement>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllShapeGraphs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null
        )
        {
            try
            {
                return Ok(await _dgraphService.GetAllShapeGraphsAsync(page, pageSize, search));
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something wrong happened while looking for Shape Graphs in Dgraph:\n{ex.GetType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a Shape Graph from the provided TTL File.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph</param>
        /// <param name="shapeFile">The TTL File with the Shape Graph.</param>
        /// <response code="200"> Ok with the response obtained from the DataBase and how NQuads triples were added.</response>
        /// <response code="400"> Bad Request if the file is void or not of TTL extension.</response>
        /// <response code="409"> Conflict if there is already a Shape Graph with the same identifier.</response>
        /// <response code="500"> Internal Server Error if there was any issue while either processing the TTL File or uploading the NQuads into the DataBase.</response>
        [HttpPost("{shapeId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadShapeGraph(string shapeId, IFormFile shapeFile)
        {
            //Check if the file has been uploaded correctly
            if (shapeFile == null || shapeFile.Length == 0)
            {
                return BadRequest("Something wrong with the uploaded file");
            }

            //Check if the file is of .ttl type
            var extension = System.IO.Path.GetExtension(shapeFile.FileName);
            if (extension == null || extension.ToLower() != ".ttl")
            {
                return BadRequest("File can only be of .ttl extension, instead recieved a " + (extension is null ? "void" : extension.ToLower()) + " file");
            }

            bool check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
            if (!check)
            {
                //it doesn't exist

                //load TTL File into RDF Graph
                List<string>? nquads = null;
                try
                {
                    nquads = _importService.GetFullShapeGraphNquads(shapeId.ToLowerInvariant(), shapeFile) ?? throw new Exception("The obtained list of NQuads of the Shape Graph was null");
                    foreach(var n in nquads)
                        Console.WriteLine(n);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Something wrong happened while parsing the Shape Graph to NQuads DGraph:\n{ex.GetType}: {ex.Message}");
                }
                try
                {
                    //Load NQUADS List into DGraph
                    var response = await _dgraphService.AddNQuadTripleAsync(nquads);

                    return Ok($"{response} {nquads.ToArray().Length} triples added to DGraph successfully");

                }catch(Exception ex)
                {
                    return StatusCode(500, $"Something wrong happened while importing the Shape Graph to DGraph:\n{ex.GetType}: {ex.Message}");
                }
            }
            return Conflict($"There is already a Shape Graph with the id {shapeId}");
        }

        /// <summary>
        /// Retrieves the List of Shapes from the Shape Graph.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <response code="200"> Ok with the list of Shapes of the Shape Graph.</response>
        /// <response code="404"> Not Found if the Shape Graph was not found.</response>
        /// <response code="500"> Internal Server Error if there was any issue while retrieving the Shapes. </response>
        [HttpGet("{shapeId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(List<JsonElement>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetShapeGraphByID(string shapeId)
        {
            try
            {
                var check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
                if (check)
                {
                    return Ok(await _dgraphService.GetShapesFromShapeGraph(shapeId));
                }
                return NotFound($"{shapeId} Shape Graph does not exist");
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something wrong happened while obtaining the Shapes from the {shapeId} Shape Graph:\n{ex.GetType()}: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a Shape from the Shape Graph.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="nodeShapeId">The identifier of the Shape.</param>
        /// <response code="200"> Ok with the Shape of the Shape Graph.</response>
        /// <response code="404"> Not Found if either the Shape Graph was not found, there was no Shape with the provided identifier in the Shape Graph, or the Shape's information could not be retrieved.</response>
        /// <response code="500"> Internal Server Error if there was any issue while obtaining the Shape from the Shape Graph.</response>
        [HttpGet("{shapeId}/shapes/{nodeShapeId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNodeShapeFromShapeGraphById(string shapeId, string nodeShapeId)
        {
            var check = await _dgraphService.ExistsNodeShapeInShapeGraphAsync(shapeId, nodeShapeId);
            if (!check)
            {
                return NotFound($"{nodeShapeId} NodeShape does not belong to {shapeId} ShapeGraph or {shapeId} Shape Graph does not exist");
            }
            try
            {
                var response = await _dgraphService.GetNodeShapeFromShapeGraphByIdAsync(shapeId, nodeShapeId);
                if(response is null)
                {
                    return StatusCode(500, $"Something went wrong while getting the {nodeShapeId} NodeShape from DGraph");
                }
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something wrong happened while obtaining the Shapes from the {shapeId} Shape Graph:\n{ex.GetType()}: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a Shape Graph by its identifier.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <response code="204"> No Content if the Shape Graph was successfully deleted.</response>
        /// <response code="404"> Not Found if the Shape Graph was not found.</response>
        /// <response code="500"> Internal Server Error if there was any issue while deleting the Shape Graph.</response>
        [HttpDelete("{shapeId}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteShapeGraphByID(string shapeId)
        {
            var check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
            if (!check)
            {
                return NotFound($"{shapeId} Shape Graph does not exist");
            }
            try
            {
                var response = await _dgraphService.DeleteShapeGraphByIdAsync(shapeId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while deleting the {shapeId} Shape Graph from DGraph: {ex.GetType}: {ex}");
            }
        }

        /// <summary>
        /// Returns the ShapeGraph in a flattened JSON format.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <response code="200"> Ok with the flattened JSON of the Shape Graph.</response>
        /// <response code="404"> Not Found if the Shape Graph was not found.</response>
        /// <response code="500"> Internal Server Error if there was any issue while generating the JSON of the Shape Graph.</response>
        [HttpGet("{shapeId}/export/Json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportShapeGraphInFlattenedJsonFormat(string shapeId)
        {
            var check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
            if (!check)
            {
                return NotFound($"{shapeId} Shape Graph does not exist");
            }
            JsonObject json;
            try
            {
                json = await _exportService.GetShapeGraphFlattenedJson(shapeId) ?? throw new Exception($"The received flattened Json of {shapeId} Shape Graph is null");
            }catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the {shapeId} Shape Graph JSON from DGraph: {ex.GetType}: {ex}");
            }
            return Ok(json);
        }

        /// <summary>
        /// Returns the Shape Graph in a JSON-LD format.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <response code="200"> Ok with the JSON-LD of the Shape Graph.</response>
        /// <response code="404"> Not Found if the Shape Graph was not found.</response>
        /// <response code="500"> Internal Server Error if there was any issue while either obtaining the flattened JSON or the JSON-LD of the Shape Graph.</response>
        [HttpGet("{shapeId}/export/JsonLd")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(JsonObject), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportShapeGraphInJsonLdFormat(string shapeId)
        {
            var check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
            if (!check)
            {
                return NotFound($"{shapeId} Shape Graph does not exist");
            }
            JsonObject json;
            try
            {
                json = await _exportService.GetShapeGraphFlattenedJson(shapeId) ?? throw new Exception($"The flattened Json obtained of the {shapeId} Shape Graph is null");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the {shapeId} Shape Graph JSON from DGraph: {ex.GetType}: {ex}");
            }
            JsonObject jsonLd;
            try
            {
                jsonLd = ExportService.GetJsonLDFromRegularJson(json, shapeId, true) ?? throw new Exception("Obtained JsonLd is null");
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the JsonLd of the {shapeId} Shape Graph from its Json: {ex.GetType}: {ex}");
            }
            return Ok(jsonLd);
        }

        /// <summary>
        /// Returns the Shape Graph in a TTL format File.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <response code="200"> Ok with the TTL File of the Shape Graph.</response>
        /// <response code="404"> Not Found if the Shape Graph was not found.</response>
        /// <response code="500"> internal Server Error if there was any issue while either obtaining the flattened JSON or the TTL File of the Shape Graph.</response>
        [HttpGet("{shapeId}/export/TTL")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/octet-stream")]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound, "application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError, "application/json")]
        public async Task<IActionResult> ExportShapeGraphInTTLFormat(string shapeId)
        {
            var check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
            if (!check)
            {
                return NotFound($"{shapeId} Shape Graph does not exist");
            }
            JsonObject json;
            try
            {
                json = await _exportService.GetShapeGraphFlattenedJson(shapeId) ?? throw new Exception($"The flattened Json obtained of the {shapeId} Shape Graph is null");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the {shapeId} Shape Graph JSON from DGraph: {ex.GetType}: {ex}");
            }
            JsonObject jsonLd;
            try
            {
                jsonLd = ExportService.GetJsonLDFromRegularJson(json, shapeId, true) ?? throw new Exception($"The obtained JsonLd from the {shapeId} Shape Graph is null");
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the JsonLd of the {shapeId} Shape Graph from its Json: {ex.GetType}: {ex}");
            }
            try
            {
                return File(FormatService.GetTTLFileFromRegularJson(shapeId, jsonLd, ld:true), "text/turtle", $"{shapeId}_shapeGraph.ttl");
            }
            catch (Exception e)
            {
                return StatusCode(500, $"Something wrong while parsing to TTL Format:{e}");
            }
        }

        /// <summary>
        /// Validates a Twin, taking into account also its Ontologies, using the Shape Graph.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <response code="200"> Ok with the report of the results obtained after the ShaCL valdiation.</response>
        /// <response code="404"> Not Found if either the ShapeGraph or the Twin were not found.</response>
        /// <response code="500"> Internal Server Error if there was any issue while obtaining the flattened JSON or generating the necessary Graphs.</response>
        [HttpGet("{shapeId}/validate/{twinId}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateTwinWithShapeGraph(string shapeId, string twinId)
        {
            var check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
            if (!check)
            {
                return NotFound($"{shapeId} Shape Graph does not exist");
            }
            check = await _dgraphService.ExistsThingByIdAsync(twinId);
            if (!check)
            {
                return NotFound($"{twinId} Twin does not exist");
            }

            JsonObject json;
            try
            {
                json = await _exportService.GetShapeGraphFlattenedJson(shapeId) ?? throw new Exception($"The recieved flattened Json of the {shapeId} shape Graph is null");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the {shapeId} Shape Graph JSON from DGraph: {ex.GetType}: {ex}");
            }
            JsonObject jsonLd;
            try
            {
                jsonLd = ExportService.GetJsonLDFromRegularJson(json, shapeId, true) ?? throw new Exception($"The obtained JsonLd from the {shapeId} Shape Graph is null");
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while getting the JsonLd of the {shapeId} Shape Graph from its Json: {ex.GetType}: {ex}");
            }
            //Load Shape Graph in VDS.RDF.ShapeGraph
            ShapesGraph shapeGraph; 
            try
            {
                IGraph shapeRDFgraph = FormatService.GetRDFGraphFromJson(jsonLd, shapeId, ld:true) ?? throw new Exception("The graph obtained is null");
                shapeGraph = new ShapesGraph(shapeRDFgraph) ?? throw new Exception("The Shape Graph obtained is null");
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while loading the RDF Graph of the Shape Graph {ex.GetType}: {ex}");
            }

            //Get the twin + the ontology Graph
            IGraph compound = new VDS.RDF.Graph();
            IGraph g1;
            try
            {
                var twinJson = await _exportService.GetJsonWithoutNamespace(twinId) ?? throw new Exception("The recieved Json of the Twin is null");
                g1 = FormatService.GetRDFGraphFromJson(twinJson, twinId) ?? throw new Exception("The recieved Graph of the Twin is null");
                
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something went wrong while loading the RDF Graph of the twin: {ex.GetType}: {ex}");
            }

            //Add also the ontology to the graphs
            List<string> ontologies = await _dgraphService.GetOntologiesOfTwinAsync(twinId);
            foreach(string ontologyId in ontologies)
            {
                try
                {
                    var ontologyJson = await _exportService.GetJsonWithNamespace(ontologyId, await _dgraphService.GetNamespacesInOntologyAsync(ontologyId) ?? null) ?? throw new Exception($"The recieved Json of the {ontologyId} Ontology is null");
                    var ontologyGraph = FormatService.GetRDFGraphFromJson(ontologyJson, ontologyId) ?? throw new Exception($"The recieved Graph of the {ontologyId} Ontology is null");

                    compound.Merge(ontologyGraph, true);
                }catch(Exception ex)
                {
                    return StatusCode(500, $"Something went wrong while loading the RDF Graph of the {ontologyId} Ontology. {ex.GetType}: {ex}");
                }
            }

            //in compound graph we have the twin and the ontologies it uses

            //Validation

            var results = shapeGraph.Validate(compound);

            if (results.Conforms)
            {
                return Ok("Validation successful — graph conforms to the shape.");
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("Validation FAILED:\n");

            foreach (var res in results.Results)
            {
                // Focus node (what failed)
                string focusNode = res.FocusNode?.ToSafeString() ?? "(unknown node)";

                // Property path (what property failed)
                string path = res.ResultPath?.ToSafeString() ?? "(no path)";

                // Constraint type (minCount, datatype, etc.)
                string constraint = res.SourceConstraintComponent?.ToSafeString()
                                    ?? "(unknown constraint)";

                // Shape
                string shape = res.SourceShape?.ToSafeString() ?? "(unknown shape)";

                // Severity
                string severity = res.Severity?.ToSafeString() ?? "Violation";

                report.AppendLine($"• Node: {focusNode}");
                report.AppendLine($"  Shape: {shape}");
                report.AppendLine($"  Property: {path}");
                report.AppendLine($"  Constraint: {constraint}");
                report.AppendLine($"  Severity: {severity}");
                report.AppendLine($"  Message: {res.Message}");
                report.AppendLine();
            }

            return Ok(report.ToString());

        }
    }
}

