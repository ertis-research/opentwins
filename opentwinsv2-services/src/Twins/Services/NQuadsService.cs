using System.Security.Cryptography.X509Certificates;
using J2N.Text;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;


namespace OpenTwinsV2.Twins.Services
{
    /// <summary>
    /// Handles Generation of NQuads for Twins Project  
    /// </summary>
    public class NQuadsService
    {

        private readonly ConverterService _converterService;

        public NQuadsService(ConverterService converterService)
        {
            _converterService = converterService;
        }

    /// <summary>
    /// Returns the proper uid for a namespace depending on the prefix and parent's identifier.
    /// </summary>
    /// <param name="id">The identifier of the parent.</param>
    /// <param name="prefix">The prefix of the namespace.</param>
    /// <returns>Returns the uid of the namespace in the form _:{id}namespace_{prefix}.</returns>
        private string GetNamespaceUid(string id, string prefix)
        {
            return $"_:{id}namespace_{prefix}";
        }

        /// <summary>
        /// Returns the proper uid for a Shape Reference depending on the reference, prefix and aprent's identifier.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <param name="reference">The name of the reference.</param>
        /// <param name="prefix">The prefix of the reference.</param>
        /// <returns>Returns the uid of the reference in the form _:{id}reference_{prefix}_{reference}.</returns>
        private string GetReferenceUid(string id, string reference, string prefix)
        {
            return $"_:{id}reference_{prefix}_{reference}";
        }

        /// <summary>
        /// Returns the uid of the default Property of a node depending of its identifier.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <returns>Returns the uid of the default Property in the form _:{id}defaultproperty.</returns>
        private string GetDefaultPropertyUid(string id)
        {
            return $"_:{id}defaultproperty";
        }

        /// <summary>
        /// Returns the uid of the default Property of a node depending of its identifier and its name.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <param name="shape">The name of the shape.</param>
        /// <returns>Returns the uid of the default Property in the form _:{id}_{shape}defaultproperty.</returns>
        private string GetDefaultPropertyUid(string id, string shape)
        {
            return $"_:{id}_{shape.Replace("_:", "")}defaultproperty";
        }

        /// <summary>
        /// Returns the uid of a Shape Property depending on the uid of the uid of its parent and the property count.
        /// </summary>
        /// <param name="parentUid">The uid of the parent.</param>
        /// <param name="count">The index of node child of the parent.</param>
        /// <returns>Return the uid of the Shape Property in the form {parentUid}_property{count}.</returns>
        private string GetPropertyUid(string parentUid, int count)
        {
            return $"{parentUid}_property{count}";
        }

        /// <summary>
        /// Returns the uid of a Shape Constraint depending on the uid of its parent, its prefix and its constraint name.
        /// </summary>
        /// <param name="parentUid">The uid of its parent.</param>
        /// <param name="prefix">The prefix of the constraint.</param>
        /// <param name="constraint">The name of the constraint.</param>
        /// <returns>The uid of the ShapeConstraint in the form {parentUid}_{prefix}_{constraint}constraint</returns>
        private string GetConstraintUid(string parentUid, string prefix, string constraint)
        {
            return $"{parentUid}_{prefix}_{constraint}constraint";
        }

        /// <summary>
        /// Returns the uid of a Shape Value depending on the uid of its parent and the property count.
        /// </summary>
        /// <param name="parentUid">The uid of the parent.</param>
        /// <param name="nProperty">The index of Property of the parent.</param>
        /// <returns>The uid of the Shape Value in the form {parentUid}_value{nProperty}</returns>
        private string GetValueUid(string parentUid, int nProperty)
        {
            return $"{parentUid}_value{nProperty}";
        }

        /// <summary>
        /// Returns whether a uid belongs to a Shape Property Node or not.
        /// </summary>
        /// <param name="uid">The uid of the node that is being checked.</param>
        /// <returns>
        /// Returns true if the uid's node is a Shape Property.<br/>
        /// Returns false if the uid's node is not a Shape Property.
        /// </returns>
        private bool IsPropertyUid(string uid)
        {
            string[] parts = uid.Split("_");
            return parts[parts.Length-1].Contains("property");
        }

