using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using System.Xml;
using Dgraph4Net;
using HtmlAgilityPack;
using J2N;
using J2N.Text;
using Twins.Builders;
using Twins.Models;
using Twins.Services;
using VDS.Common.Collections.Enumerations;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
using static Twins.Models.CustomType;


namespace OpenTwinsV2.Twins.Services
{
    /// <summary>
    /// Handles Generation of NQuads for Twins Project  
    /// </summary>
    public static class NQuadsService
    {
        /// <summary>
        /// Extracts the uid of the node provided.
        /// </summary>
        /// <param name="node">The node object.</param>
        /// <param name="graph">The graph object that contains the node.</param>
        /// <param name="prefix">OPTIONAL. The prefix to add in the uid.</param>
        /// <returns>
        /// Returns the uid of the node in the form of "_:{local}.
        /// </returns>
        public static string GetUid(VDS.RDF.INode node, IGraph graph, string? prefix = null)
        {
            //get the uid omiting all prefixes
            var (_, local) = FormatService.GetLocalName(node, graph, "");
            local ??= "nameless" + Guid.NewGuid();
            return $"_:{$"{FormatService.SanitizeTypeAndUIDValues(prefix ?? "")}_"}{FormatService.SanitizeTypeAndUIDValues(local)}";
        }

    /// <summary>
    /// Returns the proper uid for a namespace depending on the prefix and parent's identifier.
    /// </summary>
    /// <param name="id">The identifier of the parent.</param>
    /// <param name="prefix">The prefix of the namespace.</param>
    /// <returns>Returns the uid of the namespace in the form _:{id}namespace_{prefix}.</returns>
        private static string GetNamespaceUid(string id, string prefix)
        {
            return $"_:{id}namespace_{FormatService.SanitizeTypeAndUIDValues(prefix.ToLowerInvariant())}";
        }

        /// <summary>
        /// Returns the proper uid for a Shape Reference depending on the reference, prefix and aprent's identifier.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <param name="reference">The name of the reference.</param>
        /// <param name="prefix">The prefix of the reference.</param>
        /// <returns>Returns the uid of the reference in the form _:{id}reference_{prefix}_{reference}.</returns>
        public static string GetReferenceUid(string id, string reference, string prefix)
        {
            return $"_:{FormatService.SanitizeTypeAndUIDValues(id)}reference_{FormatService.SanitizeTypeAndUIDValues(prefix)}_{FormatService.SanitizeTypeAndUIDValues(reference)}";
        }

        /// <summary>
        /// Returns the uid of the default Property of a node depending of its identifier.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <returns>Returns the uid of the default Property in the form _:{id}defaultproperty.</returns>
        private static string GetDefaultPropertyUid(string id)
        {
            return $"_:{FormatService.SanitizeTypeAndUIDValues(id)}defaultproperty";
        }

        /// <summary>
        /// Returns the uid of the default Property of a node depending of its identifier and its name.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <param name="shape">The name of the shape.</param>
        /// <returns>Returns the uid of the default Property in the form _:{id}_{shape}defaultproperty.</returns>
        private static string GetDefaultPropertyUid(string id, string shape)
        {
            return $"_:{FormatService.SanitizeTypeAndUIDValues(id)}_{shape.Replace("_:", "")}defaultproperty";
        }

        /// <summary>
        /// Returns the uid of a Shape Property depending on the uid of the uid of its parent and the property count.
        /// </summary>
        /// <param name="parentUid">The uid of the parent.</param>
        /// <param name="count">The index of node child of the parent.</param>
        /// <returns>Return the uid of the Shape Property in the form {parentUid}_property{count}.</returns>
        private static string GetPropertyUid(string parentUid, int count)
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
        public static string GetConstraintUid(string parentUid, string prefix, string constraint)
        {
            return $"{parentUid}_{FormatService.SanitizeTypeAndUIDValues(prefix)}_{constraint}constraint";
        }

        /// <summary>
        /// Returns the uid of a Shape Value depending on the uid of its parent and the property count.
        /// </summary>
        /// <param name="parentUid">The uid of the parent.</param>
        /// <param name="nProperty">The index of Property of the parent.</param>
        /// <returns>The uid of the Shape Value in the form {parentUid}_value{nProperty}</returns>
        public static string GetValueUid(string parentUid, int nProperty)
        {
            return $"{parentUid}_value{nProperty}";
        }

