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


namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("ontologies")]
    public class OntologiesController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;
        private const string ActorType = Actors.ThingActor;

        public OntologiesController(DGraphService dgraphService, ThingsService thingsService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
        }

        private string SanitizeTypeAndUIDValues(string uri)
        {
            //Check which character is last
            char[] separators = { '#', '/', '&' };

            // Remove trailing separator if present at the end
            while (uri.Length > 0 && separators.Contains(uri.Last()))
            {
                //Delete illegal characters at the end
                uri = uri.Substring(0, uri.Length - 1);
            }

            //in case the whole uri were illegal characters (unlikely but possible)
            if (uri.Length == 0)
            {
                return null;
            }

            // Find last separator after removing trailing char
            int indx = uri.LastIndexOfAny(separators);

            //return the substring or the whole uri in case none of the characters are present
            return (indx >= 0 && indx < uri.Length - 1) ? uri.Substring(indx + 1) : uri;
        }

        private (string Prefix, string LocalName) GetLocalName(VDS.RDF.INode node, IGraph graph, string ontologyId)
        {
            if (node is UriNode uriNode)
            {
                string qname;
                string prefix;
                string localName;
                if (graph.NamespaceMap.ReduceToQName(uriNode.Uri.ToString(), out qname))
                {
                    //get the name right after the prefix
                    var parts = qname.Split(':');
                    prefix = parts[0];
                    localName = parts[1];
                }
                else
                {
                    //Get the last part of the URI
                    var uri = uriNode.Uri.ToString();
                    localName = SanitizeTypeAndUIDValues(uri);
                    prefix = $"pref{ontologyId}";
                }
                return (prefix, localName);
            }
            return ($"pref{ontologyId}", node.ToString());
        }

        private string GetUid(VDS.RDF.INode node, IGraph graph)
        {
            //get the uid omiting all prefixes
            var (_, local) = GetLocalName(node, graph, "");
            local ??= "nameless" + Guid.NewGuid();
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



        private List<string> GetNQuadAttributeTriples(string subject, string predicate, ILiteralNode literal, string ontology, string prefix)
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
            string dataType = literal.DataType?.ToString() ?? "string";
            var type = SanitizeTypeAndUIDValues(dataType) ?? "string";

            var namespace_uid = $"_:{ontology}namespace_{prefix}";

            string attribute_uid = $"_:att_{predicate}_{Guid.NewGuid()}";
            nquads.Add($"{attribute_uid} <dgraph.type> \"Attribute\" .");
            nquads.Add($"{attribute_uid} <Attribute.key> \"{predicate}\" .");
            nquads.Add($"{attribute_uid} <Attribute.type> \"{type}\" .");
            nquads.Add($"{attribute_uid} <Attribute.prefix> {namespace_uid} .");
            nquads.Add($"{attribute_uid} <Attribute.value> \"{value}\" .");

            //Link the node to the attribute
            nquads.Add($"{subject} <hasAttribute> {attribute_uid} .");

            return nquads;
        }

        private List<string> GetNQuadRelationTriples(string subject, string predicate, string obj, bool bidirectional, string createdAt, string ontology, string prefix)
        {
            /*
            type Relation {
                Relation.name =============> Name of the original predicate
                Relation.createdAt ========> timestamp
                Relation.attributes =======> any additional info of the relation (thre's no example in the sample ontology provided)
                hasPart ===================> ignore as of now
                hasChild ==================> ignore as of now
                relatedTo =================> The object of a relation. If bidirectional, also the subject
                relatedFrom ===============> The subject of a relation only if it's unidirectional. If not, not defined
            }
            */

            var nquads = new List<string>();
            var namespace_uid = $"_:{ontology}namespace_{prefix}";
            //Relation node
            string relation_uid = $"_:rel_{predicate}_{Guid.NewGuid()}";
            nquads.Add($"{relation_uid} <dgraph.type> \"Relation\" .");
            nquads.Add($"{relation_uid} <Relation.name> \"{predicate}\" .");
            nquads.Add($"{relation_uid} <Relation.createdAt> \"{createdAt}\" .");
            nquads.Add($"{relation_uid} <Relation.prefix> {namespace_uid} .");
            nquads.Add($"{relation_uid} <relatedTo> {obj} ."); //always
            nquads.Add($"{relation_uid} <{(bidirectional ? "relatedTo" : "relatedFrom")}> {subject} .");

            return nquads;
        }

        private List<string> GetNQuadNodeTriples(string uid, string thingId, string createdAt, string ontology, string prefix)
        {
            var nquads = new List<string>();
            var ontology_uid = $"_:{ontology}";
            var namespace_uid = $"_:{ontology}namespace_{prefix}";

            nquads.Add($"{uid} <dgraph.type> \"Thing\" .");
            nquads.Add($"{uid} <thingId> \"{ontology}:{thingId}\" .");
            nquads.Add($"{uid} <name> \"{thingId}\" .");
            nquads.Add($"{uid} <createdAt> \"{createdAt}\" .");
            nquads.Add($"{uid} <Thing.prefix> {namespace_uid} .");

            //Associate the Thing node with the Ontology node
            nquads.Add($"{ontology_uid} <hasThing> {uid} .");

            return nquads;
        }

        private List<string> GetNQuadsOntologyTriples(string ontologyId, string createdAt)
        {
            var nquads = new List<string>();

            var ontology_uid = $"_:{ontologyId}";
            nquads.Add($"{ontology_uid} <dgraph.type> \"Ontology\" .");
            nquads.Add($"{ontology_uid} <createdAt> \"{createdAt}\" .");
            nquads.Add($"{ontology_uid} <ontologyId> \"{ontologyId}\" .");
            nquads.Add($"{ontology_uid} <Ontology.name> \"{ontologyId}\" .");
            nquads.Add($"{ontology_uid} <Ontology.name> \"{ontologyId}\" .");
            return nquads;
        }

        private List<string> GetNQuadsNamespaceTriples(string ontologyId, string createdAt, string prefix, string uri)
        {
            var nquads = new List<string>();

            /*
            namespaceId ===========> ontologyId:namespace
            prefix ================> prefix of the type "rdf:"
            uri ===================> uri that replaces the prefix (http://example.org/)
            Namespace.name ========> ontologyId:namespace
            */

            var namespace_uid = $"_:{ontologyId}namespace_{prefix}";
            nquads.Add($"{namespace_uid} <dgraph.type> \"Namespace\" .");
            nquads.Add($"{namespace_uid} <Namespace.createdAt> \"{createdAt}\" .");
            nquads.Add($"{namespace_uid} <namespaceId> \"{ontologyId}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <Namespace.name> \"{ontologyId}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <prefix> \"{prefix}\" .");
            nquads.Add($"{namespace_uid} <uri> \"{uri}\" .");

            //link namespace to the ontology
            nquads.Add($"_:{ontologyId} <namespace> {namespace_uid} .");

            return nquads;
        }

        private (bool, bool) isRelationBidirectional(IGraph graph, Triple triple, string uidSubj, string uidObj, string predicate, List<string> nquads)
        {
            bool bid = false;
            bool exists = false;

            //check if in Object there's: uidObj <predicate> uidSubj
            /*look specifically for:
            relUid <relatedTo> objUid 
            and then:
            relUid <relatedTo> subjUid
            to make sure, supposedly if the first nquad is present it means 
            */

            var reversedTriple = new Triple(triple.Object, triple.Predicate, triple.Subject);
            bid = graph.ContainsTriple(triple) && graph.ContainsTriple(reversedTriple);

            if (bid)
            {
                var pattern = @"^(?<subject>\S+)\s+<(?<predicate>[^>]+)>\s+(?:(?<objectUri>\S+)|""(?<objectLit>[^""]+)"")";
                var relUid = $"_:rel_{predicate}_";

                var parsed = nquads
                    .Select(line => new { line, match = Regex.Match(line, pattern) })
                    .Where(x => x.match.Success)
                    .ToList();

                var relationsNquads = parsed
                    .Where(x =>
                        x.match.Groups["subject"].Value.Contains(relUid) &&
                        x.match.Groups["predicate"].Value.Equals("relatedTo") &&
                        x.match.Groups["objectUri"].Value.Equals(uidSubj) &&
                        parsed.Any(y =>
                            y.match.Groups["subject"].Value == x.match.Groups["subject"].Value &&
                            y.match.Groups["predicate"].Value.Equals("relatedTo") &&
                            y.match.Groups["objectUri"].Value.Equals(uidObj)))
                    .Select(x => x.line)
                    .ToList();

                exists = relationsNquads.Count > 0;
            }

            Console.WriteLine($"{(!bid ? "NO" : "")} es bidireccional");
            Console.WriteLine($"{(!exists ? "NO" : "YA")} existia");
            return (bid, exists);
        }

        private void GetNquadsAsTxtFile(List<string> nquads)
        {
            //DEBUGGING
            System.IO.File.WriteAllText("nquads.txt", "");
            foreach (string str in nquads)
            {
                System.IO.File.AppendAllText("nquads.txt", str + "\n");
            }
        }

        private bool IsRelevantBlankNode(IGraph g, VDS.RDF.INode node)
        {
            var interestingPredicates = new[] {
                "owl:oneOf", "owl:intersectionOf", "owl:unionOf",
                "owl:someValuesFrom", "owl:allValuesFrom",
                "rdf:first", "rdf:rest"
            };

            return g.GetTriplesWithSubject(node)
                .Any(t => t.Predicate is IUriNode p &&
                        interestingPredicates.Any(ip => p.ToString().Contains(ip)));
        }

        [HttpGet("")]
        public async Task<IActionResult> GetAllOntologiesId()
        {
            try
            {
                return Ok(await _dgraphService.GetAllOntologiesIdsAsync());
            }catch(Exception ex)
            {
                return StatusCode(500, $"Something wrong happened while looking for ontologies in Dgraph:\n{ex.GetType}: {ex.Message}");
            }
        }

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
                return BadRequest("File can only be of .ttl extension, instead recieved a " + extension.ToLower() + " file");
            }

            if (!await _dgraphService.ExistsOntologyByIdAsync(ontologyId))
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

                    //save the namespace mapping [(prefix,uri)] of the ontology, ignoring the previously defined prefixes in ignoredPrefixes
                    var prefixList = graph.NamespaceMap.Prefixes
                        .Where(p => !ignoredPrefixes.Contains(p))
                        .Select(p => new
                        {
                            Prefix = p,
                            NamespaceUri = graph.NamespaceMap.GetNamespaceUri(p).ToString()
                        })
                        .ToList();


                    var allNodes = graph.Triples
                        .Select(t => t.Subject)
                        .Where(s => s.NodeType == VDS.RDF.NodeType.Uri || (s.NodeType == VDS.RDF.NodeType.Blank && IsRelevantBlankNode(graph, s)))
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

                    //Create the Namespace nodes associated to the ontology
                    foreach (var ns in prefixList)
                    {
                        nquads.AddRange(GetNQuadsNamespaceTriples(ontologyId, createdAt, ns.Prefix, ns.NamespaceUri));
                    }
                    //In case there is a node with no prefix, I set a fallback with a generic uri unique for the ontology
                    nquads.AddRange(GetNQuadsNamespaceTriples(ontologyId, createdAt, $"pref{ontologyId}", $"http://example.org/ontology/{ontologyId}"));

                    foreach (var node in allNodes)
                    {

                        if (ignoredPrefixes.Any(p => node.ToString().Contains(p)))
                        {
                            continue;
                        }

                        string uid = GetUid(node, graph);
                        var (prefix, thingId) = GetLocalName(node, graph, ontologyId); //here it takes care of the no prefix fallback
                        thingId ??= "thing" + Guid.NewGuid();
                        nquads.AddRange(GetNQuadNodeTriples(uid, thingId, createdAt, ontologyId, prefix));

                    }

                    var ignoredPredicates = new List<string> { "domain", "range", "inverseOf", "uid" };

                    foreach (Triple triple in graph.Triples.Distinct())
                    {
                        //Get the subject, predicate and object of the triple
                        string subject = GetUid(triple.Subject, graph); //_:uid
                        var (prefixPredicate, predicate) = GetLocalName(triple.Predicate, graph, "");  //uri
                        predicate ??= "predicate" + Guid.NewGuid();
                        //as this includes the original type and uid of the ontology, we exclude them so as not to duplicate the existing ones
                        if (!ignoredPredicates.Contains(predicate))
                        {
                            //Check if the object is a literal (Attribute) or the uid to another node (Relation)
                            if (predicate.Equals("type") || predicate.Equals("a"))
                            {
                                //Support for "type" and "a" predicate
                                //create or find a thing whose thingId is the name of the type, and with this subject, relate it to the type Thing through hasType relation
                                var (typePrefix, typeOfNode) = GetLocalName(triple.Object, graph, ontologyId);
                                Console.WriteLine("PREFIX: " + typePrefix + ", TYPE: " + typeOfNode);
                                // var typeOfNode = ((ILiteralNode)triple.Object).ToString();
                                string match = nquads.FirstOrDefault(nquad => nquad.Contains($"<thingId> {typeOfNode}")); //null manegement ahead
                                string typeUid = "";
                                //find out if it is already a Thing
                                if (match != null)
                                {
                                    //the Thing already exists
                                    //first we get the uid of the Type Thing
                                    typeUid = match.Split('<')[0];
                                    Console.WriteLine("UID de Type: " + typeUid);
                                }
                                else
                                {
                                    //the Thing doesn't exist, we have to create it
                                    //typeOfNode is the thingId, but we need the prefix too
                                    typeUid = $"_:typeThing{typeOfNode}";
                                    nquads.AddRange(GetNQuadNodeTriples($"_:typeThing{typeOfNode}", typeOfNode, createdAt, ontologyId, typePrefix));
                                }
                                if (typeUid.Equals(""))
                                {
                                    //typeUid has nit been instanciated correctly, something has gone wrong
                                    return StatusCode(500, $"Something wrong happened while importing the Ontology to DGraph:\nType Uid of ${subject} node not instanciated");
                                }

                                //instanciate hasType relation
                                nquads.Add($"{subject} <hasType> {typeUid} .");
                            }
                            else if (triple.Object.NodeType == VDS.RDF.NodeType.Literal)
                            {
                                //Attribute of a node
                                var literal = (ILiteralNode)triple.Object;
                                nquads.AddRange(GetNQuadAttributeTriples(subject, predicate, literal, ontologyId, prefixPredicate));
                            }
                            else
                            {
                                //Relation between 2 nodes
                                string obj = GetUid(triple.Object, graph);
                                Console.WriteLine($"SUBJECT: {subject} OBJECT: {obj}");
                                //check if the relation is bidirectional or not
                                //if yes, check if the relation object has already been added to the nquads
                                var (bid, existent) = isRelationBidirectional(graph, triple, subject, obj, predicate, nquads);
                                if (!existent)
                                    nquads.AddRange(GetNQuadRelationTriples(subject, predicate, obj, bid, createdAt, ontologyId, prefixPredicate));
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
                    return StatusCode(500, $"Something wrong happened while importing the Ontology to DGraph:\n{ex.GetType}: {ex.Message}");
                }
            }
            return Conflict("There is already an ontology with this id");
        }

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
                if (result == null)
                    throw new KeyNotFoundException("Relation could not be found");
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
                if (result == null)
                    throw new KeyNotFoundException("Attribute could not be found");
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }



        private async Task<JsonObject?> GetWOTThingProperties(string ontologyId, string thingId)
        {
            var json = await _dgraphService.GetThingAttributesByIdAsync(ontologyId, thingId);
            var res = new JsonObject();
            if (json != null && json.Value.ValueKind.Equals(JsonValueKind.Array))
            {
                foreach (var att in json.Value.EnumerateArray().ToArray())
                {
                    var key = att.GetProperty("Attribute.key").ToString()!;
                    var type = att.GetProperty("Attribute.type").ToString()!;
                    //var value = att.GetProperty("Attribute.value").ValueKind == JsonValueKind.Number ? att.GetProperty("Attribute.value") : att.GetProperty("Attribute.value").ToString();

                    var valueElement = att.GetProperty("Attribute.value").ToString();
                    object? value;

                    switch (type.ToLower())
                    {
                        case "int":
                        case "integer":
                            value = int.TryParse(valueElement, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? int.Parse(valueElement) : valueElement;
                            break;
                        case "float":
                        case "double":
                            value = double.TryParse(valueElement, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? float.Parse(valueElement) : valueElement;
                            break;
                        case "bool":
                            value = bool.TryParse(valueElement, out _) ? bool.Parse(valueElement) : valueElement;
                            break;
                        default:
                            value = valueElement;
                            break;
                    }

                    var obj = new JsonObject
                    {
                        ["type"] = type
                    };

                    if (value is not null)
                    {
                        obj["default"] = JsonValue.Create(value);
                    }

                    res.Add($"{key}", obj);
                }
            }
            return res.Count > 0 ? res : null;
        }

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
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while deleting the {ontologyId} ontology from DGraph");
            }

        }

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
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Something went wrong while deleting the {thingId} thing from the {ontologyId} ontology from DGraph");
            }

        }

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
            catch (KeyNotFoundException e)
            {
                conflict = false;
            }

            if (!conflict) // Check if the id is already on use in thingsService 
            {
                //Get and format the thing's attributes to WOT
                var props = await GetWOTThingProperties(ontologyId, thingId);

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

        private async Task<JsonObject?> GetFullOntologyJson(string ontologyId)
        {
            //We get the ontology info and its things uids and thingIds (GetThingsFromOntology in DGraph Service)
            //then we get for each thing All related nodes (GetThingInOntologyByIdAsync in DGraph Service) now we don't get duplicates in relations
            var ontologyThings = await _dgraphService.GetThingsInOntologyAsync(ontologyId);

            //add namespaces
            var namespaces = await _dgraphService.GetNamespacesInOntologyAsync(ontologyId);

            var ontologyNode = new JsonObject
            {
                ["ontologyId"] = ontologyId,
                ["namespace"] = JsonNode.Parse(namespaces.Value.GetProperty("namespace").GetRawText()),
                ["things"] = new JsonArray()
            };

            //add things
            foreach (var thingKey in ontologyThings)
            {
                if (thingKey.TryGetProperty("thingId", out var thingId))
                {
                    //get all thing information by id
                    var thingInfo = await _dgraphService.GetThingInOntologyByIdAsync(ontologyId, thingId.ToString());
                    Console.WriteLine(thingInfo);

                    if (thingInfo.HasValue)
                    {
                        string raw = thingInfo.Value.GetRawText();
                        var thing = JsonNode.Parse(raw)!.AsObject();
                        //Merge Unidirectional and Bidirectional relations, each in ~relatedTo and ~relatedFrom
                        var relatedTo = thing["~relatedTo"]?.AsArray() ?? [];
                        var relatedFrom = thing["~relatedFrom"]?.AsArray() ?? [];

                        // merge both arrays
                        var relations = new JsonArray();
                        foreach (var item in relatedTo) relations.Add(item!.DeepClone());
                        foreach (var item in relatedFrom) relations.Add(item!.DeepClone());

                        var grouped = relations
                            .Where(r => r?["Relation.name"] != null)
                            .GroupBy(r => r!["Relation.name"]!.ToString());

                        var groupedArray = new JsonArray();

                        foreach (var group in grouped)
                        {
                            // Create the grouped object with the group key
                            var groupObj = new JsonObject
                            {
                                ["Relation.name"] = group.Key
                            };

                            // Pick a sample relation from the group to copy metadata from (except "relatedTo")
                            var sample = group.FirstOrDefault();
                            if (sample is JsonObject sampleObj)
                            {
                                foreach (var kvp in sampleObj)
                                {
                                    var propName = kvp.Key;
                                    // Skip relatedTo because we'll flatten those separately
                                    if (string.Equals(propName, "relatedTo", StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    // If the grouped object doesn't already have this property, copy it
                                    if (!groupObj.ContainsKey(propName))
                                    {
                                        // clone the value to avoid shared references
                                        groupObj[propName] = kvp.Value is JsonNode node ? node.DeepClone() : null;
                                    }
                                }
                            }

                            // Flatten all relatedTo arrays in the group into one array
                            var flattened = new JsonArray();
                            foreach (var item in group)
                            {
                                var arr = item?["relatedTo"]?.AsArray();
                                if (arr is not null)
                                {
                                    foreach (var entry in arr)
                                    {
                                        flattened.Add(entry!.DeepClone());
                                    }
                                }
                            }

                            groupObj["relatedTo"] = flattened;
                            groupedArray.Add(groupObj);
                        }

                        // attach merged array
                        thing["relations"] = groupedArray;
                        thing.Remove("~relatedTo");
                        thing.Remove("~relatedFrom");

                        var mergedElement = JsonDocument.Parse(thing.ToJsonString()).RootElement;
                        ontologyNode["things"]!.AsArray().Add(JsonNode.Parse(mergedElement.GetRawText()));
                    }

                }
            }


            return ontologyNode;
        }

        [HttpGet("{ontologyId}/export/Json")]
        public async Task<IActionResult> GetAllOntologyNodes(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            var json = await GetFullOntologyJson(ontologyId);
            return Ok(json);
        }

        private JsonObject? GetJsonLDFromRegularJson(JsonObject ontologyJson, string ontologyId)
        {
            var namespaces = ontologyJson["namespace"]?.AsArray() ?? new JsonArray();
            var things = ontologyJson["things"]?.AsArray() ?? new JsonArray();

            //initialize the context with the namespaces info
            // prefix: uri

            var context = new JsonObject();

            foreach (var ns in namespaces)
            {
                if (ns == null)
                {
                    continue;
                }
                var prefix = string.IsNullOrWhiteSpace(ns["prefix"]?.GetValue<string>()) ? $"blankNodePrefix_{ontologyId}" : ns["prefix"]?.ToString();
                var uri = ns["uri"]?.GetValue<string>();

                if (prefix is not null && uri is not null)
                {
                    context[prefix] = uri;
                }
            }

            var finalThings = new JsonArray();

            //iterate through the thing nodes 
            foreach (var thingInfo in things)
            {
                var thing = new JsonObject();

                //obtener prefijo
                var prefix = thingInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
                prefix = string.IsNullOrWhiteSpace(prefix) ? $"blankNodePrefix_{ontologyId}" : prefix;
                //@id -> name (it's the thing id but without the ontology name as prefix)
                thing["@id"] = $"{prefix}:{thingInfo?["name"]}";
                //type of node is stored via hasType relation between Things (The Things that represent Types are ignored in the GetOntologyThings method) 
                //depending on the ontology, one thing may have more than one type´
                //if 1 -> JsonValue, if more -> JsonLD

                //Type(s):
                var types = thingInfo?["hasType"];
                if ((types is not null) && (types.AsArray().Count > 0))
                {
                    //this thing has at least one type
                    foreach (var typeInfo in types.AsArray())
                    {
                        var typeName = typeInfo?["name"]?.GetValue<string>();
                        var typePrefix = typeInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
                        typePrefix = string.IsNullOrWhiteSpace(typePrefix) ? $"blankNodePrefix_{ontologyId}" : typePrefix;

                        if (typeName is not null && (typeName.Length > 0))
                        {
                            if (types.AsArray().Count == 1)
                            {
                                //only one type
                                thing["@type"] = $"{((typePrefix == null || typePrefix.Length == 0) ? "" : typePrefix)}:{typeName}";
                            }
                            else
                            {
                                if (thing["@type"] is null)
                                    thing["@type"] = new JsonArray();
                                //more than one type
                                thing["@type"]?.AsArray().Add($"{((typePrefix == null || typePrefix.Length == 0) ? "" : typePrefix)}:{typeName}");
                            }
                        }
                    }
                }

                //Attributes:
                var attributes = thingInfo?["hasAttribute"];
                if ((attributes is not null) && attributes.AsArray().Count > 0)
                {
                    //this thing has attributes
                    foreach (var attributeInfo in attributes.AsArray())
                    {
                        //TODO prefix support
                        var key = attributeInfo?["Attribute.key"]?.GetValue<string>();
                        var value = attributeInfo?["Attribute.value"]?.GetValue<string>();
                        var attPrefix = attributeInfo?["Attribute.prefix"]?["prefix"]?.GetValue<string>();
                        attPrefix = string.IsNullOrWhiteSpace(attPrefix) ? $"blankNodePrefix_{ontologyId}" : attPrefix;

                        if ((key is not null) && (value is not null) && (key.Length > 0) && (value.Length > 0))
                        {
                            thing[$"{attPrefix}:{key}"] = value;
                            // thing[key] = value;
                        }
                    }
                }
                var relations = thingInfo?["relations"];
                if ((relations is not null) && relations.AsArray().Count > 0)
                {
                    //this thing has relations with other things
                    foreach (var relationInfo in relations.AsArray())
                    {
                        //TODO prefix support
                        var name = relationInfo?["Relation.name"]?.GetValue<string>();
                        var relPrefix = relationInfo?["Relation.prefix"]?["prefix"]?.GetValue<string>();
                        relPrefix = string.IsNullOrWhiteSpace(relPrefix) ? $"blankNodePrefix_{ontologyId}" : relPrefix;
                        var relatedNode = relationInfo?["relatedTo"];
                        if (relatedNode is null)
                        {
                            continue;
                        }
                        //check if there is only one lement or more
                        if (name is not null && name.Length > 0)
                        {
                            List<string> relatedThingstr = new List<string>();
                            foreach (var relatedThing in relatedNode.AsArray())
                            {
                                var relatedName = relatedThing?["name"]?.GetValue<string>();
                                var relatedPrefix = relatedThing?["Thing.prefix"]?["prefix"]?.GetValue<string>();
                                relatedPrefix = string.IsNullOrWhiteSpace(relatedPrefix) ? $"blankNodePrefix_{ontologyId}" : relatedPrefix;
                                if (relatedName is not null && relatedName.Length > 0)
                                    relatedThingstr.Add($"{relatedPrefix}:{relatedName}");
                            }
                            if (relatedThingstr.Count >= 1)
                            {
                                var jsonArray = new JsonArray();

                                foreach (var id in relatedThingstr)
                                {
                                    var node = new JsonObject
                                    {
                                        ["@id"] = id
                                    };
                                    jsonArray.Add(node);
                                }
                                
                                thing[$"{relPrefix}:{name}"] = jsonArray.Count==1 ? jsonArray[0]!.DeepClone() : jsonArray;
                            }
                        }
                    }
                }
                //add final thing to the things jsonArray
                finalThings.Add(thing);
            }

            //assemble the final json
            var jsonLd = new JsonObject
            {
                ["@context"] = context,
                ["@graph"] = finalThings
            };

            return jsonLd;
        }

        [HttpGet("{ontologyId}/export/JsonLd")]
        public async Task<IActionResult> ExportOntologyInJsonLdFormat(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            var ontologyJson = await GetFullOntologyJson(ontologyId);

            if (ontologyJson == null)
            {
                return StatusCode(500, "Something wrong with the Ontology Json:\nNull json recieved");//error 500, json malformado
            }

            if (ontologyJson["namespace"] == null)
            {
                return StatusCode(500, "Something wrong with the Ontology Json:\nNo namespace found");//error 500, json malformado
            }

            var jsonLd = GetJsonLDFromRegularJson(ontologyJson, ontologyId);

            if (jsonLd is null)
            {
                return StatusCode(500, "Something went wrong while parsing");//error 500, json malformado
            }

            return Ok(jsonLd);

            //Example format:
            /*
            {
                "@context": {
                    "@vocab": "http://schema.org/",
                    "ex": "http://example.org/university#",
                    "rdf": "http://www.w3.org/1999/02/22-rdf-syntax-ns#",
                    "rdfs": "http://www.w3.org/2000/01/rdf-schema#",
                    "owl": "http://www.w3.org/2002/07/owl#"
                },
                "@graph": [
                    {
                    "@id": "ex:Person",
                    "@type": "rdfs:Class",
                    "rdfs:label": "Person",
                    "rdfs:comment": "A human being who is part of the university."
                    },
                    {
                    "@id": "ex:Student",
                    "@type": "rdfs:Class",
                    "rdfs:subClassOf": { "@id": "ex:Person" },
                    "rdfs:label": "Student",
                    "rdfs:comment": "A person who is enrolled in one or more courses."
                    },
                    {
                    "@id": "ex:Professor",
                    "@type": "rdfs:Class",
                    "rdfs:subClassOf": { "@id": "ex:Person" },
                    "rdfs:label": "Professor",
                    "rdfs:comment": "A person who teaches courses."
                    }
                ]
            */
        }

        private void LoadNamespaceIntoGraph(JsonArray namespaces, IGraph graph, string ontologyId)
        {
            foreach (var ns in namespaces)
            {
                if (ns == null)
                {
                    continue;
                }

                var prefix = ns["prefix"];
                var uri = ns["uri"];

                if (prefix == null || uri == null)
                {
                    continue;
                }


                graph.NamespaceMap.AddNamespace(string.IsNullOrWhiteSpace(prefix.ToString()) ? $"blankNodePrefix_{ontologyId}": prefix.ToString(), new Uri(uri.ToString()));
            }
        }

        private VDS.RDF.Graph GetOntologyRDFGraph(JsonObject ontologyJson, JsonArray ns, string ontologyId)
        {
            
                
            var store = new TripleStore();
            var jsonLd = GetJsonLDFromRegularJson(ontologyJson, ontologyId);
            var jsonString = JsonSerializer.Serialize(jsonLd);
            var parser = new VDS.RDF.Parsing.JsonLdParser();
            using var reader = new StringReader(jsonString);
            parser.Load(store, reader);

            var mergedGraph = new VDS.RDF.Graph();

            //load namespaces into graph for eventual prefix parsing to uri
            LoadNamespaceIntoGraph(ns, mergedGraph, ontologyId);

            foreach (var g in store.Graphs)
            {
                mergedGraph.Merge(g, true); // true = keep namespace mappings
            }

            return mergedGraph;
            
        }

        [HttpGet("{ontologyId}/export/TTL")]
        public async Task<IActionResult> ExportOntologyInTTLFormat(string ontologyId)
        {
            var check = await _dgraphService.ExistsOntologyByIdAsync(ontologyId);
            if (!check)
            {
                return NotFound(new { message = $"Ontology '{ontologyId}' does not exist" });
            }
            var ontologyJson = await GetFullOntologyJson(ontologyId);

            if (ontologyJson == null)
            {
                return StatusCode(500, "Something wrong with the Ontology Json:\nNull json recieved");//error 500, json malformado
            }

            if (ontologyJson["namespace"] == null)
            {
                return StatusCode(500, $"Something wrong with the Ontology Json:\nNo namespace found");//error 500, json malformado
            }
            var ns = ontologyJson["namespace"]?.AsArray();

            try
            {
                var mergedGraph = GetOntologyRDFGraph(ontologyJson, ns, ontologyId);
                
                var ttlWriter = new VDS.RDF.Writing.CompressingTurtleWriter();
                using var sw = new StringWriter();
                ttlWriter.Save(mergedGraph, sw);
                string ttlString = sw.ToString();
                
                //convert the string to bytes
                var ttlBytes = Encoding.UTF8.GetBytes(ttlString);
                var stream = new MemoryStream(ttlBytes);
                //return it as a downloadable file
                return File(stream, "text/turtle", $"{ontologyId}_ontology.ttl");
            }
            catch (Exception e)
            {
                return StatusCode(500, $"Something wrong while parsing to TTL Format:{e}");
            }

            //Example format:
            /*
            @prefix ex: <http://example.org/> .
            @prefix foaf: <http://xmlns.com/foaf/0.1/> .
            @prefix schema: <http://schema.org/> .

            ex:alice a foaf:Person ;
                foaf:name "Alice" ;
                foaf:age 30 ;
                foaf:knows ex:bob, ex:carol ;
                schema:memberOf ex:bookClub .

            ex:bob a foaf:Person ;
                foaf:name "Bob" ;
                foaf:age 25 ;
                foaf:knows ex:alice, ex:dave ;
                schema:memberOf ex:chessClub .

            ex:carol a foaf:Person ;
                foaf:name "Carol" ;
                foaf:age 28 ;
                foaf:knows ex:alice ;
                schema:memberOf ex:bookClub .

            ex:dave a foaf:Person ;
                foaf:name "Dave" ;
                foaf:age 35 ;
                foaf:knows ex:bob ;
                schema:memberOf ex:chessClub .

            ex:bookClub a schema:Organization ;
                schema:name "Local Book Club" .

            ex:chessClub a schema:Organization ;
                schema:name "City Chess Club" .

            */
        }

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
                var ontologyJson = await GetFullOntologyJson(ontologyId);

                if (ontologyJson == null)
                {
                    return StatusCode(500, "Something wrong with the Ontology Json:\nNull json recieved");//error 500, json malformado
                }

                if (ontologyJson["namespace"] == null)
                {
                    return StatusCode(500, $"Something wrong with the Ontology Json:\nNo namespace found");//error 500, json malformado
                }
                var ns = ontologyJson["namespace"]?.AsArray();
                VDS.RDF.Graph graph;
                try
                {
                    graph = GetOntologyRDFGraph(ontologyJson, ns, ontologyId);
                }
                catch (Exception e)
                {
                    return StatusCode(500, $"Something wrong while getting the RDF Graph:\n{e.GetType}: {e.Message}");
                }

                //once we have the graph, we can run queries on it
                var store = new TripleStore();
                store.Add(graph);
                var dataset = new InMemoryDataset(store, true);
                var processor = new LeviathanQueryProcessor(dataset);
                var results = (SparqlResultSet)processor.ProcessQuery(query);

                //parse results format so it is readable
                if (query.QueryType == SparqlQueryType.Ask)
                    return Ok(results.Result);

                if (results.IsEmpty)
                    return Ok(new List<object>());

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