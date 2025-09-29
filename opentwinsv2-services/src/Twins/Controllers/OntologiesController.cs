using VDS.RDF;
using VDS.RDF.Parsing;
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


namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("ontologies")]
    public class OntologiesController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private const string ActorType = Actors.ThingActor;

        public OntologiesController(DGraphService dgraphService)
        {
            _dgraphService = dgraphService;
        }

        private string GetLocalName(INode node, IGraph graph)
        {
            if(node is UriNode uriNode)
            {
                string qname;
                if (graph.NamespaceMap.ReduceToQName(uriNode.Uri.ToString(), out qname))
                {
                    //get the name right after the prefix
                    return qname.Split(':')[1];
                }
                else
                {
                    //Get the last part of the URI
                    var uri = uriNode.Uri.ToString();

                    //Check which character is last: # or /
                    var indx1 = uri.LastIndexOf('#');
                    var indx2 = uri.LastIndexOf('/');
                    var indx = Math.Max(indx1, indx2);

                    //return the substring or the whole uri in case none of the characters are present
                    return indx >= 0 ? uri.Substring(indx + 1) : uri;
                }
            }
            return node.ToString();
        }

        private string GetUid(INode node, IGraph graph)
        {
            //get the uid omiting all prefixes
            string local = GetLocalName(node, graph);
            return $"_:{local}";
        }

        private string SanitizeAttributeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("\\", "\\\\")   // omit \
                .Replace("\"", "\\\"")   // omit "
                .Replace("\n", "\\n")    // omit \n
                .Replace("\r", "\\r")    // omit \r
                .Replace("\t", "\\t");   // omit \t
        }

        private List<string> GetNQuadAttributeTriples(string subject, string predicate, ILiteralNode literal)
        {

            /*
            type Attribute {
                Attribute.key =============> Generated key (it will overrided when uploaded to DGraph)
                Attribute.type ============> Type of the attribute value (DGraph format)
                Attribute.value ===========> Value of the attribute casted to string
            }
            */

            var nquads = new List<string>();

            string value = SanitizeAttributeValue(literal.Value);
            //There's the possibility of the value containing " characters

            //string type by default
            string dataType = literal.DataType?.ToString() ?? "xsd:string";

            string attribute_uid = $"_:att_{predicate}_{Guid.NewGuid()}";
            nquads.Add($"{attribute_uid} <dgraph.type> \"Attribute\" .");
            nquads.Add($"{attribute_uid} <Attribute.key> \"{predicate}\" .");
            nquads.Add($"{attribute_uid} <Attribute.type> \"{dataType}\" .");
            nquads.Add($"{attribute_uid} <Attribute.value> \"{value}\" .");

            //Link the node to the attribute
            nquads.Add($"{subject} <hasAttribute> {attribute_uid} .");

            return nquads;
        }

        private List<string> GetNQuadRelationTriples(string subject, string predicate, string obj, string createdAt)
        {
            var nquads = new List<string>();

            //Relation node
            string relation_uid = $"_:rel_{predicate}_{Guid.NewGuid()}";
            nquads.Add($"{relation_uid} <dgraph.type> \"Relation\" .");
            nquads.Add($"{relation_uid} <Relation.name> \"{predicate}\" .");
            nquads.Add($"{relation_uid} <Relation.createdAt> \"{createdAt}\" .");
            nquads.Add($"{relation_uid} <relatedTo> {subject} .");

            /*
             type Relation {
                Relation.name =============> Name of the original predicate
                Relation.createdAt ========> timestamp
                Relation.attributes =======> any additional info of the relation (thre's no example in the sample antology provided)
                relatedTo =================> each Realtion object has 2 relatedTo attribute (subject and object)
                hasPart ===================> ignore as of now
                hasChild ==================> ignore as of now
            }
             */

            nquads.Add($"{relation_uid} <relatedTo> {obj} .");


            return nquads;
        }

        private List<string> GetNQuadNodeTriples(string uid, string thingId, string createdAt, string ontology)
        {
            var nquads = new List<string>();
            var ontology_uid = $"_:{ontology}";

            nquads.Add($"{uid} <dgraph.type> \"Thing\" .");
            nquads.Add($"{uid} <thingId> \"{thingId}\" .");
            nquads.Add($"{uid} <name> \"{thingId}\" .");
            nquads.Add($"{uid} <createdAt> \"{createdAt}\" .");

            //Associate the Thing node with the Ontology node
            nquads.Add($"{ontology_uid} <hasThing> {uid} .");

            return nquads;
        }

        private List<string> GetNQuadsOntologyTriples(string ontologyId, string createdAt)
        {
            var nquads = new List<string> ();

            var ontology_uid = $"_:{ontologyId}";
            nquads.Add($"{ontology_uid} <dgraph.type> \"Ontology\" .");
            nquads.Add($"{ontology_uid} <createdAt> \"{createdAt}\" .");
            nquads.Add($"{ontology_uid} <ontologyId> \"{ontologyId}\" .");
            nquads.Add($"{ontology_uid} <Ontology.name> \"{ontologyId}\" .");

            return nquads;
        }

        private void GetNquadsAsTxtFile(List<string> nquads)
        {
            //DEBUGGING
            System.IO.File.WriteAllText("nquads.txt", "");
            foreach (String str in nquads)
            {
                System.IO.File.AppendAllText("nquads.txt", str + "\n");
            }
        }

        [HttpPost("{ontologyId}")]
        public async Task<IActionResult> UploadAntology(IFormFile ontologyFile, string ontologyId)
        {
            //Check if the file has been uploaded correctly
            if (ontologyFile == null || ontologyFile.Length == 0) {
                return BadRequest("Something wrong with the uploaded file");
            }

            //Check if the file is of .ttl type
            var extension = Path.GetExtension(ontologyFile.FileName);
            if (extension == null || extension.ToLower() != ".ttl")
            {
                return BadRequest("File can only be of .ttl extension, instead recieved a "+extension.ToLower()+" file");
            }

            if(!await _dgraphService.ExistsThingByIdAsync(ontologyId))
            {
                //Parse to rdf
                //first we read the file data and save it in a IGraph variable
                IGraph graph = new VDS.RDF.Graph();
                var parser = new TurtleParser();

                //the RDF parser will load into the graph the data from the file using the stream reader
                using (var stream = ontologyFile.OpenReadStream())
                using (var reader = new StreamReader(stream))
                {
                    parser.Load(graph, reader);
                }

                //Parse to JSON
                //The parsed node will be stored in a list of dictionaries

                var res = new List<Dictionary<string, object>>();

                //Iterate over each Subject
                //Triple: Subject - Predicate -> Object

                //List of triples in NQuads format
                var nquads = new List<string>();

                //to prevent predicates to be created as nodes, we'll ignore as of now the owl types
                try
                {
                    var owlTypes = graph.Triples
                .Where(t => t.Predicate.ToString().EndsWith("type") && t.Object.ToString().Contains("owl"))
                .Select(t => t.Object.ToString())
                .Distinct();

                    var ignoredPrefixes = new[] { "swrl:", "swrla:" }; //PROVISIONAL: Ignore the Blank Nodes
                                                                       //Ignore the property types so it will not create a node for a Relation
                    var ignoredTypes = owlTypes.ToList();

                    var allNodes = graph.Triples
                    .Select(t => t.Subject)
                    .Where(s => s.NodeType == NodeType.Uri)
                    /*.Where(s =>
                    {
                        // Obtain the node type
                        var types = graph.GetTriplesWithSubjectPredicate(s, graph.CreateUriNode("rdf:type"))
                                         .Select(tr => tr.Object.ToString());
                        // Exclude the ones with any excluded type
                        return !types.Any(t => ignoredTypes.Contains(t));
                    })*/
                    .Distinct();
                    string createdAt = DateTime.UtcNow.ToString("O");

                    //Create the Ontology node
                    nquads.AddRange(GetNQuadsOntologyTriples(ontologyId, createdAt));

                    foreach (var node in allNodes)
                    {

                        if (ignoredPrefixes.Any(p => node.ToString().Contains(p)))
                        {
                            continue;
                        }

                        string uid = GetUid(node, graph);
                        string thingId = GetLocalName(node, graph);
    
                        nquads.AddRange(GetNQuadNodeTriples(uid, thingId, createdAt, ontologyId));
                        
                    }

                    var ignoredPredicates = new List<string> { "domain", "range", "inverseOf", "type", "uid" };

                    foreach (Triple triple in graph.Triples.Distinct())
                    {
                        //Get the subject, predicate and object of the triple
                        string subject = GetUid(triple.Subject, graph); //_:uid
                        string predicate = GetLocalName(triple.Predicate, graph); //uri

                        //as this includes the original type and uid of the ontology, we exclude them so as not to duplicate the existing ones
                        if (!ignoredPredicates.Contains(predicate))
                        {
                            //Check if the object is a literal (Attribute) or the uid to another node (Relation)
                            if (triple.Object.NodeType == NodeType.Literal)
                            {
                                //Attribute of a node
                                var literal = (ILiteralNode)triple.Object;
                                nquads.AddRange(GetNQuadAttributeTriples(subject, predicate, literal));
                            }
                            else
                            {
                                //Relation between 2 nodes
                                string obj = GetUid(triple.Object, graph);
                                nquads.AddRange(GetNQuadRelationTriples(subject, predicate, obj, createdAt));
                            }
                        }

                    }

                    //---------------------------------------------------------------------------------
                    //Upload the triples as a mutation to DGraph

                    var response = await _dgraphService.AddNQuadTripleAsync(nquads);

                    return Ok($"{response} {nquads.ToArray().Length} triples added to DGraph successfully");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Something wrong happened while importing the Antology to DGraph:\n{ex.GetType}: {ex.Message}");
                }
            }
            return Conflict("There is already an ontology with this id");
        }

        [HttpGet("{ontologyId}")]
        public async Task<IActionResult> GetThingsByOntologyId(string ontologyId)
        {
            var response = await _dgraphService.GetThingsInOntologyAsync(ontologyId);
            return Ok(response);
        }

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
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            
        }

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
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}