        /// <summary>
        /// Returns the uid of the type and adds it into the list of nquads if it's not already yet..
        /// </summary>
        /// <param name="typePrefix">The prefix of the type.</param>
        /// <param name="typeOfNode">The name of the type.</param>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="nquads">The list of nquads.</param>
        /// <returns>The uid of the type.</returns>
        public static string GetTypeUid(string typePrefix, string typeOfNode, string ontologyId, ICollection<string> nquads)
        {
            string match = nquads.FirstOrDefault(nquad => nquad.Contains($"<thingId> \"{ontologyId}:{typeOfNode}\"")) ?? ""; //null manegement ahead
            string typeUid = "";
            //find out if it is already a Thing
            if (match is not null && !string.IsNullOrWhiteSpace(match))
                //the Thing already exists
                //first we get the uid of the Type Thing
                typeUid = match!.Split('<')[0];
            else
            {
                //the Thing doesn't exist, we have to create it
                //typeOfNode is the thingId, but we need the prefix too
                typeUid = $"_:{ontologyId}:{typeOfNode}";
                AddNQuadThingNodeTriples(typeUid, typeOfNode, ontologyId, typePrefix, nquads);
            } 
            return typeUid;
        }

        /// <summary>
        /// Returns whether a uid belongs to a Shape Property Node or not.
        /// </summary>
        /// <param name="uid">The uid of the node that is being checked.</param>
        /// <returns>
        /// Returns true if the uid's node is a Shape Property.<br/>
        /// Returns false if the uid's node is not a Shape Property.
        /// </returns>
        private static bool IsPropertyUid(string uid)
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
        public static bool isConstraintUid(string uid){
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
        /// Adds in the list of necessary NQuads of a Thing's Attribute Node.
        /// </summary>
        /// <param name="subject">The uid of the Attribute's parent node.</param>
        /// <param name="predicate">The predicate of the triple representing the Attribute.</param>
        /// <param name="literalType">The name of the type of the literal value.</param>
        /// <param name="literalValue">The value of the literal in string format.</param>
        /// <param name="ontology">The identifier of the Ontology of the nodes.</param>
        /// <param name="prefix">The prefix of the Attribute.</param>
        /// <param name="nquads">The list of nquads.</param>
        public static void AddNQuadThingAttributeTriples(string subject, string predicate, string literalType, string? literalValue, string ontology, string prefix, ICollection<string> nquads)
        {

            /*
            type Attribute {
                Attribute.key =============> Generated key (it will overrided when uploaded to DGraph)
                Attribute.type ============> Type of the attribute value (DGraph format)
                Attribute.value ===========> Value of the attribute casted to string
            }
            */

            var namespace_uid = $"_:{ontology}namespace_{prefix.ToLowerInvariant()}";

            string attribute_uid = $"_:att_{predicate}_{Guid.NewGuid()}";
            nquads.Add($"{attribute_uid} <dgraph.type> \"Attribute\" .");
            nquads.Add($"{attribute_uid} <Attribute.key> \"{predicate}\" .");
            nquads.Add($"{attribute_uid} <Attribute.type> \"{literalType}\" .");
            nquads.Add($"{attribute_uid} <Attribute.prefix> {namespace_uid} .");
            if(!string.IsNullOrWhiteSpace(literalValue))
                nquads.Add($"{attribute_uid} <Attribute.value> \"{literalValue}\" .");

            //Link the node to the attribute
            nquads.Add($"{subject} <hasAttribute> {attribute_uid} .");
        }

        /// <summary>
        /// Returns the list of necessary NQuads of a Relation Node.
        /// </summary>
        /// <param name="subject">The uid of the subject of the Relation.</param>
        /// <param name="predicate">The predicate of the Relation.</param>
        /// <param name="obj">The uid of the object of the Relation.</param>
        /// <param name="bidirectional">Whether the relation is bidirectional or not.</param>
        /// <param name="ontology">The identifier of the Ontology of the nodes.</param>
        /// <param name="prefix">The prefix of the Relation.</param>
        /// <param name="nquads">The list of NQuads.</param>
        public static void AddNQuadThingRelationTriples(string subject, string predicate, string obj, bool bidirectional, string ontology, string prefix, ICollection<string> nquads)
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

            var namespace_uid = $"_:{ontology}namespace_{prefix.ToLowerInvariant()}";
            //Relation node
            string relation_uid = $"_:rel_{predicate}_{Guid.NewGuid()}";
            nquads.Add($"{relation_uid} <dgraph.type> \"Relation\" .");
            nquads.Add($"{relation_uid} <Relation.name> \"{predicate}\" .");
            nquads.Add($"{relation_uid} <Relation.createdAt> \"{DateTime.UtcNow:O}\" .");
            nquads.Add($"{relation_uid} <Relation.prefix> {namespace_uid} .");
            nquads.Add($"{relation_uid} <relatedTo> {obj} ."); //always
            nquads.Add($"{relation_uid} <{(bidirectional ? "relatedTo" : "relatedFrom")}> {subject} .");
        }

