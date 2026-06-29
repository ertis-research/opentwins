using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenTwinsV2.Twins.Services;
using Twins.Builders;
using Twins.Models;
using VDS.Common.Collections.Enumerations;
using VDS.RDF;
using VDS.RDF.Parsing;
using static Twins.Models.CustomType;

namespace Twins.Services
{
    /// <summary>
    /// Reunites all import functions common to Ontologies, Twins and Shapes controllers.
    /// </summary>
    public class ImportService
    {

        private readonly DGraphService _dgraphService;

        public ImportService(DGraphService dgraphService)
        {
            _dgraphService = dgraphService;
        }

        /// <summary>
        /// Checks if the node provided is the first element of a RDF list.
        /// </summary>
        /// <param name="g">The graph object.</param>
        /// <param name="node">The node that belongs to the graph and it is being checked.</param>
        /// <returns>
        /// Returns true if the node is the head of an RDF list.<br/>
        /// Returns false if the node is not the head of an RDF list.
        /// </returns>
        public static bool IsListHead(IGraph g, INode node)
        {
            var rdfFirst = g.CreateUriNode("rdf:first");
            return g.GetTriplesWithSubjectPredicate(node, rdfFirst).Any();
        }

        /// <summary>
        /// Reads through the RDF list that is headed by the node provided.
        /// </summary>
        /// <param name="g">The graph object.</param>
        /// <param name="head">The head node object of the RDF list.</param>
        /// <returns>Returns the list of node objects that are part of the RDF list.</returns>
        public static List<INode> ReadStrictList(IGraph g, INode head)
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

        #region Shapes

        /// <summary>
        /// Returns the type of Shape Constraint depending on the predicate. 
        /// </summary>
        /// <param name="predicate">The predicate of the constraint.</param>
        /// <returns>
        /// Returns Logical if the predicate is 'or', 'and', 'not' or 'xone'.<br/>
        /// Returns Cardinality if the predicate is 'minCount' or 'maxCount'.<br/>
        /// Returns Structure if the predicate is 'datatype', 'nodeKind', 'class' or 'node'.<br/>
        /// Returns Set if the predicate is 'in' or 'hasValue'.<br/>
        /// Returns Value if the predicate is 'equals', 'disjoint', 'lessThan' or 'lessThanOrEquals'.<br/>
        /// Returns Generic if the predicate did not enter in any of the defined categories.
        /// </returns>
        public static string CategorizeConstraint(string predicate)
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

        public void GetBasicAttributeShape(string prefixPredicate, string predicate, string prefixSubject, string subject, string ontologyId, ((string? Prefix, string? Name) Value, (string Prefix, string Name) Datatype) value, JsonArray graph, Dictionary<string, JsonObject> graphIndex)
        {
            BasicConstraintsInsertion(prefixPredicate, predicate, prefixSubject, subject, ontologyId, value, graph, graphIndex, false);
        }

        public void GetBasicRelationShape(string prefixPredicate, string predicate, string prefixSubject, string subject, string ontologyId, Connection value, JsonArray graph, Dictionary<string, JsonObject> graphIndex)
        {
            BasicConstraintsInsertion(prefixPredicate, predicate, prefixSubject, subject, ontologyId, (value.Name, value.Datatype??= ("", "")), graph, graphIndex, true);
        }

        private void BasicConstraintsInsertion(string prefixPredicate, string predicate, string prefixSubject, string subject, string ontologyId, ((string? Prefix, string? Name) Value, (string Prefix, string Name) Datatype) value, JsonArray graph, Dictionary<string, JsonObject> graphIndex, bool isRelation)
        {
            //1. Check if there's already a shape with targetSubjectsOf{ @id = attribute/relationName}
            // Yes -> get reference          No -> Create basic nodeShape 

            var shapeId = $"{ontologyId.ToLowerInvariant()}:{predicate}SubjectsOfShape";
            var nodeShape = graphIndex.GetValueOrDefault(shapeId);

            if (nodeShape is null || nodeShape.Count == 0)
            {
                nodeShape = ShapeBuilder.GetBasicNodeShape($"{ontologyId.ToLowerInvariant()}:{predicate}");
                graph.Add(nodeShape);
                graphIndex[shapeId] = nodeShape;
            };

            //2. Get reference to xone node
            if(nodeShape["sh:xone"] is null)
                nodeShape["sh:xone"] = new JsonArray();
            var xoneArr = nodeShape["sh:xone"]!.AsArray();
            var xoneIndex = xoneArr
                .Where(n => n is not null && n?["sh:node"] is not null)
                .ToDictionary(n => 
                (n!["sh:node"]?["sh:class"]?["@id"]!.GetValue<string>(), n!["sh:node"]?["sh:property"]?["sh:path"]?["@id"]!.GetValue<string>() ) 
                , n=> n!.AsObject());

            //3. Look if there's an inner nodeShape that has sh:node{ sh:class { @id = subject } }
            // Yes -> Append to sh:or if it has not the same restriction already    No -> Create inner restriction (static)
            JsonNode obj = isRelation ? ShapeBuilder.GetBasicIdNode((string.IsNullOrWhiteSpace(value.Datatype.Name) || value.Datatype.Name == "uri" ? value.Value.Prefix : ontologyId) ?? "", (string.IsNullOrWhiteSpace(value.Datatype.Name) || value.Datatype.Name == "uri" ? value.Value.Name : value.Datatype.Name) ?? "") : value.Value.Name ?? "";
            if(obj is not null)
                ShapeBuilder.InsertCaseIntoNodeShape(obj, FormatService.ReplacePrefix($"{prefixPredicate}:{predicate}", ontologyId), xoneArr, xoneIndex, FormatService.ReplacePrefix($"{prefixSubject}:{subject}", ontologyId), ontologyId, isRelation);
        }

