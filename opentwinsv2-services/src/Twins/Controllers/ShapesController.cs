

using J2N.Text;
using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Twins.Services;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("shapes")]
    public class ShapeController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private readonly ConverterService _converterService;

        public ShapeController(DGraphService dgraphService, ConverterService converterService)
        {
            _dgraphService = dgraphService;
            _converterService = converterService;
        }

        // -------------------------- Import auxiliary Methods --------------------------

        private void GetNquadsAsTxtFile(List<string> nquads)
        {
            //DEBUGGING
            System.IO.File.WriteAllText("nquads.txt", "");
            foreach (string str in nquads)
            {
                System.IO.File.AppendAllText("nquads.txt", str + "\n");
            }
        }
        private string GetNamespaceUid(string id, string prefix)
        {
            return $"_:{id}namespace_{prefix}";
        }

        private string GetReferenceUid(string id, string reference, string prefix)
        {
            return $"_:{id}reference_{prefix}_{reference}";
        }

        private string GetDefaultPropertyUid(string id)
        {
            return $"_:{id}defaultproperty";
        }

        private string GetDefaultPropertyUid(string id, string shape)
        {
            return $"_:{id}_{shape.Replace("_:", "")}defaultproperty";
        }

        private string GetPropertyUid(string parentUid, int count)
        {
            return $"{parentUid}_property{count}";
        }

        private string GetConstraintUid(string parentUid, string prefix, string constraint)
        {
            return $"{parentUid}_{prefix}_{constraint}constraint";
        }

        private string GetValueUid(string parentUid, int nProperty)
        {
            return $"{parentUid}_value{nProperty}";
        }

        
        private bool IsPropertyUid(string uid)
        {
            string[] parts = uid.Split("_");
            return parts[parts.Length-1].Contains("property");
        }

        private bool IsBlankUid(string uid)
        {
            string[] parts = uid.Split("_");
            return parts[parts.Length-1].Contains("Blank");
        }

        private bool isConstraintUid(string uid){
            string[] parts2 = uid.Split("_");
            if(parts2[parts2.Length-1].Contains("constraint", StringComparison.CurrentCulture))
            {
                string[] parts1 = uid.Split("Blank");
                if (parts1[parts1.Length-1].Contains("constraint"))
                {
                    return true;
                }
            }
            
            return false;
        }

        private List<string> GetNQuadsShapeGraphTriples(string shapeId, string createdAt)
        {
            List<string> nquads = new List<string>();
            /*
                shapeId -----------> shapeId
                Shape.name: -------> shapeId
                Shape.createdAt: --> timestamp
            */

            string shapeUid = $"_:{shapeId}";

            nquads.Add($"{shapeUid} <dgraph.type> \"Shape\" .");
            nquads.Add($"{shapeUid} <createdAt> \"{createdAt}\" .");
            nquads.Add($"{shapeUid} <shapeId> \"{shapeId}\" ."); //???
            nquads.Add($"{shapeUid} <Shape.name> \"{shapeId}\" .");

            return nquads;
        }

        //TODO Abstract nquads methods, this same method is present at Ontologies Controller
        private List<string> GetNQuadsNamespaceTriples(string shapeId, string createdAt, string prefix, string uri)
        {
            var nquads = new List<string>();

            /*
            namespaceId ===========> ontologyId:namespace
            prefix ================> prefix of the type "rdf:"
            uri ===================> uri that replaces the prefix (http://example.org/)
            Namespace.name ========> ontologyId:namespace
            */

            var namespace_uid = GetNamespaceUid(shapeId, prefix);
            nquads.Add($"{namespace_uid} <dgraph.type> \"Namespace\" .");
            nquads.Add($"{namespace_uid} <Namespace.createdAt> \"{createdAt}\" .");
            nquads.Add($"{namespace_uid} <namespaceId> \"{shapeId}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <Namespace.name> \"{shapeId}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <prefix> \"{prefix}\" .");
            nquads.Add($"{namespace_uid} <uri> \"{uri}\" .");

            //link namespace to the ontology
            nquads.Add($"_:{shapeId} <namespace> {namespace_uid} .");

            return nquads;
        }
        
        private List<string> GetNQuadsNodeShapeTriples(string shapeId, string shapeNodeId, string nodeUid, string prefix, string createdAt, bool isSubShape)
        {
            var nquads = new List<string>();

            /*
                nodeShapeId ------------> shapeId:shapeNodeId
                NodeShape.name ---------> shapeNodeId
                NodeShape.createdAt ----> timestamp
                NodeShape.prefix -------> uid to the namespace node
                target -----------------> Yet to be defined
                properties -------------> Yet to be defined
            */

            var graphUid = $"_:{shapeId}";
            var namespaceUid = GetNamespaceUid(shapeId, prefix);

            nquads.Add($"{nodeUid} <dgraph.type> \"NodeShape\" .");
            nquads.Add($"{nodeUid} <nodeShapeId> \"{shapeId}:{shapeNodeId}\" .");
            nquads.Add($"{nodeUid} <NodeShape.name> \"{shapeNodeId}\" .");
            nquads.Add($"{nodeUid} <NodeShape.prefix> {namespaceUid} .");
            nquads.Add($"{nodeUid} <NodeShape.createdAt> \"{createdAt}\" .");

            //Add default property
            // nquads.AddRange(GetNQuadsShapePropertyTriples(shapeId, nodeUid, "", prefix, true, 0, null, null));
            string defPropUid = GetDefaultPropertyUid(shapeId, nodeUid);
            nquads.Add($"{defPropUid} <dgraph.type> \"ShapeProperty\" .");
            nquads.Add($"{defPropUid} <ShapeProperty.prefix> {GetNamespaceUid(shapeId, prefix)} .");   
            nquads.Add($"{nodeUid} <defaultProperty> {defPropUid} .");
            nquads.Add($"{defPropUid} <description> \"Default Property Of the NodeShape\" .");
            nquads.Add($"{nodeUid} <properties> {defPropUid} .");

            //Link it to the ShapeGraph node if it's not a subshape
            if (!isSubShape)
                nquads.Add($"{graphUid} <shapes> {nodeUid} .");

            return nquads;
        }

        private List<string> GetNQuadsReferenceTriples(string shapeId, string prefix, string referenceId)
        {

            string nodeUid = GetReferenceUid(shapeId, referenceId, prefix);

            var nquads = new List<string>
            {
                $"{nodeUid} <dgraph.type> \"Reference\" .",
                $"{nodeUid} <targetId> \"{prefix}:{referenceId}\" .",
                $"{nodeUid} <Target.prefix> {GetNamespaceUid(shapeId, prefix)} .",
                $"{nodeUid} <Target.name> \"{referenceId}\" .",
            };

            return nquads;
        }

        private List<string> GetNQuadsValueTriples(string uid, int nProperty, INode node)
        {
            string dataType = "string";
            string value="";

            if (node.NodeType.Equals(NodeType.Literal))
            {
                (dataType, value) = _converterService.GetLiteralCleanData((ILiteralNode)node);
            }
            else
            {
                (dataType, value) = _converterService.GetLiteralCleanData(node.AsValuedNode().ToSafeString());
            }
            var valueUid = GetValueUid(uid, nProperty);
            var nquads = new List<string>
            {
                $"{valueUid} <dgraph.type> \"Value\" .",
                $"{valueUid} <valueId> \"{dataType}valueFor{uid.Replace("_:", "")}\" .",
                $"{valueUid} <Value.type> \"{dataType}\" .",
                $"{valueUid} <value> \"{value}\" .",
            };

            return nquads;
        }

        private bool IsListHead(IGraph g, INode node)
        {
            var rdfFirst = g.CreateUriNode("rdf:first");
            return g.GetTriplesWithSubjectPredicate(node, rdfFirst).Any();
        }

        private List<INode> ReadStrictList(IGraph g, INode head)
        {
            var items = new List<INode>();
            var rdfFirst = g.CreateUriNode("rdf:first");
            var rdfRest  = g.CreateUriNode("rdf:rest");
            var rdfNil   = g.CreateUriNode("rdf:nil");

            INode current = head;

            while (!current.Equals(rdfNil))
            {
                var first = g.GetTriplesWithSubjectPredicate(current, rdfFirst).Single().Object;
                items.Add(first);

                var rest = g.GetTriplesWithSubjectPredicate(current, rdfRest).Single().Object;
                current = rest;
            }

            return items;
        }

        private string CategorizeConstraint(string predicate)
        {
            string type = "Generic";

            switch (predicate)
            {
                case "or":
                case "and":
                case "not":
                case "xone":
                    type="Logical";
                    //assign type
                    break;
                case "minCount": //A
                case "maxCount": //B

                    type="Cardinality";
                    break;
                case "datatype":
                case "nodeKind":
                case "class":
                case "node":    
                    type="Structure";
                    break;
                case "in":
                case "hasValue":
                    type="Set";
                    break;
                case "equals":
                case "disjoint":
                case "lessThan":
                case "lessThanOrEquals":
                    type="Value";
                    break;
                default:
                    break;
            }

            return type;
        }

        private List<string> SortNodes(string shapeId, string subjectUid, string prefixPredicate, string predicate, string type, int nProperty, Triple triple, IGraph graph)
        {
            var nquads = new List<string>();

            if (triple.Object.NodeType.Equals(NodeType.Literal) || triple.Object.NodeType.Equals(NodeType.GraphLiteral) || predicate.Equals("datatype") || predicate.Equals("nodeKind"))
            {
                nquads.AddRange(GetNQuadsValueTriples(subjectUid, nProperty, triple.Object));
                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {GetValueUid(subjectUid, nProperty)} .");
            }
            else if(triple.Object.NodeType.Equals(NodeType.Uri))
            {
                var (refPrefix, refId) = _converterService.GetLocalName(triple.Object, graph, shapeId);
                nquads.AddRange(GetNQuadsReferenceTriples(shapeId, refPrefix, refId));
                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {GetReferenceUid(shapeId, refPrefix, refId)} .");
            }
            else
            {
                //Blank node
                List<Triple> source;
                if(IsListHead(graph, triple.Object))
                {
                    source = new List<Triple>();
                    foreach(INode node in ReadStrictList(graph, triple.Object))
                    {
                        nquads.AddRange(MainLoop(shapeId, $"{subjectUid}Blank{nProperty}", node, graph, true));
                        nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {subjectUid}Blank{nProperty} .");
                        nProperty++;
                    }
                    
                }
                else
                {
                    source = graph.Triples.Where(tr => tr.Subject.Equals(triple.Object)).Distinct().ToList();
                    foreach(Triple t in source)
                    {
                        if (t.Object.NodeType.Equals(NodeType.Blank))
                        {
                            //Check how many elements it has inside
                            if(!predicate.Equals("property") && (graph.Triples.Count(tr => tr.Subject.Equals(t.Object) && tr.Object.NodeType.Equals(NodeType.Blank)) > 1))
                            {
                                //pass the defaultproperty uid instead
                                nquads.AddRange(MainLoop(shapeId, $"{subjectUid}Blank{nProperty}", t.Object, graph, true));
                                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {subjectUid}Blank{nProperty} .");
                            }
                            else
                            {
                                var (childPrefix, childPredicate) = _converterService.GetLocalName(triple.Predicate, graph, shapeId);
                                nquads.AddRange(GenerateShapeTriples(shapeId, subjectUid, childPrefix, type.Equals("Generic") ? "GenericConstraint.value" : childPredicate, nProperty, t, graph));
                            }
                        }
                        else
                        {
                            nquads.AddRange(SortNodes(shapeId, isConstraintUid(subjectUid) ? subjectUid : GetConstraintUid(subjectUid, prefixPredicate, predicate), prefixPredicate, predicate, type, nProperty, t, graph));
                        }
                        nProperty++;
                    }   
                }
            }

            return nquads;
        }

        private List<string> GenerateShapeTriples(string shapeId, string subjectUid, string parentPrefix, string parentPredicate, int nProperty, Triple triple, IGraph graph)
        {
            /*
            subjectUid <parentPredicate> objUid
            objUid <predicate> nodeUid
            */

            var nquads = new List<string>();
            string prefixPredicate, predicate;

            if (!triple.Predicate.NodeType.Equals(NodeType.Literal))
            {
                (prefixPredicate, predicate) = _converterService.GetLocalName(triple.Predicate, graph, "");  //uri
            }
            else
            {
                prefixPredicate = parentPrefix;
                predicate = parentPredicate;
            }

            predicate ??= "predicate" + Guid.NewGuid();

            var isProperty = predicate.ContentEquals("property"); 
            string uid = "";
            if (isProperty)
            {
                nquads.Add($"{subjectUid} <ShapeProperty.prefix> {GetNamespaceUid(shapeId, prefixPredicate)} .");
                uid = GetPropertyUid(subjectUid, nProperty);
                if (predicate.Equals("message") && triple.Object.NodeType.Equals(NodeType.Literal))
                {
                    nquads.Add($"{uid} <description> {triple.Object.ToString()}");
                }
                else
                {
                    foreach(Triple t in graph.Triples.Where(tr => tr.Subject.Equals(triple.Object)).Distinct().ToList())
                    {
                        nquads.AddRange(GenerateShapeTriples(shapeId, uid, prefixPredicate, "constraints", nProperty, t, graph));
                    }
                }
                nProperty++;
            }
            else
            {
                switch (predicate)
                {
                    case "type":
                    case "message":                            
                        break;
                    default:
                        if (predicate.Equals("path") && IsPropertyUid(subjectUid))
                            {
                            var (refPrefix, refId) = _converterService.GetLocalName(triple.Object, graph, shapeId);
                            nquads.AddRange(GetNQuadsReferenceTriples(shapeId, refPrefix, refId));
                            nquads.Add($"{subjectUid} <path> {GetReferenceUid(shapeId, refId, refPrefix)} .");
                            break;
                        }
                        //constraint
                        uid = GetConstraintUid(subjectUid, prefixPredicate, $"{predicate}{nProperty}");
                        
                        nquads.Add($"{uid} <constrainId> \"{predicate}\" .");
                        nquads.Add($"{uid} <ShapeConstraint.prefix> {GetNamespaceUid(shapeId, prefixPredicate)} .");
                        string type = CategorizeConstraint(predicate);
                        nquads.Add($"{uid} <dgraph.type> \"{type}Constraint\" .");
                        nquads.AddRange(SortNodes(shapeId, uid, prefixPredicate, predicate, type, nProperty+1, triple, graph));
                        break;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(uid) && !string.IsNullOrWhiteSpace(parentPredicate) && !subjectUid.Equals(uid))
            {
                if (parentPredicate.Equals("constraints"))
                {
                    nquads.Add($"{(IsPropertyUid(subjectUid) ? subjectUid : GetDefaultPropertyUid(shapeId, subjectUid))} <{parentPredicate}> {uid} .");
                }
                else
                {
                    nquads.Add($"{subjectUid} <{parentPredicate}> {uid} .");
                }
            }
            return nquads;
        }

        private List<string> MainLoop(string shapeId, string nodeUid, INode node, IGraph graph, bool isSubShape)
        {   
            var nquads = new List<string>();


            List<INode> nodes = new List<INode>();
            if (node.NodeType.Equals(NodeType.Blank) && IsListHead(graph, node))
            {
                nodes = ReadStrictList(graph, node);
            }
            else
            {
                nodes.Add(node);
            }

            int nProperty = 1;
            foreach (INode nodeSubj in nodes)
            {
                var (nodePrefix, nodeId) = _converterService.GetLocalName(nodeSubj, graph, shapeId);
                var nodeUidSubj = $"{nodeUid}_{nodeId}";

                nquads.AddRange(GetNQuadsNodeShapeTriples(shapeId, nodeId, nodes.Count==1 ? nodeUid: nodeUidSubj, nodePrefix, DateTime.UtcNow.ToString("O"), isSubShape));

                var relatedTriples = graph.Triples
                    .Where(t => t.Subject.Equals(node))
                    .Distinct()
                    .ToList();
                
                

                foreach (Triple triple in relatedTriples)
                {
                    var (prefixPredicate, predicate) = _converterService.GetLocalName(triple.Predicate, graph, "");  //uri
                    predicate ??= "predicate" + Guid.NewGuid();

                    bool isProperty = predicate.Equals("property");

                    nquads.AddRange(GenerateShapeTriples(shapeId, nodes.Count==1 ? nodeUid: nodeUidSubj, prefixPredicate, isProperty ? "properties" : "constraints", nProperty, triple, graph));

                    nProperty++;
                }
            }

            return nquads;
        }

        [HttpPost("{shapeId}")]
        public async Task<IActionResult> UploadShapeGraph(string shapeId, IFormFile shapeFile)
        {
            //Check if the file has been uploaded correctly
            if (shapeFile == null || shapeFile.Length == 0)
            {
                return BadRequest("Something wrong with the uploaded file");
            }

            //Check if the file is of .ttl type
            var extension = Path.GetExtension(shapeFile.FileName);
            if (extension == null || extension.ToLower() != ".ttl")
            {
                return BadRequest("File can only be of .ttl extension, instead recieved a " + (extension is null ? "void" : extension.ToLower()) + " file");
            }

            bool check = await _dgraphService.ExistsShapeGraphByIdAsync(shapeId);
            if (!check)
            {
                //it doesn't exist

                //load TTL File into RDF Graph
                IGraph graph = new VDS.RDF.Graph();
                var parser = new TurtleParser();
                string createdAt = DateTime.UtcNow.ToString("O");

                using (var stream = shapeFile.OpenReadStream())
                using (var reader = new StreamReader(stream))
                {
                    parser.Load(graph, reader);
                }

                //Define NQUADS list and auxiliary functions
                var nquads = new List<string>();

                try
                {
                    //Add Shape Graph basic nquads
                    nquads.AddRange(GetNQuadsShapeGraphTriples(shapeId, createdAt));

                    //Get prefixes, uri pairs
                    // var ignoredPrefixes = new[] { "swrl:", "swrla:" }; //PROVISIONAL: Ignore 
                    var prefixList = graph.NamespaceMap.Prefixes
                        // .Where(p => !ignoredPrefixes.Contains(p))
                        .Select(p => new
                        {
                            Prefix = p,
                            NamespaceUri = graph.NamespaceMap.GetNamespaceUri(p).ToString()
                        })
                        .ToList();

                    //store them in Namespace nodes
                    foreach (var ns in prefixList)
                    {
                        nquads.AddRange(GetNQuadsNamespaceTriples(shapeId, createdAt, ns.Prefix, ns.NamespaceUri));
                    }

                    // Iterate through Shape nodes
                    var allShapeNodes = graph.Triples
                        .Select(t => t.Subject)
                        .Where(s => s.NodeType == VDS.RDF.NodeType.Uri)
                        .Distinct()
                        .ToList();

                    foreach (var node in allShapeNodes)
                    {
                        string uid = _converterService.GetUid(node, graph);
                        var (prefix, shapeNodeId) = _converterService.GetLocalName(node, graph, shapeId); //here it takes care of the no prefix fallback
                        shapeNodeId ??= "shape" + Guid.NewGuid();

                        //Iterate through triples
                        nquads.AddRange(MainLoop(shapeId, uid, node, graph, false));
                    }
                    nquads = nquads.Distinct().ToList();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Something wrong happened while parsing the Shape Graph to NQuads DGraph:\n{ex.GetType}: {ex.Message}");
                }
                try
                {
                    //TODO Load NQUADS List into DGraph
                    // GetNquadsAsTxtFile(nquads);
                    var response = await _dgraphService.AddNQuadTripleAsync(nquads);

                    return Ok($"{response} {nquads.ToArray().Length} triples added to DGraph successfully");

                }catch(Exception ex)
                {
                    return StatusCode(500, $"Something wrong happened while importing the Shape Graph to DGraph:\n{ex.GetType}: {ex.Message}");
                }
            }
            return Conflict($"There is already a Shape Graph with the id {shapeId}");
        }


    }
}