        /// <summary>
        /// Adds the necessary NQuads of a Thing Node.
        /// </summary>
        /// <param name="uid">The uid of the Thing.</param>
        /// <param name="thingId">the identifier of the thing.</param>
        /// <param name="ontology">The identifier of the Ontology of the nodes.</param>
        /// <param name="prefix">The prefix of the Thing.</param>
        /// <param name="nquads">The list of NQuads.</param>
        /// <returns></returns>
        public static void AddNQuadThingNodeTriples(string uid, string thingId, string ontology, string prefix, ICollection<string> nquads)
        {
            var ontology_uid = $"_:{ontology}";
            var namespace_uid = $"_:{ontology}namespace_{prefix.ToLowerInvariant()}";

            nquads.Add($"{uid} <dgraph.type> \"Thing\" .");
            nquads.Add($"{uid} <thingId> \"{ontology}:{thingId}\" .");
            nquads.Add($"{uid} <name> \"{thingId}\" .");
            nquads.Add($"{uid} <createdAt> \"{DateTime.UtcNow:O}\" .");
            nquads.Add($"{uid} <Thing.prefix> {namespace_uid} .");

            //Associate the Thing node with the Ontology node
            nquads.Add($"{ontology_uid} <hasThing> {uid} .");
        }


        /// <summary>
        /// Adds to the list of necessary NQuads of the Ontology Node.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="nquads">The list of NQuads.</param>
        public static void AddNQuadsOntologyTriples(string ontologyId, ICollection<string> nquads)
        {
            var ontology_uid = $"_:{ontologyId}";
            nquads.Add($"{ontology_uid} <dgraph.type> \"Ontology\" .");
            nquads.Add($"{ontology_uid} <createdAt> \"{DateTime.UtcNow:O}\" .");
            nquads.Add($"{ontology_uid} <ontologyId> \"{ontologyId}\" .");
            nquads.Add($"{ontology_uid} <Ontology.name> \"{ontologyId}\" .");
            nquads.Add($"{ontology_uid} <Ontology.name> \"{ontologyId}\" .");
        }

        /// <summary>
        /// Adds to the list the NQuads of the prefixes.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="graph">The RDF TTL graph.</param>
        /// <param name="ignoredPrefixes">The list of the prefixes to ignore.</param>
        /// <param name="nquads">The list of nquads.</param>
        public static void AddPrefixNQuadsTriples(string ontologyId, IGraph graph, ICollection<string> ignoredPrefixes, ICollection<string> nquads)
        {
            
            var prefixes = graph.NamespaceMap.Prefixes
                .Where(p => !ignoredPrefixes.Contains(p))
                .Select(p => new
                {
                    Prefix = p,
                    NamespaceUri = graph.NamespaceMap.GetNamespaceUri(p).ToString()
                });

            foreach(var prefix in prefixes)
                AddNQuadsNamespaceTriples(ontologyId, prefix.Prefix, prefix.NamespaceUri, nquads);
            
            AddNQuadsNamespaceTriples(ontologyId, $"pref{ontologyId}", $"http://example.org/ontology/{ontologyId}", nquads);

        }