        /// <summary>
        /// Returns whether a uid belongs to a Shape Constraint or not.
        /// </summary>
        /// <param name="uid">The uid of the node that is being checked.</param>
        /// <returns>
        /// Returns true if the uid's node is a Shape Constraint.<br/>
        /// Returns false if the uid's node is not a Shape Constraint.
        /// </returns>
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

        /// <summary>
        /// Returns the list of necessary NQuads of a Thing's Attribute Node.
        /// </summary>
        /// <param name="subject">The uid of the Attribute's parent node.</param>
        /// <param name="predicate">The predicate of the triple representing the Attribute.</param>
        /// <param name="literal">The object node of the triple representing the Attribute.</param>
        /// <param name="ontology">The identifier of the Ontology of the nodes.</param>
        /// <param name="prefix">The prefix of the Attribute.</param>
        /// <returns>Returns the list of necessary NQuads of the Attribute Node.</returns>
        private List<string> GetNQuadThingAttributeTriples(string subject, string predicate, ILiteralNode literal, string ontology, string prefix)
        {

            /*
            type Attribute {
                Attribute.key =============> Generated key (it will overrided when uploaded to DGraph)
                Attribute.type ============> Type of the attribute value (DGraph format)
                Attribute.value ===========> Value of the attribute casted to string
            }
            */

            var nquads = new List<string>();

            // string value = _converterService.SanitizeAttributeValue(literal.Value);
            // //There's the possibility of the value containing " characters

            // //string type by default
            // string dataType = literal.DataType?.ToString() ?? "string";
            // var type = _converterService.SanitizeTypeAndUIDValues(dataType) ?? "string";

            var(type, value) = _converterService.GetLiteralCleanData(literal);

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

        /// <summary>
        /// Returns the list of necessary NQuads of a Relation Node.
        /// </summary>
        /// <param name="subject">The uid of the subject of the Relation.</param>
        /// <param name="predicate">The predicate of the Relation.</param>
        /// <param name="obj">The uid of the object of the Relation.</param>
        /// <param name="bidirectional">Whether the relation is bidirectional or not.</param>
        /// <param name="createdAt">The timestamp of the creation of the node.</param>
        /// <param name="ontology">The identifier of the Ontology of the nodes.</param>
        /// <param name="prefix">The prefix of the Relation.</param>
        /// <returns>Retuns the list of NQuads of the Relation Node and  the necessary ones to relate the Thing Nodes.</returns>
        private List<string> GetNQuadThingRelationTriples(string subject, string predicate, string obj, bool bidirectional, string createdAt, string ontology, string prefix)
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

        /// <summary>
        /// The list of necessary NQuads of a Thing Node.
        /// </summary>
        /// <param name="uid">The uid of the Thing.</param>
        /// <param name="thingId">the identifier of the thing.</param>
        /// <param name="createdAt">The timestamp of the creation of the node.</param>
        /// <param name="ontology">The identifier of the Ontology of the nodes.</param>
        /// <param name="prefix">The prefix of the Thing.</param>
        /// <returns>Returns the list of NQuads of the Thing Node.</returns>
        private List<string> GetNQuadThingNodeTriples(string uid, string thingId, string createdAt, string ontology, string prefix)
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

        /// <summary>
        /// The list of necessary NQuads of the Ontology Node.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="createdAt">The timestamp of the creation of the node.</param>
        /// <returns></returns>
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

        /// <summary>
        /// The list of necessary NQuads of the Namespace Node depending on its id, prefix and uri.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <param name="createdAt">The timestamp of the creation of the node.</param>
        /// <param name="prefix">The prefix of the namespace.</param>
        /// <param name="uri">The uri of the namespace.</param>
        /// <returns>Returns the list of the NQuads  of the Namespace Node.</returns>
        private List<string> GetNQuadsNamespaceTriples(string id, string createdAt, string prefix, string uri)
        {
            var nquads = new List<string>();

            /*
            namespaceId ===========> ontologyId:namespace
            prefix ================> prefix of the type "rdf:"
            uri ===================> uri that replaces the prefix (http://example.org/)
            Namespace.name ========> ontologyId:namespace
            */

            var namespace_uid = GetNamespaceUid(id, prefix);
            nquads.Add($"{namespace_uid} <dgraph.type> \"Namespace\" .");
            nquads.Add($"{namespace_uid} <Namespace.createdAt> \"{createdAt}\" .");
            nquads.Add($"{namespace_uid} <namespaceId> \"{id}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <Namespace.name> \"{id}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <prefix> \"{prefix}\" .");
            nquads.Add($"{namespace_uid} <uri> \"{uri}\" .");

            //link namespace to the ontology
            nquads.Add($"_:{id} <namespace> {namespace_uid} .");

            return nquads;
        }

        /// <summary>
        /// The list of necessary NQuads of the Shape Graph main Node depending of its identifier.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph node.</param>
        /// <param name="createdAt">The timestamp of the creation of the node.</param>
        /// <returns>Returns the list of NQuads of the Shape Graph.</returns>
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

        /// <summary>
        /// The list of necessary NQuads of a Node Shape and its default Property. 
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="shapeNodeId">The identifier of the Node Shape.</param>
        /// <param name="nodeUid">The uid of the Node Shape.</param>
        /// <param name="prefix">The prefix of the Node Shape.</param>
        /// <param name="createdAt">The timestamp of the creation of the node.</param>
        /// <param name="isSubShape">Whether the Node Shape is part of another Node SHape or not.</param>
        /// <returns>Returns the list of NQuads of the NodeShape, its default Property and the link between the Node Shape and the Shape graph if necessary.</returns>
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

        /// <summary>
        /// The list of necessary NQuads of a Reference Node depending on its identifier, prefix and identifier of its Shape Graph.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="prefix">The prefix of the Reference node.</param>
        /// <param name="referenceId">The identifier of the Reference node.</param>
        /// <returns>Returns the list of NQuads of the Reference node.</returns>
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

        /// <summary>
        /// Returns the list of NQuads of a Value node of a parent node depending on its index and source node.
        /// </summary>
        /// <param name="uid">The uid of the parent node</param>
        /// <param name="index">The index of child nodes of the parent node.</param>
        /// <param name="node">The source node of the Value node.</param>
        /// <returns>Returns the list NQuads of the Value Node.</returns>
        private List<string> GetNQuadsValueTriples(string uid, int index, INode node)
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
            var valueUid = GetValueUid(uid, index);
            var nquads = new List<string>
            {
                $"{valueUid} <dgraph.type> \"Value\" .",
                $"{valueUid} <valueId> \"{dataType}valueFor{uid.Replace("_:", "")}\" .",
                $"{valueUid} <Value.type> \"{dataType}\" .",
                $"{valueUid} <value> \"{value}\" .",
            };

            return nquads;
        }

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
        private List<string> SortNodes(string shapeId, string subjectUid, string prefixPredicate, string predicate, string type, int index, Triple triple, IGraph graph)
        {
            var nquads = new List<string>();

            if (triple.Object.NodeType.Equals(NodeType.Literal) || triple.Object.NodeType.Equals(NodeType.GraphLiteral) || predicate.Equals("datatype") || predicate.Equals("nodeKind"))
            {
                nquads.AddRange(GetNQuadsValueTriples(subjectUid, index, triple.Object));
                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {GetValueUid(subjectUid, index)} .");
            }
            else if(triple.Object.NodeType.Equals(NodeType.Uri))
            {
                var (refPrefix, refId) = _converterService.GetLocalName(triple.Object, graph, shapeId);
                nquads.AddRange(GetNQuadsReferenceTriples(shapeId, refPrefix, refId));
                nquads.Add($"{subjectUid} <{(type.Equals("Generic") ? "GenericConstraint.value" : predicate)}> {GetReferenceUid(shapeId, refId, refPrefix)} .");
            }
            else
            {
                //Blank node
                List<Triple> source;
                if(_converterService.IsListHead(graph, triple.Object))
                {
                    source = new List<Triple>();
                    foreach(INode node in _converterService.ReadStrictList(graph, triple.Object))
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
                                var (childPrefix, childPredicate) = _converterService.GetLocalName(triple.Predicate, graph, shapeId);
                                nquads.AddRange(GenerateShapeTriples(shapeId, subjectUid, childPrefix, type.Equals("Generic") ? "GenericConstraint.value" : childPredicate, index, t, graph));
                            }
                        }
                        else
                        {
                            nquads.AddRange(SortNodes(shapeId, isConstraintUid(subjectUid) ? subjectUid : GetConstraintUid(subjectUid, prefixPredicate, predicate), prefixPredicate, predicate, type, index, t, graph));
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
        private List<string> MainLoop(string shapeId, string nodeUid, INode node, IGraph graph, bool isSubShape)
        {   
            var nquads = new List<string>();


            List<INode> nodes = new List<INode>();
            if (node.NodeType.Equals(NodeType.Blank) && _converterService.IsListHead(graph, node))
            {
                nodes = _converterService.ReadStrictList(graph, node);
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

        /// <summary>
        /// Redirects the algorithm detecting if the object node is a Shape Property or Shape Constraint.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="subjectUid">The uid of the subject node of the triple.</param>
        /// <param name="parentPrefix">The prefix of the parent predicate.</param>
        /// <param name="parentPredicate">The predicate of the parent node.</param>
        /// <param name="index">The index of child nodes of the parent node.</param>
        /// <param name="triple">The original parsed triple object.</param>
        /// <param name="graph">The original parsed graph object.</param>
        /// <returns>Returns the list of NQuads of the following node depending of its kind.</returns>
        private List<string> GenerateShapeTriples(string shapeId, string subjectUid, string parentPrefix, string parentPredicate, int index, Triple triple, IGraph graph)
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
                uid = GetPropertyUid(subjectUid, index);
                nquads.Add($"{uid} <ShapeProperty.prefix> {GetNamespaceUid(shapeId, prefixPredicate)} .");
                nquads.Add($"{uid} <dgraph.type> \"ShapeProperty\" .");

                if (predicate.Equals("message") && triple.Object.NodeType.Equals(NodeType.Literal))
                {
                    nquads.Add($"{uid} <description> \"{triple.Object.ToString()}\" .");
                }
                else
                {
                    nquads.Add($"{uid} <description> \"No Description\" .");
                    foreach(Triple t in graph.Triples.Where(tr => tr.Subject.Equals(triple.Object)).Distinct().ToList())
                    {
                        nquads.AddRange(GenerateShapeTriples(shapeId, uid, prefixPredicate, "constraints", index, t, graph));
                    }
                }
                index++;
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
                        uid = GetConstraintUid(subjectUid, prefixPredicate, $"{predicate}{index}");
                        
                        nquads.Add($"{uid} <constraintId> \"{predicate}\" .");
                        nquads.Add($"{uid} <ShapeConstraint.prefix> {GetNamespaceUid(shapeId, prefixPredicate)} .");
                        string type = CategorizeConstraint(predicate);
                        nquads.Add($"{uid} <dgraph.type> \"{type}Constraint\" .");
                        nquads.AddRange(SortNodes(shapeId, uid, prefixPredicate, predicate, type, index+1, triple, graph));
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

        /// <summary>
        /// The full list of NQuads of an Ontology based on the TTL structure provided.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="ontologyFile">The TTL file of the Ontology.</param>
        /// <returns>Returns the list of NQuads of the Ontology and its nodes.</returns>
        public List<string> GetFullOntologyNQuadsFromFile(string ontologyId, IFormFile ontologyFile)
        {

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
                    .Where(s => s.NodeType == VDS.RDF.NodeType.Uri || (s.NodeType == VDS.RDF.NodeType.Blank && _converterService.IsRelevantBlankNode(graph, s)))
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

                    string uid = _converterService.GetUid(node, graph);
                    var (prefix, thingId) = _converterService.GetLocalName(node, graph, ontologyId); //here it takes care of the no prefix fallback
                    thingId ??= "thing" + Guid.NewGuid();
                    nquads.AddRange(GetNQuadThingNodeTriples(uid, thingId, createdAt, ontologyId, prefix));

                }

                var ignoredPredicates = new List<string> { "domain", "range", "inverseOf", "uid" };

                foreach (Triple triple in graph.Triples.Distinct())
                {
                    //Get the subject, predicate and object of the triple
                    string subject = _converterService.GetUid(triple.Subject, graph); //_:uid
                    var (prefixPredicate, predicate) = _converterService.GetLocalName(triple.Predicate, graph, "");  //uri
                    predicate ??= "predicate" + Guid.NewGuid();
                    //as this includes the original type and uid of the ontology, we exclude them so as not to duplicate the existing ones
                    if (!ignoredPredicates.Contains(predicate))
                    {
                        //Check if the object is a literal (Attribute) or the uid to another node (Relation)
                        if (predicate.Equals("type") || predicate.Equals("a"))
                        {
                            //Support for "type" and "a" predicate
                            //create or find a thing whose thingId is the name of the type, and with this subject, relate it to the type Thing through hasType relation
                            var (typePrefix, typeOfNode) = _converterService.GetLocalName(triple.Object, graph, ontologyId);
                            // var typeOfNode = ((ILiteralNode)triple.Object).ToString();
                            string match = nquads.FirstOrDefault(nquad => nquad.Contains($"<thingId> {typeOfNode}")) ?? ""; //null manegement ahead
                            string typeUid = "";
                            //find out if it is already a Thing
                            if (match is not null && !string.IsNullOrWhiteSpace(match))
                            {
                                //the Thing already exists
                                //first we get the uid of the Type Thing
                                typeUid = match!.Split('<')[0];
                            }
                            else
                            {
                                //the Thing doesn't exist, we have to create it
                                //typeOfNode is the thingId, but we need the prefix too
                                typeUid = $"_:typeThing{typeOfNode}";
                                nquads.AddRange(GetNQuadThingNodeTriples($"_:typeThing{typeOfNode}", typeOfNode, createdAt, ontologyId, typePrefix));
                            }
                            if (typeUid.Equals(""))
                            {
                                //typeUid has not been instanciated correctly, something has gone wrong
                                throw new Exception($"Type Uid of ${subject} node not instanciated");
                            }

                            //instanciate hasType relation
                            nquads.Add($"{subject} <hasType> {typeUid} .");
                        }
                        else if (triple.Object.NodeType == VDS.RDF.NodeType.Literal)
                        {
                            //Attribute of a node
                            var literal = (ILiteralNode)triple.Object;
                            nquads.AddRange(GetNQuadThingAttributeTriples(subject, predicate, literal, ontologyId, prefixPredicate));
                        }
                        else
                        {
                            //Relation between 2 nodes
                            string obj = _converterService.GetUid(triple.Object, graph);
                            //check if the relation is bidirectional or not
                            //if yes, check if the relation object has already been added to the nquads
                            var (bid, existent) = _converterService.isRelationBidirectional(graph, triple, subject, obj, predicate, nquads);
                            if (!existent)
                                nquads.AddRange(GetNQuadThingRelationTriples(subject, predicate, obj, bid, createdAt, ontologyId, prefixPredicate));
                        }
                    }

                }
            }
            catch (Exception)
            {
                throw;
            }

            return nquads;
        }

        /// <summary>
        /// The full list of NQuads of a Shape Graph based on the TTL strcuture provided.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <param name="shapeFile">The TTL file of the Shape Graph.</param>
        /// <returns>Returns the list of all NQuads of the Shape Graph and its nodes.</returns>
        public List<string> GetFullShapeGraphNquadsFromFile(string shapeId, IFormFile shapeFile)
        {
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
                //Add the default prefix pref{shapeId}
                nquads.AddRange(GetNQuadsNamespaceTriples(shapeId, createdAt, $"pref{shapeId}", $"http://example.org/shapeGraph/{shapeId}"));

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
            catch (Exception)
            {
                throw;
            }
            return nquads;
        }
    }
}