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
                //.Replace("'", "\\\'")    // omit '
                .Replace("\n", "\\n")    // omit \n
                .Replace("\r", "\\r")    // omit \r
                .Replace("\t", "\\t");   // omit \t
        }

        [HttpPost("{ontologieUpload}")]
        public async Task<IActionResult> UploadAntology(IFormFile antologyFile)
        {
            //Check if the file has been uploaded correctly
            if (antologyFile == null || antologyFile.Length == 0) {
                return BadRequest("Something wrong with the uploaded file");
            }

            //Check if the file is of .ttl type
            var extension = Path.GetExtension(antologyFile.FileName);
            if (extension == null || extension.ToLower() != ".ttl")
            {
                return BadRequest("File can only be of .ttl extension, instead recieved a "+extension.ToLower()+" file");
            }

            //Parse to rdf
            //first we read the file data and save it in a IGraph variable
            IGraph graph = new Graph();
            var parser = new TurtleParser();

            //the RDF parser will load into the graph the data from the file using the stream reader
            using (var stream = antologyFile.OpenReadStream())
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
                .Where(s => s.NodeType == NodeType.Uri || s.NodeType == NodeType.Blank)
                .Where(s =>
                {
                    // Obtain the node type
                    var types = graph.GetTriplesWithSubjectPredicate(s, graph.CreateUriNode("rdf:type"))
                                     .Select(tr => tr.Object.ToString());
                    // Exclude the ones with any excluded type
                    return !types.Any(t => ignoredTypes.Contains(t));
                })
                .Distinct();
                string createdAt = DateTime.UtcNow.ToString("O");

                foreach (var node in allNodes)
                {

                    if (ignoredPrefixes.Any(p => node.ToString().Contains(p)))
                    {
                        continue;
                    }

                    string uid = $"_:{GetLocalName(node, graph)}";
                    string thingId = GetLocalName(node, graph);
                    

                    nquads.Add($"{uid} <dgraph.type> \"Thing\" .");
                    nquads.Add($"{uid} <dgraph.type> \"Ontology\" .");
                    nquads.Add($"{uid} <thingId> \"{thingId}\" .");
                    nquads.Add($"{uid} <name> \"{thingId}\" .");
                    nquads.Add($"{uid} <createdAt> \"{createdAt}\" .");
                }

                var ignoredPredicates = new List<string> { "domain", "range", "inverseOf", "type", "uid" };

                foreach (Triple triple in graph.Triples.Distinct())
                {
                    //Get the subject, predicate and object of the triple
                    string subject = GetUid(triple.Subject, graph); //_:uid
                    string predicate = GetLocalName(triple.Predicate, graph); //uri

                    //as this includes the original type and uid of the antology, we exclude them so as not to duplicate the existing ones
                    if (!ignoredPredicates.Contains(predicate))
                    {
                        //TODO Create a Relation object which representates each relation of every node

                        string obj;

                        //Rest of attributes: TODO: WOT
                        //Check if the object is a literal or the uid to another node
                        if (triple.Object.NodeType == NodeType.Literal)
                        {
                            //Attribute of a node

                            /*
                             type Attribute {
                                Attribute.key =============> Generated key (it will overrided when uploaded to DGraph)
                                Attribute.type ============> Type of the attribute value (DGraph format)
                                Attribute.value ===========> Value of the attribute casted to string
                            }
                             */

                            var literal = (ILiteralNode) triple.Object;

                            string value = SanitizeAttributeValue(literal.Value);
                            //There's the possibility of the value containing " characters

                            //string type by default
                            string dataType = literal.DataType?.ToString() ?? "xsd:string";

                            string attribute_uid = $"{predicate}";
                            nquads.Add($"{attribute_uid} <dgraph.type> \"Attribute\" .");
                            nquads.Add($"{attribute_uid} <Attribute.key> \"{attribute_uid}\" .");
                            nquads.Add($"{attribute_uid} <Attribute.type> \"{dataType}\" .");
                            nquads.Add($"{attribute_uid} <Attribute.value> \"{value}\" .");

                            //Link the node to the attribute
                            nquads.Add($"{subject} <hasAttribute> {attribute_uid} .");

                        }
                        else
                        {
                            //Relation between 2 nodes

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

                            obj = GetUid(triple.Object, graph);
                            nquads.Add($"{relation_uid} <relatedTo> {obj} .");
                            //nquads.Add($"{subject} <{predicate}> {obj} ."); //provisional
                        }

                        //it should be here, but because the literals are not added as of now, it'll be part of the else statement
                        //nquads.Add($"{subject} <{predicate}> {obj} .");
                    }

                }

                //---------------------------------------------------------------------------------
                //Upload the triples as a mutation to DGraph

                //Console.WriteLine("Mandar nquads");
                System.IO.File.WriteAllText("nquads.txt", "");
                foreach(String str in nquads)
                {
                    System.IO.File.AppendAllText("nquads.txt", str+"\n");
                }

                var response = await _dgraphService.AddNQuadTripleAsync(nquads);

                Console.WriteLine("Procesados nquads");
                return Ok($"{response} {nquads.ToArray().Length} triples added to DGraph successfully");
            }
            catch(Exception ex)
            {
                return StatusCode(500, $"Something wrong happened while importing the Antology to DGraph:\n{ex.GetType}: {ex.Message}");
            }
              

        }
    }
}