        /// <summary>
        /// Add to the list of necessary NQuads of the Namespace Node depending on its id, prefix and uri.
        /// </summary>
        /// <param name="id">The identifier of the parent.</param>
        /// <param name="prefix">The prefix of the namespace.</param>
        /// <param name="uri">The uri of the namespace.</param>
        /// <param name="nquads">The list of NQuads.</param>
        public static void AddNQuadsNamespaceTriples(string id, string prefix, string uri, ICollection<string> nquads)
        {
            /*
            namespaceId ===========> ontologyId:namespace
            prefix ================> prefix of the type "rdf:"
            uri ===================> uri that replaces the prefix (http://example.org/)
            Namespace.name ========> ontologyId:namespace
            */

            var namespace_uid = GetNamespaceUid(id, prefix);
            nquads.Add($"{namespace_uid} <dgraph.type> \"Namespace\" .");
            nquads.Add($"{namespace_uid} <Namespace.createdAt> \"{DateTime.UtcNow.ToString("O")}\" .");
            nquads.Add($"{namespace_uid} <namespaceId> \"{id}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <Namespace.name> \"{id}:namespace:{prefix}\" .");
            nquads.Add($"{namespace_uid} <prefix> \"{prefix}\" .");
            nquads.Add($"{namespace_uid} <uri> \"{uri}\" .");

            //link namespace to the ontology
            nquads.Add($"_:{id} <namespace> {namespace_uid} .");
        }

        /// <summary>
        /// The list of necessary NQuads of the Shape Graph main Node depending of its identifier.
        /// </summary>
        /// <param name="shapeId">The identifier of the Shape Graph node.</param>
        /// <param name="createdAt">The timestamp of the creation of the node.</param>
        /// <returns>Returns the list of NQuads of the Shape Graph.</returns>
        public static List<string> GetNQuadsShapeGraphTriples(string shapeId, string createdAt)
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
        public static List<string> GetNQuadsNodeShapeTriples(string shapeId, string shapeNodeId, string nodeUid, string prefix, string createdAt, bool isSubShape)
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
        public static List<string> GetNQuadsReferenceTriples(string shapeId, string prefix, string referenceId)
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
        public static List<string> GetNQuadsValueTriples(string uid, int index, INode node)
        {
            string dataType = "string";
            string value="";

            if (node.NodeType.Equals(NodeType.Literal))
                (dataType, value) = FormatService.GetLiteralCleanData((ILiteralNode)node);
            else
                (dataType, value) = FormatService.GetLiteralCleanData(node.AsValuedNode().ToSafeString());

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
        public static List<string> GenerateShapeTriples(string shapeId, string subjectUid, string parentPrefix, string parentPredicate, int index, Triple triple, IGraph graph)
        {
            /*
            subjectUid <parentPredicate> objUid
            objUid <predicate> nodeUid
            */

            var nquads = new List<string>();
            string prefixPredicate, predicate;
            if (!triple.Predicate.NodeType.Equals(NodeType.Literal))
            {
                (prefixPredicate, predicate) = FormatService.GetLocalName(triple.Predicate, graph, "");  //uri
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
                            var (refPrefix, refId) = FormatService.GetLocalName(triple.Object, graph, shapeId);
                            nquads.AddRange(GetNQuadsReferenceTriples(shapeId, refPrefix, refId));
                            nquads.Add($"{subjectUid} <path> {GetReferenceUid(shapeId, refId, refPrefix)} .");
                            break;
                        }
                        //constraint
                        uid = GetConstraintUid(subjectUid, prefixPredicate, $"{predicate}{index}");
                        
                        nquads.Add($"{uid} <constraintId> \"{predicate}\" .");
                        nquads.Add($"{uid} <ShapeConstraint.prefix> {GetNamespaceUid(shapeId, prefixPredicate)} .");
                        string type = ImportService.CategorizeConstraint(predicate);
                        nquads.Add($"{uid} <dgraph.type> \"{type}Constraint\" .");
                        nquads.AddRange(ImportService.SortNodes(shapeId, uid, prefixPredicate, predicate, type, index+1, triple, graph));
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

    }
}