        /// <summary>
        /// The list of NQuads of leaf Nodes or subNodes that are not of Reference or Value Nodes.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="subjectUid">The uid of the subject of the triple.</param>
        /// <param name="prefixPredicate">The prefix of the predicate.</param>
        /// <param name="predicate">The predicate of the triple.</param>
        /// <param name="type">The type of the node (category).</param>
        /// <param name="index">The index of child nodes of the parent node.</param>
        /// <param name="triple">The original parsed triple object.</param>
        /// <param name="graph">The original parsed graph object.</param>
        /// <returns>Returns the list of NQuads result of deciding what step of the algorithm to go back to.</returns>
        public static List<string> SortNodes(string shapeId, string subjectUid, string prefixPredicate, string predicate, string type, int index, Triple triple, IGraph graph)
        {
            var nquads = new List<string>();

            if (triple.Object.NodeType.Equals(NodeType.Literal) || triple.Object.NodeType.Equals(NodeType.GraphLiteral) || predicate.Equals("datatype") || predicate.Equals("nodeKind"))
            {
                nquads.AddRange(NQuadsService.GetNQuadsValueTriples(subjectUid, index, triple.Object));
                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {NQuadsService.GetValueUid(subjectUid, index)} .");
            }
            else if(triple.Object.NodeType.Equals(NodeType.Uri))
            {
                var (refPrefix, refId) = FormatService.GetLocalName(triple.Object, graph, shapeId);
                nquads.AddRange(NQuadsService.GetNQuadsReferenceTriples(shapeId, refPrefix, refId));
                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {NQuadsService.GetReferenceUid(shapeId, refId, refPrefix)} .");
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
                        nquads.AddRange(MainLoop(shapeId, $"{subjectUid}Blank{index}", node, graph, true));
                        nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {subjectUid}Blank{index} .");
                        index++;
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
                                nquads.AddRange(MainLoop(shapeId, $"{subjectUid}Blank{index}", t.Object, graph, true));
                                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {subjectUid}Blank{index} .");
                            }
                            else
                            {
                                var (childPrefix, childPredicate) = FormatService.GetLocalName(triple.Predicate, graph, shapeId);
                                nquads.AddRange(NQuadsService.GenerateShapeTriples(shapeId, subjectUid, childPrefix, type.Equals("Generic") ? "GenericConstraint.value" : childPredicate, index, t, graph));
                            }
                        }
                        else
                        {
                            nquads.AddRange(SortNodes(shapeId, NQuadsService.isConstraintUid(subjectUid) ? subjectUid : NQuadsService.GetConstraintUid(subjectUid, prefixPredicate, predicate), prefixPredicate, predicate, type, index, t, graph));
                        }
                        index++;
                    }   
                }
            }

            return nquads;
        }

        /// <summary>
        /// Returns the list of all NQuads related to the nodes directly related to the provided node. 
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="nodeUid">The uid of the parent node.</param>
        /// <param name="node">The parent node object.</param>
        /// <param name="graph">The original parsed graph object.</param>
        /// <param name="isSubShape">Whether the parent node is part of another Shape Node or not.</param>
        /// <returns>Returns the final list of NQuads of the node.</returns>
        public static List<string> MainLoop(string shapeId, string nodeUid, INode node, IGraph graph, bool isSubShape)
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
                var (nodePrefix, nodeId) = FormatService.GetLocalName(nodeSubj, graph, shapeId);
                var nodeUidSubj = $"{nodeUid}_{nodeId}";

                nquads.AddRange(NQuadsService.GetNQuadsNodeShapeTriples(shapeId, nodeId, nodes.Count==1 ? nodeUid: nodeUidSubj, nodePrefix, DateTime.UtcNow.ToString("O"), isSubShape));

                var relatedTriples = graph.Triples
                    .Where(t => t.Subject.Equals(node))
                    .Distinct()
                    .ToList();

                foreach (Triple triple in relatedTriples)
                {
                    var (prefixPredicate, predicate) = FormatService.GetLocalName(triple.Predicate, graph, "");  //uri
                    predicate ??= "predicate" + Guid.NewGuid();

                    bool isProperty = predicate.Equals("property");

                    nquads.AddRange(NQuadsService.GenerateShapeTriples(shapeId, nodes.Count==1 ? nodeUid: nodeUidSubj, prefixPredicate, isProperty ? "properties" : "constraints", nProperty, triple, graph));

                    nProperty++;
                }
            }

            return nquads;
        }

        /// <summary>
        /// The full list of NQuads of a Shape Graph based on the TTL structure provided.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="graph">The RDF Graph of the Shape Graph.</param>
        /// <returns>Returns the list of all NQuads of the Shape Graph and its nodes.</returns>
        public List<string> GetFullShapeGraphNquads(string shapeId, Graph graph)
        {
            var nquads = new List<string>();
            string createdAt = DateTime.UtcNow.ToString("O");
            var lowerShapeId = shapeId.ToLowerInvariant();
            try
            {
                nquads.AddRange(NQuadsService.GetNQuadsShapeGraphTriples(lowerShapeId, createdAt));

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
                    NQuadsService.AddNQuadsNamespaceTriples(lowerShapeId, ns.Prefix, ns.NamespaceUri, nquads);
                }
                //Add the default prefix pref{lowerShapeId}
                NQuadsService.AddNQuadsNamespaceTriples(lowerShapeId, $"pref{lowerShapeId}", $"http://example.org/shapeGraph/{lowerShapeId}", nquads);

                // Iterate through Shape nodes
                var allShapeNodes = graph.Triples
                    .Select(t => t.Subject)
                    .Where(s => s.NodeType == NodeType.Uri)
                    .Distinct()
                    .ToList();

                foreach (var node in allShapeNodes)
                {
                    string uid = NQuadsService.GetUid(node, graph);
                    var (prefix, shapeNodeId) = FormatService.GetLocalName(node, graph, lowerShapeId); //here it takes care of the no prefix fallback
                    shapeNodeId ??= "shape" + Guid.NewGuid();

                    //Iterate through triples
                    nquads.AddRange(MainLoop(lowerShapeId, uid, node, graph, false));
                }
                nquads = [.. nquads.Distinct()];
            }
            catch (Exception)
            {
                throw;
            }
            // File.WriteAllLines("nquads.txt", nquads);
            return nquads;
        }

        /// <summary>
        /// The full list of NQuads of a Shape Graph based on the TTL strcuture provided.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="shapeFile">The TTL file of the Shape Graph.</param>
        /// <returns>Returns the list of all NQuads of the Shape Graph and its nodes.</returns>
        public List<string> GetFullShapeGraphNquads(string shapeId, IFormFile shapeFile)
        {
            Graph graph = new();
            var parser = new TurtleParser();

            using (var stream = shapeFile.OpenReadStream())
            using (var reader = new StreamReader(stream))
            {
                parser.Load(graph, reader);
            }

            return GetFullShapeGraphNquads(shapeId.ToLowerInvariant(), graph);
        }

        public List<string> GetFullShapeGraphNquads(string shapeId, JsonObject jsonLd)
        {
            var graph = FormatService.GetRDFGraphFromJson(jsonLd, shapeId, ld: true);
            return GetFullShapeGraphNquads(shapeId, graph);
        }

        #endregion

        #region Ontologies

        private bool IsOntologyProperty(INode node, IGraph graph)
        {
            //it's a property if its type is one of the owl property ones or if it appears as a predicate
            var types = FormatService.GetNodesTypes(node, graph);
            bool res = false;
            foreach((_, var type) in types)
                res = res || type switch
                {
                    "Property" or "ObjectProperty" or "DatatypeProperty" or "FunctionalProperty" or "InverseFunctionalProperty" or "TransitiveProperty" or "SymmetricProperty" or "AsymmetricProperty" => true,
                    _ => false,
                };
            
            return res ? res : graph.GetTriplesWithPredicate(node).Any();
        }

        private IEnumerable<INode> GetDomainNode(INode node, IGraph graph)
        {
            var domains = graph.GetTriplesWithSubject(node).Where(t =>
            {
                (_, var predicate) = FormatService.GetLocalName(t.Predicate, graph, "");
                return predicate == "domain";
            }).Select(t => t.Object);
            foreach(var domain in domains)
                if(domain.NodeType == NodeType.Blank)
                    if(IsListHead(graph, domain))
                        foreach(var domainEl in ReadStrictList(graph, domain))
                            yield return domainEl;
                    else
                        yield return domain;
                else
                    yield return domain;
            yield break;
        }

        private IEnumerable<INode> GetRangeNode(INode node, IGraph graph)
        {
            var ranges = graph.GetTriplesWithSubject(node).Where(t =>
            {
                (_, var predicate) = FormatService.GetLocalName(t.Predicate, graph, "");
                return predicate == "range";
            }).Select(t => t.Object);
            foreach(var range in ranges)
                if(range.NodeType == NodeType.Blank)
                    if(IsListHead(graph, range))
                        foreach(var rangeEl in ReadStrictList(graph, range))
                            yield return rangeEl;
                    else
                        yield return range;
                else
                    yield return range;
            yield break;
        }

        private IEnumerable<(bool direct, (string Prefix, string Name))> GetPropertiesFromRestriction(INode node, IGraph graph)
        {
            var properties = graph.GetTriplesWithSubject(node).Where(t =>
            {
                (_, var predicate) = FormatService.GetLocalName(t.Predicate, graph, "");
                return predicate == "onProperty";
            }).Select(t => t.Object);
            foreach(var property in properties)
            {
                if(property.NodeType == NodeType.Blank)
                {
                    var inverses = graph.GetTriplesWithSubject(property).Where(t =>
                    {
                        (_, var predicate) = FormatService.GetLocalName(t.Predicate, graph, "");
                        return predicate == "inverseOf";
                    }).Select(t => t.Object);
                    foreach(var path in inverses)
                    {
                        (var pref, var prop) = FormatService.GetLocalName(path, graph, "");
                        yield return (false, (pref, prop));
                    }
                }
                else
                {
                    (var pref, var prop) = FormatService.GetLocalName(property, graph, "");
                    yield return (true, (pref, prop));
                }  
            } 
            yield break;
        }
        
        private IEnumerable<(string Constraint, string Prefix, string Name, string? Inner)> GetValuesFromRestriction(INode node, IGraph graph, Dictionary<INode, CustomType> urisDict, string? logicalPredicate = null)
        {
            var values = graph.GetTriplesWithSubject(node).Where(t =>
            {
                (_, var predicate) = FormatService.GetLocalName(t.Predicate, graph, ""); 
                return predicate switch
                {
                    "hasValue" or "someValuesFrom" or "allValuesFrom" => true,
                    _ => false
                };
            });

            foreach(var valueProp in values)
            {
                (_, var predicate) = FormatService.GetLocalName(valueProp.Predicate, graph, ""); 
                if(valueProp.Object.NodeType == NodeType.Uri)
                {
                    (var pref, var name) = FormatService.GetLocalName(valueProp.Object, graph, "");
                    yield return (predicate, pref, name, logicalPredicate);
                }else if(valueProp.Object.NodeType == NodeType.Blank)
                {
                    if( IsListHead(graph, valueProp.Object))
                        foreach(var innerValueObj in  ReadStrictList(graph, valueProp.Object))
                            foreach(var valueObj in GetValuesFromRestriction(innerValueObj, graph, urisDict, logicalPredicate))
                                yield return valueObj;
                    else
                        foreach(var innerValue in graph.GetTriplesWithSubject(valueProp.Object).Where(
                            t => {
                                (_, var predicate) = FormatService.GetLocalName(t.Predicate, graph, "");
                                return ShapeBuilder.IsLogicalContraintPredicate(predicate) || predicate switch
                                {
                                    "hasValue" or "someValuesFrom" or "allValuesFrom" => true,
                                    _ => false
                                };
                            }))
                            {
                                (var prefixPredicate, var innerPredicate) = FormatService.GetLocalName(innerValue.Predicate, graph, "");
                                if (ShapeBuilder.IsLogicalContraintPredicate(innerPredicate))
                                {
                                    foreach(var listObj in  IsListHead(graph, innerValue.Object) ?  ReadStrictList(graph, innerValue.Object) : Enumerable.Repeat(innerValue.Object, 1))
                                        if(listObj.NodeType == NodeType.Uri)
                                        {
                                            (var listprefix, var listName) = FormatService.GetLocalName(listObj, graph, "");
                                            yield return (predicate, listprefix, listName, innerPredicate);
                                        }    
                                }
                                else if(FormatService.GetNodesTypes(innerValue.Subject, graph).Contains(("owl","Restriction")))
                                {
                                    (var prefixObj, var obj) = FormatService.GetLocalName(innerValue.Object, graph, "");
                                    yield return (predicate, prefixObj, obj, null);
                                }else
                                    foreach(var valueObj in GetValuesFromRestriction(innerValue.Object, graph, urisDict, predicate))
                                        yield return valueObj;
                            }
                }

            }
            yield break;
        }

        private IEnumerable<(string Constraint, int Value)> GetCardinalitiesFromRestriction(INode node, IGraph graph)
        {
            var cardinalities = graph.GetTriplesWithSubject(node).Where(t =>
            {
                (_, var predicate) = FormatService.GetLocalName(t.Predicate, graph, ""); 
                return predicate switch
                {
                    "cardinality" or "maxCardinality" or "minCardinality" => true,
                    _ => false
                };
            });

            foreach(var cardinalTriple in cardinalities)
            {
                if(cardinalTriple.Object.NodeType != NodeType.Literal)
                    continue;
                (_, var predicate) = FormatService.GetLocalName(cardinalTriple.Predicate, graph, "");
                (_, var stringValue) = FormatService.GetLiteralCleanData((ILiteralNode) cardinalTriple.Object);
                if(int.TryParse(stringValue, out var intValue))
                    yield return (predicate, intValue);
            }

            yield break;
        }

        private HashSet<Constraint> BuildValueConstraintObject(INode subject, INode node, IGraph graph, Dictionary<INode, CustomType> urisDict, bool createRelations = false)
        {
            //Get the object, for each predicate, add to the list
            //if the predicate is relevant, create a new Constraint through recurssion
            //if it's not, regular Relation object

            var values = GetValuesFromRestriction(node, graph, urisDict);
            
            if(!values.Any())
                return [];

            var valueConstraints = new HashSet<Constraint>();
            var properties = GetPropertiesFromRestriction(node, graph);
            
            //If there is no value attribute in the restriction, check for range attr
        
            foreach((var isDirect, var property) in properties)
            {
                var relation = new Relation(property, isDirect);
                string? inner = null;
                Constraint? valueConstraint = null;
                foreach(var value in values)
                {
                    valueConstraint ??= new Constraint(("owl", value.Constraint));
                    relation.AddObject(new Connection((value.Prefix, value.Name), value.Constraint == "hasValue" ? null : ("", "uri")), isDirect ? FormatService.IsRelationBidirectional(subject, relation.Name.Prefix, relation.Name.Name, value.Prefix, value.Name, graph) : false);
                    if (!string.IsNullOrWhiteSpace(value.Inner))
                        inner = value.Inner;
                    else if(createRelations && property.Name != "type")
                        if(isDirect)
                            urisDict[subject].AddRelation(property.Prefix, property.Name, new Connection((value.Prefix, value.Name), ("", "uri")));
                        else
                            try
                            {
                                var objNode = graph.GetUriNode($"{value.Prefix}:{value.Name}");
                                if(objNode is not null)
                                {
                                    (var prefixSub, var subj) = FormatService.GetLocalName(subject, graph, "");
                                    urisDict[objNode].AddRelation(property.Prefix, property.Name, new Connection((prefixSub, subj), ("", "uri")));
                                }
                            }catch(Exception ex)
                            {
                                Console.WriteLine($"Could not find the object node: {ex.Message}");
                                continue;
                            }
                }
                if(valueConstraint is null)
                    continue;
                if (!string.IsNullOrWhiteSpace(inner))
                {
                    var innerConstraint = valueConstraint.GetInnerConstraint(("owl", inner)) ?? new Constraint(("owl", inner));
                    innerConstraint.AddObjects(relation.Objects.Select(o => o.Connection));
                    relation.Objects = [(innerConstraint, false)];
                }
                valueConstraint.AddObject(relation);
                valueConstraints.Add(valueConstraint);
            }
            return valueConstraints;
        }

        private IEnumerable<Constraint> BuildCardinalityConstraintObject(INode node, IGraph graph)
        {
            var cardinalities = GetCardinalitiesFromRestriction(node, graph);

            if(!cardinalities.Any())
                return [];

            var cardinalityConstraints = new HashSet<Constraint>();
            var properties = GetPropertiesFromRestriction(node, graph);

            foreach((var isDirect, var property) in properties)
                foreach(var cardinality in cardinalities)
                {
                    var cardinalityConstraint = new Constraint(("owl", cardinality.Constraint)); 
                    var relation = new Relation(property, isDirect);
                    relation.AddObject(new Connection(("", cardinality.Value.ToString()), ("xsd", "nonNegativeInteger")), false);
                    cardinalityConstraint.AddObject(relation);
                    cardinalityConstraints.Add(cardinalityConstraint);
                }

            return cardinalityConstraints;
        }

        private void TreatClassTriple(INode subject, string prefixPredicate, string predicate, INode obj, IGraph graph, Dictionary<INode, CustomType> urisDict, Connection? nest = null)
        {
            var specialCases = ShapeBuilder.IsLogicalContraintPredicate(predicate);

            (var prefixObj, var objName) = FormatService.GetLocalName(obj, graph, "");

            //if list
            if(IsListHead(graph, obj))
            {
                foreach(var objEl in ReadStrictList(graph, obj))
                    TreatClassTriple(subject, prefixPredicate, predicate, objEl, graph, urisDict, nest: nest);
                return;
            }

            var info = urisDict[subject];

            string? objValue = null;
            string? objPrefix = null;
            var objType = (Prefix: "", Name: "uri");
            switch (obj.NodeType)
            {
                case NodeType.Literal:
                    (_, objValue) =FormatService.GetLiteralCleanData((ILiteralNode)obj);
                    objType.Name = FormatService.GetXsdType(objValue).XsdType.Split(":").Last();
                    objType.Prefix = "xsd";
                    break;
                case NodeType.Uri:
                    objValue = objName;
                    objPrefix = prefixObj;
                    break;
                default:
                    //blank node
                    //list (recursive) or regular blank node                    
                    if (!specialCases)
                    {
                        //create an entry in urisDict for it (new Class)
                        if(urisDict.GetValueOrDefault(obj) is null)
                            urisDict[obj] = new CustomType(prefixObj, objName, true, false);
                        foreach(var triple in graph.GetTriplesWithSubject(obj))
                        {
                            (var prefixTriplePredicate, var triplePredicate) = FormatService.GetLocalName(triple.Predicate, graph, "");
                            TreatClassTriple(obj, prefixTriplePredicate, triplePredicate, triple.Object, graph, urisDict, nest);
                        }
                    }
                    else
                    {
                        // Skip a level, their triples are for this parent one
                        // var isRelation = false;
                        var objTypes = FormatService.GetNodesTypes(obj, graph).Select(t => t.Name);
                        if (objTypes.Contains("Restriction"))
                        {
                            //tipically, it has an onProperty -> path (or inversePath) 
                            //Shape + Info into Custom Type
                            MergeIntoCurrent(info, nest, BuildValueConstraintObject(subject, obj, graph, urisDict, predicate == "subClassOf" || predicate == "equivalentClass"), false, subject, graph);
                            MergeIntoCurrent(info, nest, BuildCardinalityConstraintObject(obj, graph), false, subject, graph);
                        }else
                            foreach(var triple in graph.GetTriplesWithSubject(obj))
                            {
                                (var prefixTriplePredicate, var triplePredicate) = FormatService.GetLocalName(triple.Predicate, graph, "");
                                if(triplePredicate == "type")
                                    continue;
                                var nestedConstraint = info.GetConstraint((prefixTriplePredicate, triplePredicate));
                                
                                if(nestedConstraint is null && !(predicate=="subClassOf" || predicate == "equivalentClass"))
                                {
                                    nestedConstraint = new Constraint((prefixTriplePredicate, triplePredicate));
                                    MergeIntoCurrent(info, nest, [nestedConstraint], false, subject, graph);
                                }

                                var nestedInnerConstraint = nestedConstraint?.GetInnerConstraint((prefixTriplePredicate, triplePredicate)) ?? new Constraint((prefixTriplePredicate, triplePredicate));
                                TreatClassTriple(subject, prefixTriplePredicate, triplePredicate, triple.Object, graph, urisDict, nest: nestedInnerConstraint ?? nestedConstraint ?? nest);
                                
                                MergeIntoCurrent(info, nestedConstraint is null || (nestedConstraint.Objects.Count == 0 &&  nestedConstraint.Name == nestedInnerConstraint?.Name) ? nest : nestedInnerConstraint, [nestedInnerConstraint!], false, subject, graph);
                            }
                    }
                    return;
            }
            if(objValue is null)
                return;
            var con = new Connection(((objType.Prefix != "xsd" && (objPrefix is null || objValue is null)) ? ("", "uri") : (objPrefix, objValue))!, (objType.Prefix, objType.Name));
            var isInherit = predicate == "subClassOf";
            MergeIntoCurrent(info, nest, [isInherit || !specialCases ? con.Name.Prefix == "xsd" ? con : new Relation((prefixPredicate, predicate), [(con, FormatService.IsRelationBidirectional(subject, prefixPredicate, predicate, con.Name.Prefix, con.Name.Name, graph))]) : con], isInherit || !specialCases, subject, graph);
        }

        private void LoopThroughNodes(INode node, IGraph graph, Dictionary<INode, CustomType> urisDict)
        {
            if(IsListHead(graph, node))
                foreach(var nodeEl in ReadStrictList(graph, node))
                    LoopThroughNodes(nodeEl, graph, urisDict);
            else
                foreach(var triple in graph.GetTriplesWithSubject(node))
                {
                    //assess depending on predicate and type of object node
                    (var prefixPredicate, var predicate) = FormatService.GetLocalName(triple.Predicate, graph, "");
                    TreatClassTriple(node, prefixPredicate, predicate, triple.Object, graph, urisDict);
                }
        }

        private bool IsRelationRedundant((string Prefix, string Name) relName, (string Prefix, string Name) subj, ((string Prefix, string Name) Name, (string Prefix, string Name)? Datatype) obj, IGraph graph, Dictionary<INode, CustomType> urisDict)
        {
            //check if the relation in the Thing has already defined its domain and/or range
            //if yes, omit creating the specific for the Thing relations
            INode? node = null;
            try
            {
                node = graph.GetUriNode($"{relName.Prefix}:{relName.Name}");
            }catch(Exception){
                return false;
            }
            if(node is null)
                return false;
            
            var info = urisDict.GetValueOrDefault(node);
            if(info is null)
                return false;

            if(info.Domain.Any(d => d.Prefix == subj.Prefix && d.Name == subj.Name))
                return false;

            return info.Range.Any(r => (r.Prefix == obj.Name.Prefix && r.Name == obj.Name.Name) || (r.Prefix == obj.Datatype?.Prefix && r.Name == obj.Datatype?.Name));
        }

        private void SortThingType((string Prefix, string Name) type, string name, string uid, string ontologyId, JsonArray shapeGraph, Dictionary<string, JsonObject> shapeGraphIndex, HashSet<string> nquads)
        {
            var shapeId = $"{ontologyId.ToLowerInvariant()}:{name}SubjectsOfShape";
            var shape = shapeGraphIndex.GetValueOrDefault(shapeId);
            (string? Predicate, JsonNode? Value) value = (null,null); 
            switch (type.Name)
            {
                case "ObjectProperty":
                    value.Predicate = "sh:nodeKind";
                    value.Value = ShapeBuilder.GetBasicIdNode("sh", "IRI");
                    break;
                case "DatatypeProperty":
                    value.Predicate = "sh:nodeKind";
                    value.Value = ShapeBuilder.GetBasicIdNode("sh", "Literal");
                    break;
                case "FunctionalProperty":
                    value.Predicate = "sh:maxCount";
                    value.Value = 1;
                    break;
                case "InverseFunctionalProperty":
                    shapeId = $"{ontologyId.ToLowerInvariant()}:{name}ObjectsOfShape";
                    shape = shapeGraphIndex.GetValueOrDefault($"{ontologyId.ToLowerInvariant()}:{name}ObjectsOfShape");
                    if(shape is null)
                    {
                        shape = ShapeBuilder.GetBasicNodeShape($"{ontologyId.ToLowerInvariant()}:{name}", "ObjectsOf");
                        shapeGraphIndex[shapeId] = shape;
                        shapeGraph.Add(shape);
                    }
                    ShapeBuilder.MergeIntoShapeProperty(shape, (ontologyId, name), "sh:maxCount", 1);
                    return;
                case "SymmetricProperty":
                    value.Predicate = "sh:equals";
                    value.Value = new JsonObject{["sh:inversePath"] = ShapeBuilder.GetBasicIdNode(ontologyId, name)};
                    break;
                case "AssymmetricProperty":
                    value.Predicate = "sh:disjoint";
                    value.Value = new JsonObject{["sh:inversePath"] = ShapeBuilder.GetBasicIdNode(ontologyId, name)};
                    break;
                case "TransitiveProperty":
                    return;
                default:
                    var typeUid = NQuadsService.GetTypeUid(type.Prefix ?? "", type.Name, ontologyId, nquads);
                    nquads.Add($"{uid} <hasType> {typeUid} .");
                    return;
            }
            if(shape is null)
            {
                shape = ShapeBuilder.GetBasicNodeShape($"{ontologyId.ToLowerInvariant()}:{name}");
                shapeGraphIndex[shapeId] = shape;
                shapeGraph.Add(shape);
            }
            if(!(string.IsNullOrWhiteSpace(value.Predicate) || value.Value is null))
                ShapeBuilder.MergeIntoShapeProperty(shape, (ontologyId, name), value.Predicate, value.Value);
        }

        private void SortConstraintsIntoShapesAndNQuads(CustomType info, (string Prefix, string Name) constraint, IEnumerable<Connection> objs, string ontologyId, Dictionary<INode, CustomType> urisDict, IGraph graph, JsonArray shapeGraph, Dictionary<string, JsonObject> shapeGraphIndex, HashSet<string> nquads, JsonArray? currentProperty = null, bool isNot = false)
        {
            if(!objs.Any())
                return;
            string uid = $"_:{ontologyId}_{info.Name}";
            string? shaClEq = ShapeBuilder.GetShaClEquivalentFromLogicalOWL(constraint.Name, objs.Count()>1);

            bool isValueCons = constraint.Name switch
            {
                "someValuesFrom" or "allValuesFrom" or "hasValue" => true,
                _ => false
            };

            var shapeId = $"{ontologyId.ToLowerInvariant()}:{info.Name}ClassShape";
            var shape = shapeGraphIndex.GetValueOrDefault(shapeId);
            
            if(shape is null)
            {
                shape = ShapeBuilder.GetBasicNodeShape($"{ontologyId.ToLowerInvariant()}:{info.Name}", "Class");
                shapeGraphIndex[shapeId] = shape;
                shapeGraph.Add(shape);
            }

            JsonNode? parentConsArr = null;
            bool nest = shaClEq is not null;
            if(nest)
            {
                parentConsArr = currentProperty is null ? new JsonArray() : new JsonObject{[shaClEq!] = new JsonArray()};
                if(currentProperty is null)
                    shape![shaClEq!] ??= parentConsArr;
                else
                    currentProperty!.Add(parentConsArr);
            }
            foreach(var obj in objs)
            {
                if(obj is null)
                    continue;
                else if(obj is Constraint objCons )
                    SortConstraintsIntoShapesAndNQuads(info, objCons.Name, objCons.Objects, ontologyId, urisDict, graph, shapeGraph, shapeGraphIndex, nquads, currentProperty is null ? parentConsArr!.AsArray() : parentConsArr![shaClEq!]!.AsArray(), shaClEq == "sh:not");
                else if(obj is Relation objRel)
                {
                    //first, get a property with the proper path
                    //if there isn't one,k create it
                    JsonObject? consInnerShape = !(parentConsArr is not null || currentProperty is not null) ? null : [];
                    if(parentConsArr is not null || currentProperty is not null)
                        (currentProperty is null ? parentConsArr : currentProperty)!.AsArray().Add(consInnerShape);
                    
                    switch (constraint.Name)
                    {
                        case "someValuesFrom":
                        case "allValuesFrom":
                            var isSomeValues = constraint.Name == "someValuesFrom";
                            foreach((var relObjective, var isBid) in objRel.Objects)
                            {
                                var node = new JsonObject();
                                var predicate = isSomeValues ? "sh:qualifiedValueShape" : relObjective.Name.Prefix == "xsd" ? "sh:datatype" : "sh:class";
                                if(relObjective is Constraint cons)
                                {
                                    var shaClEquiv = ShapeBuilder.GetShaClEquivalentFromLogicalOWL(cons.Name.Name, cons.Objects.Count>1);
                                    if(shaClEquiv is not null)
                                    {
                                        var parsed = cons.Objects.Select<Connection, JsonNode>(ob => new JsonObject{[predicate] = ob.Datatype?.Prefix == "xsd" ? ob.Name.Name : ShapeBuilder.GetBasicIdNode(ontologyId, ob.Name.Name)});
                                        // node[] = cons.Objects.Count==1 ? parsed.First() : new JsonArray([.. parsed]);
                                        ShapeBuilder.MergeIntoShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), shaClEquiv, cons.Objects.Count==1 ? parsed.First() : new JsonArray([.. parsed]));
                                    }
                                }else
                                {
                                    bool isLiteral = relObjective.Datatype?.Prefix == "xsd";
                                    bool isAttribute = relObjective.Name.Prefix == "xsd";
                                    // node = isSomeValues ? new JsonObject {[isAttribute ? "sh:datatype" : "sh:class"] = ShapeBuilder.GetBasicIdNode(isAttribute ? relObjective.Name.Prefix : ontologyId, relObjective.Name.Name)} : ShapeBuilder.GetBasicIdNode(isAttribute ? relObjective.Name.Prefix : ontologyId, relObjective.Name.Name);
                                    JsonNode value = isLiteral ? relObjective.Name.Name : ShapeBuilder.GetBasicIdNode(isAttribute ? relObjective.Name.Prefix : ontologyId, relObjective.Name.Name);
                                    ShapeBuilder.MergeIntoShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), predicate, value);
                                    //Add relation/attribute nquads
                                    
                                    if(!isNot && info.Relations.GetValueOrDefault(objRel.Name) is null)
                                        if(isAttribute)
                                            NQuadsService.AddNQuadThingAttributeTriples(uid, objRel.Name.Name, relObjective.Datatype?.Name == "uri" ? relObjective.Name.Name : relObjective.Datatype?.Name!, relObjective.Datatype?.Name == "uri" ? null : relObjective.Name.Name, ontologyId, objRel.Name.Prefix, nquads);
                                        else if(relObjective.Name.Prefix != "xsd")
                                            NQuadsService.AddNQuadThingRelationTriples(objRel.IsDirect ? uid : $"_:{ontologyId}_{relObjective.Name.Name}", objRel.Name.Name, !objRel.IsDirect ? uid : $"_:{ontologyId}_{relObjective.Name.Name}", isBid, ontologyId, objRel.Name.Prefix, nquads);
                                }
                            }
                            if(isSomeValues)
                                ShapeBuilder.MergeIntoShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), "sh:qualifiedMinCount", 1);
                            break;
                        case "hasValue":
                            //isValueCons is not null
                            //create both relation (if not specified otherwise) and its shape equivalent (always)
                            foreach(var valueObj in objRel.Objects)
                                ShapeBuilder.MergeIntoShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), "sh:hasValue", valueObj.Connection.Datatype?.Prefix == "xsd" ? valueObj.Connection.Name.Name : ShapeBuilder.GetBasicIdNode(ontologyId, valueObj.Connection.Name.Name));
                            
                            break;
                        case "maxCardinality":
                        case "minCardinality":
                        case "cardinality":
                            //min/maxCardinality --------------> sh:min/maxCount
                            //should only point to one object
                            var cardStringValue = objRel.Objects.FirstOrDefault();
                            if(cardStringValue.Connection is null)
                                continue;

                            if(!int.TryParse(cardStringValue.Connection.Name.Name, out var cardInt))
                                continue;

                            if (constraint.Name == "cardinality")
                            {
                                if(!ShapeBuilder.ExistsShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), "sh:minCount"))
                                    //TODO: Override?
                                    ShapeBuilder.MergeIntoShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), "sh:minCount", cardInt);
                                if(!ShapeBuilder.ExistsShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), "sh:maxCount"))
                                    //TODO: Override?
                                    ShapeBuilder.MergeIntoShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), "sh:maxCount", cardInt);
                            }
                            else
                            {
                                var min = constraint.Name == "minCardinality";
                                var shaPred = $"sh:{(min ? "min" : "max")}Count"; 
                                if(!ShapeBuilder.ExistsShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), shaPred))
                                    //TODO: Override?
                                    ShapeBuilder.MergeIntoShapeProperty(consInnerShape ?? shape!, (ontologyId, objRel.Name.Name), shaPred, cardInt);
                            }
                            break;
                        default:
                            break;
                    }
                }
                else
                    //Regular Connection
                    //is uri type?
                    if(parentConsArr is not null)
                    {
                        JsonNode value = obj.Datatype is not null && obj.Datatype?.Name == "uri" ? new JsonObject{["sh:class"] = ShapeBuilder.GetBasicIdNode(ontologyId, obj.Name.Name)} : obj.Name.Name;
                        (currentProperty is null ?  parentConsArr : parentConsArr![shaClEq!])!.AsArray().Add(value);
                    }
            }
        }

        public void ConvertInformationIntoShapesAndNQuads(string ontologyId, INode node, Dictionary<INode, CustomType> urisDict, IGraph graph, JsonArray shapeGraph, Dictionary<string, JsonObject> shapeGraphIndex, HashSet<string> nquads)
        {
            (var subjPrefix, var subj) = FormatService.GetLocalName(node, graph, "");
            var info = urisDict[node];
            var uid = NQuadsService.GetUid(node, graph, ontologyId);
            if (info.IsType)
            {
                //it's a type/class/instanciation
                //NQuads for Thing
                NQuadsService.AddNQuadThingNodeTriples(uid, info.Name, ontologyId, info.Prefix, nquads);

                //Look into realtions
                foreach((var relName, var relUris) in info.Relations)
                    foreach(var relUri in relUris.Objects)
                    {
                        if(relName.Name == "type" && !string.IsNullOrWhiteSpace(relUri.Connection.Name.Name) && !relUri.Connection.Name.Name.Contains("blank"))
                            //Check for special types (properties -> no nquads, ny shapes
                            SortThingType(relUri.Connection.Name, subj, uid, ontologyId, shapeGraph, shapeGraphIndex, nquads);
                        else if(relName.Name == "subClassOf" && relUri.Connection is not Relation && relUri.Connection is not Constraint)
                            nquads.Add($"{uid} <inheritsFrom> _:{ontologyId}_{relUri.Connection.Name.Name} .");
                        else if(relUri.Connection.Name.Prefix != "xsd" && (string.IsNullOrWhiteSpace(relUri.Connection.Datatype?.Name) || relUri.Connection.Datatype?.Name == "uri" ))
                        {
                            bool isDatatypeObj = string.IsNullOrWhiteSpace(relUri.Connection.Name.Name) || relUri.Connection.Datatype?.Name != "uri";
                            bool isBidirectional = FormatService.IsRelationBidirectional(node, relName.Prefix, relName.Name, (isDatatypeObj ? relUri.Connection.Datatype : relUri.Connection.Name)?.Prefix ?? "", (isDatatypeObj ? relUri.Connection.Datatype : relUri.Connection.Name)?.Name ?? "", graph);
                            
                            if(relUris.IsDirect)
                                NQuadsService.AddNQuadThingRelationTriples(uid, relName.Name, $"_:{ontologyId}_{(isDatatypeObj ? relUri.Connection.Datatype : relUri.Connection.Name)?.Name}", isBidirectional, ontologyId, relName.Prefix, nquads);
                            else
                                NQuadsService.AddNQuadThingRelationTriples($"_:{ontologyId}_{(isDatatypeObj ? relUri.Connection.Datatype : relUri.Connection.Name)?.Name}", relName.Name, uid, isBidirectional, ontologyId, relName.Prefix, nquads);

                            if(IsRelationRedundant(relName, (info.Prefix, info.Name), (relUri.Connection.Name, relUri.Connection.Datatype), graph, urisDict))
                                GetBasicRelationShape(relName.Prefix, relName.Name, info.Prefix, info.Name, ontologyId, relUri.Connection, shapeGraph, shapeGraphIndex);
                        }
                        else
                        {
                            NQuadsService.AddNQuadThingAttributeTriples(uid, relName.Name, relUri.Connection.Datatype?.Name == "uri" ? relUri.Connection.Name.Name : relUri.Connection.Datatype?.Name!, relUri.Connection.Datatype?.Name == "uri" ? null : relUri.Connection.Name.Name, ontologyId, relName.Prefix, nquads);
                            
                            if(IsRelationRedundant(relName, (info.Prefix, info.Name), (relUri.Connection.Name, relUri.Connection.Datatype), graph, urisDict))
                                GetBasicAttributeShape(relName.Prefix, relName.Name, info.Prefix, info.Name, ontologyId, (relUri.Connection.Name, relUri.Connection.Datatype ??= ("","")), shapeGraph, shapeGraphIndex);
                            //here, we don't set range/domain of the attribute (own CustomType info)
                        }
                    }

                //Iterate through the constraints
                foreach((string Prefix, string Name) name in info.Constraints.Keys)
                {
                    SortConstraintsIntoShapesAndNQuads(info, name, info.Constraints[name].Objects, ontologyId, urisDict, graph, shapeGraph, shapeGraphIndex, nquads);
                    if(name.Name == "intersectionOf")
                        foreach(var subs in info.Constraints[name].Objects)
                        {
                            if(subs.Name.Prefix == "owl")
                                continue;
                            try
                            {
                                var nodeInGraph = graph.GetUriNode($"{subs.Name.Prefix}:{subs.Name.Name}");
                                if(nodeInGraph is null || urisDict.GetValueOrDefault(nodeInGraph) is null)
                                    continue;
                                foreach(var rel in urisDict[nodeInGraph].Relations)
                                {
                                    foreach(var relObj in rel.Value.Objects)
                                        if(rel.Value.Name.Prefix != "xsd" && (string.IsNullOrWhiteSpace(rel.Value.Datatype?.Name) || rel.Value.Datatype?.Name == "uri" ))
                                        {
                                            bool isDatatypeObj = string.IsNullOrWhiteSpace(relObj.Connection.Name.Name) || relObj.Connection.Datatype?.Name != "uri";
                                            bool isBidirectional = FormatService.IsRelationBidirectional(node, rel.Key.Prefix, rel.Key.Name, (isDatatypeObj ? relObj.Connection.Datatype : relObj.Connection.Name)?.Prefix ?? "", (isDatatypeObj ? relObj.Connection.Datatype : relObj.Connection.Name)?.Name ?? "", graph);
                                            if(!IsRelationRedundant(rel.Value.Name, !rel.Value.IsDirect ? relObj.Connection.Name : (subjPrefix, subj), rel.Value.IsDirect ? (relObj.Connection.Name, relObj.Connection.Datatype) : ((subjPrefix, subj), ("", "uri")), graph, urisDict))
                                                NQuadsService.AddNQuadThingRelationTriples(rel.Value.IsDirect ? uid : $"_:{ontologyId}_{(isDatatypeObj ? relObj.Connection.Datatype : relObj.Connection.Name)?.Name}", rel.Value.Name.Name, rel.Value.IsDirect ? $"_:{ontologyId}_{(isDatatypeObj ? relObj.Connection.Datatype : relObj.Connection.Name)?.Name}" : uid, isBidirectional, ontologyId, rel.Value.Name.Prefix, nquads);
                                        }
                                        else
                                            NQuadsService.AddNQuadThingAttributeTriples(uid, rel.Value.Name.Name, relObj.Connection.Datatype?.Name == "uri" ? relObj.Connection.Name.Name : relObj.Connection.Datatype?.Name!, relObj.Connection.Datatype?.Name == "uri" ? null : relObj.Connection.Name.Name, ontologyId, rel.Value.Name.Prefix, nquads);
                                }
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                        }
                }
            }
            else 
            {
                //The dict was sorted so the entries that are not classes come first, so if any shape for range or domain is defined, it goes first (less redundancy afterwards)
                var shapeId = $"{ontologyId.ToLowerInvariant()}:{info.Name}Shape";
                var shape = shapeGraphIndex.GetValueOrDefault(shapeId) ?? [];

                if(info.Domain.Any())
                    ShapeBuilder.GetDomainNode(ontologyId, info.Name, ontologyId, info.Domain, !info.IsAttribute, shape);

                if(info.Range.Any())
                    ShapeBuilder.GetRangeNode(ontologyId, info.Name, ontologyId, info.Range, !info.IsAttribute, shape);

                if(info.Domain.Count != 0 && info.Range.Count != 0)
                {
                    var pairs = info.Domain.Where(d => d.Prefix != "xsd").SelectMany(a => info.Range, (a,b) => new {Dom = a, Rng = b});
                    foreach(var pair in pairs) 
                        if(!info.IsAttribute)
                            NQuadsService.AddNQuadThingRelationTriples($"_:{ontologyId}_{pair.Dom.Name}", info.Name, $"_:{ontologyId}_{pair.Rng.Name}", false, ontologyId, info.Prefix, nquads);
                }
                    
                if (shape is not null && shape!.Count > 0 && !shapeGraphIndex.ContainsKey(shapeId))
                {
                    shapeGraphIndex[shapeId] = shape;
                    shapeGraph.Add(shape);
                }

                foreach((var relName, var relUris) in info.Relations)
                    foreach(var relUri in relUris.Objects)
                        if(relName.Name == "type" && !string.IsNullOrWhiteSpace(relUri.Connection.Name.Name) && !relUri.Connection.Name.Name.Contains("blank"))
                            //Check for special types (properties -> no nquads, ny shapes
                            SortThingType(relUri.Connection.Name, info.Name, uid, ontologyId, shapeGraph, shapeGraphIndex, nquads);
                    
            }
            var options = new JsonSerializerOptions { WriteIndented = true };
            // string jsonString = System.Text.Json.JsonSerializer.Serialize(shapeGraph, options);

            // File.WriteAllText("shapejson.json", jsonString);
        }

        public async Task<HashSet<string>> GetFullOntologyNQuadsAndShapesFromGraph(string ontologyId, IGraph graph, JsonArray shapeGraph)
        {
            HashSet<string> nquads = [];
            var ignoredPrefixes = new[] { "swrl:", "swrla:" };

            var urisDict = new Dictionary<INode, CustomType>();

            var namedEntities = graph.Triples.Where(t => t.Subject.NodeType == NodeType.Uri).Select(t=> t.Subject).ToHashSet();
            foreach(var node in graph.Triples.Where(t => t.Object.NodeType == NodeType.Uri).Select(t => t.Object))
                namedEntities.AddIfMissing(node);

            foreach(var node in namedEntities)
            {
                (var prefix, var name)= FormatService.GetLocalName(node, graph, "");
                urisDict[node] = new CustomType(prefix, name);
            }
            foreach(var node in namedEntities)
            {
                var localName = urisDict[node];
                if(IsOntologyProperty(node, graph))
                {
                    //look for range + domain
                    foreach(var domain in GetDomainNode(node, graph))
                        urisDict[node].AddDomainUri(FormatService.GetLocalName(domain, graph, ""));
                    foreach(var range in GetRangeNode(node, graph))
                    {
                        var uri = FormatService.GetLocalName(range, graph, "");
                        if(range.NodeType == NodeType.Literal || uri.Prefix == "xsd"){
                            urisDict[node].IsAnAttribute();
                        }else
                            urisDict[node].IsARelation();

                        urisDict[node].AddRangeUri(uri);
                    }
                    //append type as a relation
                    var types = FormatService.GetNodesTypes(node, graph);
                    foreach(var type in types)
                        urisDict[node].AddRelation("rdfs", "type", new Connection((type.Prefix, type.Name), ("", "uri")));
                }else
                {
                    urisDict[node].IsAClass();
                    LoopThroughNodes(node, graph, urisDict);
                }
            }

            foreach(var pair in urisDict)
                Console.WriteLine($"{pair.Key} --> {pair.Value}");

            //Iterate through dictionary extracting at the same time NQUADS and Shapes
            NQuadsService.AddNQuadsOntologyTriples(ontologyId, nquads);
            NQuadsService.AddPrefixNQuadsTriples(ontologyId, graph, ignoredPrefixes, nquads);
            var shapeGraphIndex = new Dictionary<string, JsonObject>();
            foreach((var node, var info) in urisDict.OrderBy(uri => uri.Value.IsType))
            {
                if(node is null || info is null)
                    continue;

                ConvertInformationIntoShapesAndNQuads(ontologyId, node, urisDict, graph, shapeGraph, shapeGraphIndex, nquads);
            }

            if(shapeGraph.Count > 0){
            
                
                //1. The id of the shape graph must be autogenerated from the ontologyId
                // ---> If it's taken? Add random Guid sequence at the end + add the relation of default ShapeGraph
                var shapeId = $"{ontologyId.ToLowerInvariant()}_defaultshapegraph";

                //Possible liability
                while(await _dgraphService.ExistsShapeGraphByIdAsync(shapeId))
                    shapeId = $"{ontologyId.ToLowerInvariant()}_defaultshapegaph_{Guid.NewGuid().ToString()[..10]}";

                //if everything went well, the only prefixes i will need are sh, xsd, ontologyId
                var shapeGraphNQuads = GetFullShapeGraphNquads(shapeId, new JsonObject{
                    ["@context"] = new JsonObject
                    {
                        ["sh"] = "http://www.w3.org/ns/shacl#",
                        ["xsd"] = "http://www.w3.org/2001/XMLSchema#",
                        [ontologyId.ToLowerInvariant()] = $"http://example.org/ontology/{ontologyId}", //Provisional
                        ["sh:nodeKind"] = new JsonObject { ["@type"] = "@id" },
                        ["sh:class"] = new JsonObject { ["@type"] = "@id" },
                        ["sh:targetClass"] = new JsonObject { ["@type"] = "@id" },
                        ["sh:path"] = new JsonObject { ["@type"] = "@id" },
                        ["sh:datatype"] = new JsonObject { ["@type"] = "@id" }
                    },
                    ["@graph"] = shapeGraph
                });

                try
                {
                    await _dgraphService.AddNQuadTripleAsync(shapeGraphNQuads);
                }
                catch (Exception ex)
                {
                    throw new Exception($"During uploading the Shape Graph NQuads: {ex.Message}");
                }

                //once it has been succesfully uploaded, create defaultShapeGraph relation between the Ontology and the Shape Graph
                var shapeUid = await _dgraphService.GetShapeGraphUid(shapeId);
                nquads.Add($"_:{ontologyId} <defaultShapeGraph> <{shapeUid}> .");
            }
            return nquads;
        }

        public async Task<ICollection<string>> GetFullOntologyNQuadsFromFile(string ontologyId, IFormFile ontologyFile, JsonArray shapeGraph)
        {

            IGraph graph = new Graph();
            var parser = new TurtleParser();

            //the RDF parser will load into the graph the data from the file using the stream reader
            using (var stream = ontologyFile.OpenReadStream())
            using (var reader = new StreamReader(stream))
            {
                parser.Load(graph, reader);
            }

            var nquads = await GetFullOntologyNQuadsAndShapesFromGraph(ontologyId, graph, shapeGraph);
            // File.WriteAllLines("nquads.txt", nquads);
            return nquads;
        }
    }

    #endregion
}