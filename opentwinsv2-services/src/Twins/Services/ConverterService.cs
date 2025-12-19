/*
Class that reunites all export functions common to Ontologies and Twins controllers.
*/

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Api;
using Json.More;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using OpenTwinsV2.Shared.Constants;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Query.Expressions.Functions.XPath.Cast;

namespace OpenTwinsV2.Twins.Services
{
    public class ConverterService
    {
        //TODO constructor
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;
        private const string ActorType = Actors.ThingActor;
        public ConverterService(DGraphService dgraphService, ThingsService thingsService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
        }

        public string SanitizeTypeAndUIDValues(string uri)
        {
            //Check which character is last
            char[] separators = { '#', '/', '&', ':' };

            // Remove trailing separator if present at the end
            while (uri.Length > 0 && separators.Contains(uri.Last()))
            {
                //Delete illegal characters at the end
                uri = uri.Substring(0, uri.Length - 1);
            }

            //in case the whole uri were illegal characters (unlikely but possible)
            if (uri.Length == 0)
            {
                return "twin"; //for example
            }

            // Find last separator after removing trailing char
            int indx = uri.LastIndexOfAny(separators);

            //return the substring or the whole uri in case none of the characters are present
            return (indx >= 0 && indx < uri.Length - 1) ? uri.Substring(indx + 1) : uri;
        }

        public (string Prefix, string LocalName) GetLocalName(string nodeUri, string parentId)
        {

            string prefix = null;
            string localName = null;
            //get the name right after the prefix
            var parts = nodeUri.Split(':');
            if (parts.Length >= 2)
            {
                prefix = parts[0];
                localName = parts[1];
            }

            if (prefix is null || prefix.Length == 0)
            {
                //Get the last part of the URI
                prefix = $"pref{parentId}";
            }
            if (localName is null || localName.Length == 0)
            {
                localName = SanitizeTypeAndUIDValues(nodeUri);
            }
            return (prefix, localName);
        }

        public (string Prefix, string LocalName) GetLocalName(VDS.RDF.INode node, IGraph graph, string parentId)
        {
            if (node is UriNode uriNode)
            {
                string qname;
                string prefix;
                string localName;
                if (graph.NamespaceMap.ReduceToQName(uriNode.Uri.ToString(), out qname))
                {
                    (prefix, localName) = GetLocalName(qname, parentId);
                }
                else
                {
                    (prefix, localName) = GetLocalName(uriNode.Uri.ToString(), parentId);
                }
                return (prefix, localName);
            }
            return ($"pref{parentId}", $"{(node.NodeType.Equals(NodeType.Blank) ? "blank_" : "")}{ node.ToString()}");
        }

        public string GetUid(VDS.RDF.INode node, IGraph graph)
        {
            //get the uid omiting all prefixes
            var (_, local) = GetLocalName(node, graph, "");
            local ??= "nameless" + Guid.NewGuid();
            return $"_:{local}";
        }
        
        public string SanitizeAttributeValue(string value)
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

        public (string datatype, string value) GetLiteralCleanData(ILiteralNode literal)
        {
            string value = SanitizeAttributeValue(literal.Value);
            string dataType = literal.DataType?.ToString() ?? "string";
            dataType = SanitizeTypeAndUIDValues(dataType) ?? "string";

            return (dataType, value);
        }

        public (string datatype, string value) GetLiteralCleanData(string literalString)
        {
            if (!literalString.Contains("^^"))
                return ("string", literalString);

            var parts = literalString.Split("^^");
            var value = parts[0];
            var (_, dataType) = GetLocalName(SanitizeTypeAndUIDValues(parts[1]), "");
            return (dataType, value);
        }

        private JsonNode checkPrefixes(JsonNode node, Dictionary<string, string> ns, string defaultPrefix, string defaultUri, string pType = "", string pName = "")
        {
            if (node is JsonObject obj)
            {
                var pref = defaultPrefix;
                var uri = defaultUri;
                var type = string.IsNullOrWhiteSpace(pType) ? node["Relation.name"] is not null ? "Relation" : (node["Attribute.key"] is not null ? "Attribute" : "Thing") : pType;
                if (obj.TryGetPropertyValue($"{type}.prefix", out var existing))
                {
                    if (existing is not null)
                    {
                        pref = existing["prefix"]?.GetValue<string>() ?? pref;
                        uri = existing["uri"]?.GetValue<string>() ?? uri;
                    }
                }
                else
                {
                    //it doesn't have one
                    string propertyName = string.IsNullOrWhiteSpace(pName) ? type.Equals("Thing") ? "thingId" : (type.Equals("Relation") ? "Relation.name" : "Attribute.key") : pName;
                    string newName = null;
                    if (obj.TryGetPropertyValue(propertyName, out var name) && name is not null)
                        (pref, newName) = GetLocalName(name.GetValue<string>(), defaultPrefix);
                    

                    if (pref.Equals("otv2"))
                    {
                        uri = "http://opentwinsv2.org/"; //otv2 placeholder uri!!!!!!!!!
                        if (newName is not null)
                            obj[propertyName] = newName;
                    }
                    else
                    {
                        pref = defaultPrefix;
                    }
                        
                    //first, check if it has the otv2 prefix
                    //if not, we create the attribute with the info from the default's context
                    obj[$"{type}.prefix"] = new JsonObject //sample context prefix node
                    {
                        ["prefix"] = pref.Equals("otv2") ? pref : defaultPrefix,
                        ["uri"] = pref.Equals("otv2") ? uri : defaultUri 
                    };
                }
                if (!ns.ContainsKey(pref))
                {
                    ns.Add(pref, uri);
                }

            }
            return node.DeepClone();
        }

        private async Task<string?> GetTwinRawJson(string twinId)
        {
            var rawJson = await _dgraphService.GetThingsInTwinNQUADSAsync(twinId);

            if (string.IsNullOrWhiteSpace(rawJson))
                return null;
            return string.IsNullOrWhiteSpace(rawJson) ? null : rawJson;
        }

        private async Task<Dictionary<string, JsonElement>?> GetTwinDict(string twinId)
        {
            var rawJson = await GetTwinRawJson(twinId);
            if (rawJson is null)
                return null;

            Console.WriteLine(rawJson);

            using var doc = JsonDocument.Parse(rawJson);
            var json = doc.RootElement;

            var thingIds = json.GetProperty("things").EnumerateArray().SelectMany(t => t.GetProperty("~twins").EnumerateArray())
                .Where(t => t.TryGetProperty("thingId", out var id) && id.ValueKind == JsonValueKind.String)
                .Select(t => t.GetProperty("thingId").GetString()!).Distinct().ToList();

            if (thingIds is null)
                return null;

            var states = await _thingsService.GetThingsStatesAsync(thingIds);
            return states;
        }
        
        private async Task<JsonObject?> GetTwinJson(string twinId)
        {
            var dict = await GetTwinDict(twinId);
            return dict is null ? null : dict.AsJsonElement().AsNode()!.AsObject();
        }

        public async Task<JsonObject?> getJsonWithNamespace(string id, JsonElement? ns)
        {
            var finalNode = new JsonObject
            {
                [ns is not null ? "ontologyId" : "twinId"] = id,
                ["namespace"] = ns is null ? new JsonArray() : JsonNode.Parse(ns.Value.GetProperty("namespace").GetRawText()),
                ["things"] = new JsonArray()
            };
            var idSanitized = SanitizeTypeAndUIDValues(id);
            var defaultPrefix = $"pref{idSanitized}";
            var defaultUri = $"http://example.org/twin/{idSanitized}/";
            var nsDic = new Dictionary<string, string>
            {
                { defaultPrefix, defaultUri }
            };

            var things = ns is null ? await _dgraphService.GetThingsInTwinAsync(id) : await _dgraphService.GetThingsInOntologyAsync(id);

            var thingStates = new JsonObject();
            if(ns is null)
            {
                //load thing states if it's a twin, better to load them all at once
                thingStates = await GetTwinJson(id);
            }
            foreach (var thingKey in things)
            {
                if (thingKey.TryGetProperty("thingId", out var thingId))
                {
                    var thingInfo = ns is null ? await _dgraphService.GetThingInTwinByIdForJsonAsync(id, thingId.ToString()) : await _dgraphService.GetThingInOntologyByIdAsync(id, thingId.ToString());
                    if (thingInfo.HasValue)
                    {
                        string raw = thingInfo.Value.GetRawText();
                        var thing = JsonNode.Parse(raw);
                        if (ns is null)
                        {
                            thing = checkPrefixes(thing, nsDic, defaultPrefix, defaultUri);
                            
                            //ATTRIBUTES
                            JsonArray attrs = new JsonArray();
                            JsonNode state = new JsonObject();
                            if(thingStates is not null)
                            {
                                //load states of the thing
                                thingStates.TryGetPropertyValue(thingId.ToString(), out state);
                            }
                            if(thing["hasAttribute"] is not null)
                            {
                                foreach (var attr in thing["hasAttribute"]!.AsArray())
                                {
                                    var newAttr = checkPrefixes(attr, nsDic, defaultPrefix, defaultUri).AsObject();
                                    //TODO add states
                                    if (state is not null && state is JsonObject stateObj && stateObj.Count > 0 && attr.AsObject().TryGetPropertyValue("Attribute.key", out var attrKey) && stateObj.TryGetPropertyValue(attrKey!.GetValue<string>(), out var stateInfo))
                                    {
                                        //it only enters here if state is not null, it has something and has something on the attribute we are in
                                        if (stateInfo!.AsObject().TryGetPropertyValue("value", out var stateValue))
                                            newAttr.Add("value", stateValue is null ? null : stateValue.ToString());
                                        if (stateInfo!.AsObject().TryGetPropertyValue("lastUpdate", out var stateUpdate))
                                            newAttr.Add("lastUpdate", stateUpdate);

                                        //change Attribute.value into Attribute.default
                                        if (attr.AsObject().TryGetPropertyValue("Attribute.value", out var attrValue))
                                        {
                                            newAttr.Remove("Attribute.value");
                                            newAttr.Add("Attribute.default", attrValue!.AsValue().DeepClone());
                                        }

                                        //delete the state of this attribute, so when i finish iterating through the dgraph attributes i am left with the ones that are not
                                        state.AsObject().Remove(attrKey!.GetValue<string>());
                                    }
                                    attrs.Add(newAttr);
                                }
                            }

                            if(state is not null && state is JsonObject stateObj2 && stateObj2.Count > 0)
                            {
                                //there are states remaining -> Add attributes
                                foreach((string key, JsonNode stateInfo) in stateObj2)
                                {
                                    if(stateInfo is not null)
                                    {
                                        var stateNode = stateInfo.DeepClone();
                                        stateNode.AsObject().Add("Attribute.key", key);
                                        attrs.Add(checkPrefixes(stateNode, nsDic, defaultPrefix, defaultUri));
                                    }
                                }
                            
                            thing["hasAttribute"] = attrs;
                            }
                            thing = thing.AsObject();
                        }

                        //Merge Unidirectional and Bidirectional relations, each in ~relatedTo and ~relatedFrom
                        var relatedTo = thing!["~relatedTo"]?.AsArray() ?? [];
                        var relatedFrom = thing!["~relatedFrom"]?.AsArray() ?? [];

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
                            var flattenedRel = new JsonArray();
                            var flattenedChild = new JsonArray();
                            foreach (var item in group)
                            {
                                var arr = item?["relatedTo"]?.AsArray();
                                if (arr is not null)
                                {
                                    foreach (var entry in arr)
                                    {
                                        flattenedRel.Add((ns is not null) ? entry!.DeepClone() : checkPrefixes(entry, nsDic, defaultPrefix, defaultUri));
                                    }
                                }
                                var arrChild = item?["hasChild"]?.AsArray();
                                if(arrChild is not null)
                                {
                                    foreach (var entry in arrChild)
                                    {
                                        flattenedChild.Add((ns is not null) ? entry!.DeepClone() : checkPrefixes(entry, nsDic, defaultPrefix, defaultUri));
                                    }
                                }
                            }
                            if (flattenedRel.Count > 0)
                                groupObj["relatedTo"] = flattenedRel;
                                
                            if(flattenedChild.Count>0)
                                groupObj["hasChild"] = flattenedChild;
                            JsonNode groupNode = groupObj;

                            if (ns is null)
                                groupNode = checkPrefixes(groupNode, nsDic, defaultPrefix, defaultUri);

                            groupedArray.Add(groupNode.AsObject());
                        }

                        // attach merged array
                        thing["relations"] = groupedArray;
                        thing.AsObject().Remove("~relatedTo");
                        thing.AsObject().Remove("~relatedFrom");

                        Console.WriteLine(thing.AsJsonString());

                        var mergedElement = JsonDocument.Parse(thing.ToJsonString()).RootElement;
                        finalNode["things"]!.AsArray().Add(JsonNode.Parse(mergedElement.GetRawText()));

                    }
                }
            }

            if (ns is null)
            {
                foreach (var pref in nsDic.Keys)
                {
                    finalNode["namespace"]!.AsArray().Add(new JsonObject
                    {
                        ["prefix"] = pref,
                        ["uri"] = nsDic.GetValueOrDefault(pref)
                    });
                }
            }

            return finalNode;
        }

        public async Task<JsonObject?> getJsonWithoutNamespace(string id)
        {
            return await getJsonWithNamespace(id, null);
        }

        private JsonNode? GetConstraintValue(string[] types, JsonObject node)
        {
            JsonNode res = null;
            int i=0;
            while(res is null && i<types.Length)
            {
                node.TryGetPropertyValue(types[i], out res);
                i++;
            }
            return res;
        }

        private JsonNode? GetConstraintValueFromParentNode(JsonObject cons, string type)
        {
            JsonNode? res = null;
            switch (type)
            {
                case "CardinalityConstraint":
                    res = GetConstraintValue(["minCount", "maxCount"], cons);
                    break;
                case "StructureConstraint":
                    res = GetConstraintValue(["datatype", "nodeKind", "class", "node"], cons);
                    break;
                case "SetConstraint":
                    res = GetConstraintValue(["in", "hasValue"], cons);
                    break;
                case "LogicalConstraint":
                    res = GetConstraintValue(["and", "or", "xone", "not"], cons);
                    break;
                case "ValueConstraint":
                    res = GetConstraintValue(["equals", "disjoint", "lessThan", "lessThanOrEquals"], cons);
                    break;
                case "GenericConstraint":                    
                default:
                    cons.TryGetPropertyValue("GenericConstraint.value", out res);
                    break;
            }
            return res;
        }

        private JsonObject GetCastedValue(JsonNode value, JsonNode parent, string key)
        {
            (_, var val) = GetLocalName(value["value"]!.ToString(), "");
                        //parse
            if(value["Value.type"]!.GetValue<string>() is var valType )
            {
                switch (valType.ToLower())
                {
                    case "integer":
                    case "int":
                        parent[key] = Convert.ToInt32(val);
                        break;
                    case "double":
                        parent[key] = Convert.ToDouble(val);
                        break;
                    case "bool":
                    case "boolean":
                        parent[key] = Convert.ToBoolean(val);
                        break;
                    case "string":
                    default:
                        parent[key] = val;
                        break;
                }
            }
            return parent.DeepClone().AsObject();
        }

        //TODO: Auxiliar method for flattening constraints
        private JsonObject GetFlattenedShapeConstraint(JsonObject parent, JsonObject og,  Dictionary<string, string> nsDic, string defaultPrefix, string defaultUri){
            if (og.TryGetPropertyValue("constraintId", out var constraintId) && constraintId is not null)
            {
                var constraintIdStr = constraintId.GetValue<string>();
                var cons = checkPrefixes(og, nsDic, defaultPrefix, defaultUri, "ShapeConstraint", constraintIdStr);
                
                //build constraint key with prefix and constraintId
                var key = $"{(cons.AsObject().TryGetPropertyValue("ShapeConstraint.prefix", out var consPrefix) ? consPrefix["prefix"] : defaultPrefix)}:{constraintIdStr}";
                
                //value -> we need to check the type
                var type = cons["dgraph.type"]!.AsArray()[0]!.GetValue<string>();
                var value = GetConstraintValueFromParentNode(cons.AsObject(), type);

                if(value is null)
                    return parent;

                //Here it can be:
                //Value
                //Reference
                //Property
                //Array (With elements of any type of the above)
            
                //TODO: if it's an array
                if(value is JsonArray && value.AsArray().Count>1)
                {
                    var newValueArray = new JsonArray();
                    foreach(var valueElement in value.AsArray())
                    {
                        var valueType = valueElement!["dgraph.type"]!.AsArray()[0]!.GetValue<string>();
                        if (valueType.Equals("NodeShape"))
                        {
                            newValueArray.Add(GetFlattenedNodeShape(valueElement, nsDic, defaultPrefix, defaultUri));
                        }else if (valueType.Equals("ShapeProperty"))
                        {
                            newValueArray.Add(GetFlattenedShapeProperty(valueElement!.AsObject(), nsDic, defaultPrefix, defaultUri));
                        }else if (valueType.Equals("Value"))
                        {
                            parent = GetCastedValue(value, parent, key);
                        }
                        
                    }
                    
                    parent[key] = JsonNode.Parse(newValueArray.ToJsonString());

                }else if(value is not null && value is JsonObject || (value is JsonArray && value.AsArray().Count == 1))
                {
                    //if it's reference -> prefix:id
                    //if it's value -> value
                    string nodeValType;
                    if(value is JsonArray)
                    {
                        value = value[0]; //we know it only has 1 element so this is fine
                        // nodeValType = value.AsArray()[0]!["dgraph.type"]!.AsArray()[0]!.GetValue<string>();
                    }
                    // else
                    // {
                    nodeValType = value!["dgraph.type"]!.AsArray()[0]!.GetValue<string>();
                    // }

                    if (nodeValType.Equals("Value"))
                    {
                        parent = GetCastedValue(value, parent, key);
                        
                    }else if (nodeValType.Equals("Reference"))
                    {
                        var valueName = value["Target.name"]!.GetValue<string>();
                        value = checkPrefixes(value, nsDic, defaultPrefix, defaultUri, "Target", valueName);
                        var valuePrefix = value["Target.prefix"]!["prefix"]!.GetValue<string>();
                        parent[key] = $"{valuePrefix}:{valueName}";
                    }else if (nodeValType.Equals("NodeShape"))
                    {
                        //recursive call to flattened Node Shape
                        parent[key] = JsonNode.Parse(GetFlattenedNodeShape(value, nsDic, defaultPrefix, defaultUri).ToJsonString());
                    }else if (nodeValType.Equals("ShapeProperty"))
                    {
                        parent[key] = JsonNode.Parse(GetFlattenedShapeProperty(value.AsObject(), nsDic, defaultPrefix, defaultUri).ToJsonString());
                    }
                }     
            }
            return parent;
        }

        //TODO: iterate through both methods with recursivity

        //TODO: Auxiliar method for flattening properties

        private JsonObject GetFlattenedShapeProperty(JsonObject og, Dictionary<string,string> nsDic, string defaultPrefix, string defaultUri)
        {
            
            /*iterate through its contents
                path
                description (optional)
                constraints (call to GetFlattenedConstraint)
                ShapeProperty.prefix (check if it's there, if not -> desfault)
            */

            og = checkPrefixes(og, nsDic, defaultPrefix, defaultUri, "ShapeProperty", "property").AsObject();
            var prop = new JsonObject();

            //Get path
            if(og.TryGetPropertyValue("path", out var pathValue))
                prop["sh:path"] = $"{(pathValue!.AsObject().TryGetPropertyValue("Target.prefix", out var pathPrefix) ? pathPrefix!["prefix"] : defaultPrefix)}:{(pathValue.AsObject().TryGetPropertyValue("Target.name", out var pathName) ? pathName : $"path")}";

            //we leave description as it is (if it exists it will show up, if not, it won't)

            //constraints
            if(og.TryGetPropertyValue("constraints", out var constraintsList) && constraintsList is not null)
            {
                foreach(var constraintNode in constraintsList.AsArray())
                    prop = GetFlattenedShapeConstraint(prop, constraintNode!.AsObject(), nsDic, defaultPrefix, defaultUri);
                //in each call, it returns a deep cloned prop node but with the new constraint added as a first level field
            }

            // prop["SG_typeOfNode"] = "Property";

            return prop.DeepClone().AsObject();
        }

        private JsonObject GetFlattenedNodeShape(JsonNode nodeShape, Dictionary<string,string> nsDic, string defaultPrefix, string defaultUri)
        {
            //save the uid of the defaultProperty so we don't add a duplicate
            var defaultPropertyUid = nodeShape!["defaultProperty"]!["uid"]!.GetValue<string>();

            //check namespace so we add it or not in the dictionary
            //we'll use the method checkPrefix (now adapted so it recieves the type)
            JsonObject flattenedNodeShape = checkPrefixes(nodeShape, nsDic, defaultPrefix, defaultUri, "NodeShape").AsObject();
            flattenedNodeShape["properties"] = new JsonArray();
            //then we iterate through the properties
            foreach(var propertyNode in nodeShape!["properties"]!.AsArray())
            {
                // Console.WriteLine($"PROP NODE: {propertyNode.ToSafeString()}");
                if(propertyNode is null)
                    continue;
                if (propertyNode["uid"]!.GetValue<string>().Equals(defaultPropertyUid))
                {
                    //if it's default property -> add it directly into flattenedNodeShape
                    var flattenedDefaultProperty = GetFlattenedShapeProperty(propertyNode.AsObject(), nsDic, defaultPrefix, defaultUri); 
                    
                    //add constraints array directly into flattenedNodeShape
                    foreach(var (key, defConstraintNode) in flattenedDefaultProperty)
                    {

                        flattenedNodeShape[key] = defConstraintNode.DeepClone();
                    }
                    flattenedNodeShape.Remove("defaultProperty");
                }
                else
                {
                    flattenedNodeShape["properties"]!.AsArray().Add(GetFlattenedShapeProperty(propertyNode.AsObject(), nsDic, defaultPrefix, defaultUri));
                }
            }

            if(flattenedNodeShape["properties"]!.AsArray().Count == 0)
                flattenedNodeShape.Remove("properties"); //there are no properties in this nodeShape

            //we need the nodeShapeId be prefix:name
            flattenedNodeShape["nodeShapeId"] = $"{(nodeShape.AsObject().TryGetPropertyValue("NodeShape.prefix", out var nodeShapePrefix) && nodeShape is not null ? nodeShapePrefix!["prefix"]!.GetValue<string>() : defaultPrefix)}:{nodeShape!["NodeShape.name"]!.GetValue<string>()}";

            //delete all other unnecessary data
            flattenedNodeShape.Remove("NodeShape.name");
            flattenedNodeShape.Remove("NodeShape.prefix");
            flattenedNodeShape.Remove("NodeShape.createdAt");
            flattenedNodeShape.Remove("dgraph.type");
            flattenedNodeShape.Remove("uid");
            return flattenedNodeShape.DeepClone().AsObject();
        }

        public async Task<JsonObject?> GetShapeGraphFlattenedJson(string id)
        {
            var finalNode = new JsonObject
            {
                ["shapeId"] = id,
                ["namespace"] = new JsonArray(),
                ["shapes"] = new JsonArray()
            };

            var json = await _dgraphService.GetShapeGraphNestedFullJson(id);
            if(json is null)
                return null;
            var defaultPrefix = $"pref{id}";
            var defaultUri = $"http://example.org/shapeGraph/{id}/";
            var nsDic = new Dictionary<string, string>
            {
                { defaultPrefix, defaultUri }
            };

            //iterate through the node shapes
            if(json is not null && (json?.TryGetProperty("shapes", out var jsonShapes) ?? false))
            {
                foreach(var nodeShape in jsonShapes.AsNode()!.AsArray())
                {
                    //only difference -> flattening of contraint value nodes + defaultproperties (directly on the node shape)
                    
                    var flattenedNodeShape = GetFlattenedNodeShape(nodeShape!, nsDic, defaultPrefix, defaultUri);
                    if(flattenedNodeShape is not null)
                        finalNode["shapes"]!.AsArray().Add(flattenedNodeShape);
                }
            }

            foreach(var pr in nsDic.Keys)
                finalNode["namespace"]!.AsArray().Add(new JsonObject{["prefix"] = pr, ["uri"] = nsDic.GetValueOrDefault(pr)});

            return finalNode;
        }

        private JsonObject GetJsonLDContext(JsonObject json, string idSanitized)
        {
            var namespaces = json["namespace"]?.AsArray() ?? new JsonArray();
            var context = new JsonObject();

            foreach (var ns in namespaces)
            {
                if (ns == null)
                {
                    continue;
                }
                var prefix = string.IsNullOrWhiteSpace(ns["prefix"]?.GetValue<string>()) ? $"blankNodePrefix_{idSanitized}" : ns["prefix"]?.ToString();
                var uri = ns["uri"]?.GetValue<string>();

                if (prefix is not null && uri is not null)
                {
                    context[prefix] = uri;
                }
            }
            return context;
        }

        private JsonObject GetJsonLDThing(JsonNode thingInfo, string idSanitized)
        {
            var thing = new JsonObject();

            //obtain prefix
            var prefix = thingInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
            prefix = string.IsNullOrWhiteSpace(prefix) ? $"blankNodePrefix_{idSanitized}" : prefix;
            //@id -> name (it's the thing id but without the ontology name as prefix)
            thing["@id"] = $"{prefix}:{thingInfo?[string.IsNullOrWhiteSpace(thingInfo?["name"]?.GetValue<string>()) ? "thingId" : "name"]}";
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
                    if(typeInfo is not null)
                        thing = GetJsonLDTypes(typeInfo, thing, idSanitized, types.AsArray().Count).AsObject();
                    
                }
            }

            //Attributes:
            var attributes = thingInfo?["hasAttribute"];
            if ((attributes is not null) && attributes.AsArray().Count > 0)
            {
                //this thing has attributes
                foreach (var attributeInfo in attributes.AsArray())
                {
                    if(attributeInfo is not null)
                        thing = GetJsonLDAttribute(attributeInfo, thing, idSanitized).AsObject();
                }
            }
            var relations = thingInfo?["relations"];
            if ((relations is not null) && relations.AsArray().Count > 0)
            {
                //this thing has relations with other things
                foreach (var relationInfo in relations.AsArray())
                {
                    if(relationInfo is not null)
                        thing = GetJsonLDRelation(relationInfo, thing, idSanitized).AsObject();
                }
            }

            return thing.DeepClone().AsObject();
        }

        private JsonNode GetJsonLDTypes(JsonNode typeInfo, JsonNode thing, string idSanitized, int typeCount)
        {
            var typeName = typeInfo?["name"]?.GetValue<string>();
            var typePrefix = typeInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
            typePrefix = string.IsNullOrWhiteSpace(typePrefix) ? $"blankNodePrefix_{idSanitized}" : typePrefix;

            if (typeName is not null && (typeName.Length > 0))
            {
                if (typeCount==1)
                {
                    //only one type
                    thing["@type"] = $"{((typePrefix == null || typePrefix.Length == 0) ? $"blankNodePrefix_{idSanitized}" : typePrefix)}:{typeName}";
                }
                else
                {
                    if (thing["@type"] is null)
                        thing["@type"] = new JsonArray();
                    //more than one type
                    thing["@type"]?.AsArray().Add($"{((typePrefix == null || typePrefix.Length == 0) ? $"blankNodePrefix_{idSanitized}" : typePrefix)}:{typeName}");
                }
            }
            return thing.DeepClone();
        }

        private JsonNode GetJsonLDAttribute(JsonNode attributeInfo, JsonNode thing, string idSanitized)
        {
            //TODO prefix support
            var key = attributeInfo?["Attribute.key"]?.GetValue<string>();
            var value = attributeInfo?["Attribute.value"]?.GetValue<string>();
            var attPrefix = attributeInfo?["Attribute.prefix"]?["prefix"]?.GetValue<string>();
            attPrefix = string.IsNullOrWhiteSpace(attPrefix) ? $"blankNodePrefix_{idSanitized}" : attPrefix;

            if ((key is not null) && (key.Length > 0))
            {
                //Look for state data in the json (value is null, as it is now Attribute.default):
                //Attribute.default ---> Previous Attribute.value
                //value ---------------> Value of actual state (can be null)
                //lastUpdate ----------> Date of the last time the state changed (can be null)
                
                if(value == null)
                {
                    //it has a state
                    // atr.lastUpdate: "...",
                    // atr.value =  value,
                    value = attributeInfo?["value"]?.AsValue().ToString();
                    var defaultValue = attributeInfo?["Attribute.default"]?.GetValue<string>();
                    var lastUpdate = attributeInfo?["lastUpdate"]?.GetValue<string>();

                    if (defaultValue is not null)
                        thing[$"{attPrefix}:{key}.default"] = defaultValue;
                        
                    thing[$"{attPrefix}:{key}.value"] = value;
                    thing[$"{attPrefix}:{key}.lastUpdate"] = lastUpdate;
                }
                else
                {
                    thing[$"{attPrefix}:{key}"] = value;
                }
            }
            return thing.DeepClone();
        }

        private JsonNode GetJsonLDRelation(JsonNode relationInfo, JsonNode thing, string idSanitized)
        {
            //TODO prefix support
            var name = relationInfo?["Relation.name"]?.GetValue<string>();
            var relPrefix = relationInfo?["Relation.prefix"]?["prefix"]?.GetValue<string>();
            relPrefix = string.IsNullOrWhiteSpace(relPrefix) ? $"blankNodePrefix_{idSanitized}" : relPrefix;
            var relatedNode = relationInfo?["relatedTo"] ?? relationInfo?["hasChild"];
            if (relatedNode is null)
            {
                return thing.DeepClone();
            }
            //check if there is only one lement or more
            if (name is not null && name.Length > 0)
            {
                List<string> relatedThingstr = new List<string>();
                foreach (var relatedThing in relatedNode.AsArray())
                {
                    var relatedName = relatedThing?["name"]?.GetValue<string>();
                    var relatedPrefix = relatedThing?["Thing.prefix"]?["prefix"]?.GetValue<string>();
                    relatedPrefix = string.IsNullOrWhiteSpace(relatedPrefix) ? $"blankNodePrefix_{idSanitized}" : relatedPrefix;
                    if (relatedName is not null && relatedName.Length > 0)
                        relatedThingstr.Add($"{relatedPrefix}:{relatedName}");
                }
                if (relatedThingstr.Count >= 1)
                {
                    var jsonArray = new JsonArray();

                    foreach (var idThing in relatedThingstr)
                    {
                        var node = new JsonObject
                        {
                            ["@id"] = idThing
                        };
                        jsonArray.Add(node);
                    }

                    thing[$"{relPrefix}:{name}"] = jsonArray.Count == 1 ? jsonArray[0]!.DeepClone() : jsonArray;
                }
            }

            return thing.DeepClone();

        }

        private JsonNode AddNodeIntoJsonArray(JsonNode shape, string key, JsonNode value)
        {
            if(shape[key] is null)
                shape[key] = new JsonArray();
            shape[key]!.AsArray().Add(value);
            return shape.DeepClone();
        }

        private JsonObject GetJsonLDNodeShape(JsonNode shapeInfo, string idSanitized)
        {
            var shape = new JsonObject();

            shape["@id"] = $"{shapeInfo?["nodeShapeId"]}";
            shape["@type"] = "sh:NodeShape";

            //iterate through its fields (in between them: properties)
            if(shapeInfo is not null)
            {
                foreach(var (key, shapeField) in shapeInfo.AsObject())
                {
                    if(!string.IsNullOrWhiteSpace(key) && shapeField is not null)
                    {

                        if (key.Equals("properties"))
                        {
                            foreach(var propertyInfo in shapeField.AsArray())
                            {
                                if(propertyInfo is not null)
                                    shape = AddNodeIntoJsonArray(shape, "sh:property", GetJsonLDShapeProperty(propertyInfo.AsObject(), idSanitized)).AsObject();
                            }
                        }
                        else
                        {
                            shape = GetJsonLDShapeConstraint(key, shapeField, shape, idSanitized).AsObject();
                        }
                    }
                }
            }

            return shape.DeepClone().AsObject();
        }

        private JsonNode GetJsonLDShapeProperty(JsonObject propertyInfo, string idSanitized)
        {
            var prop = new JsonObject();
            foreach(var (propKey, propField) in propertyInfo)
            {
                if(propField is not null)
                    prop = GetJsonLDShapeConstraint(propKey, propField, prop, idSanitized).AsObject();
            }
            return prop.DeepClone();
        }

        private JsonNode GetJsonLDShapeConstraint(string key, JsonNode shapeField, JsonNode shape, string idSanitized, bool isArray = false)
        {
            if(shapeField is JsonValue shapeValue)
            {
                //key: value
                switch (key.Contains(":") ? key.Split(":").Last().ToLower() : key)
                {
                    case "node":
                    case "and":
                    case "or":
                    case "not":
                    case "xone":
                    case "path":
                    case "datatype":

                        //set that the value should be { "@id": value }

                        if(key.Contains("datatype") && shapeValue.GetValue<string>().StartsWith("//"))
                            shapeValue = JsonValue.Create("http:" + shapeValue.GetValue<string>());
                        
                        if (isArray)
                        {
                            shape = AddNodeIntoJsonArray(shape, key, new JsonObject{["@id"] = shapeValue.DeepClone()});
                        }
                        else
                        {
                            shape[key] = new JsonObject{["@id"] = shapeValue.DeepClone()};
                        }
                        break;
                    default:
                        if (isArray)
                        {
                            shape= key.ToLower().Contains("target") ? AddNodeIntoJsonArray(shape, key, new JsonObject{["@id"] = shapeValue.DeepClone()}) : AddNodeIntoJsonArray(shape, key, shapeValue.DeepClone()) ;
                        }
                        else
                        {
                            shape[key] = key.ToLower().Contains("target") ? new JsonObject{["@id"] = shapeValue.DeepClone()} : shapeValue.DeepClone();
                        }
                        break;
                }
            }else if(shapeField is JsonArray)
            {
                //foreach though the array recursively calling
                foreach(var element in shapeField.AsArray())
                {
                    if(element is not null)
                        shape = GetJsonLDShapeConstraint(key, element, shape, idSanitized, true);
                }

            }else if(shapeField is JsonObject shapeObject)
            {
                //We don't know whether it's a nodeshape or a property
                //properties always have path field

                if (isArray)
                {
                    shape = AddNodeIntoJsonArray(shape, key, shapeObject.Any(p => p.Key.EndsWith("path", StringComparison.Ordinal)) ? GetJsonLDShapeProperty(shapeObject, idSanitized) : GetJsonLDNodeShape(shapeObject, idSanitized) );
                }
                else
                {
                    shape[key] = shapeObject.Any(p => p.Key.EndsWith("path", StringComparison.Ordinal)) ? GetJsonLDShapeProperty(shapeObject, idSanitized) : GetJsonLDNodeShape(shapeObject, idSanitized);
                }
            }
            return shape.DeepClone();
        }

        public async Task<JsonObject?> GetJsonLDFromRegularJson(JsonObject json, string id, bool shape = false)
        {
            string idSanitized = SanitizeTypeAndUIDValues(id);
            var nodes = json[shape ? "shapes" : "things"]?.AsArray() ?? new JsonArray();

            //initialize the context with the namespaces info
            // prefix: uri

            var context = GetJsonLDContext(json, idSanitized);
            // if (shape)
            // {
            //     context["datatype"] = new JsonObject{["@id"] = "sh:datatype", ["@type"]= "@id"};
            // }

            var finalNodes = new JsonArray();

            //iterate through the thing nodes 
            foreach (var node in nodes)
            {
                //add final thing to the things jsonArray
                if(node is not null)
                    finalNodes.Add(shape ? GetJsonLDNodeShape(node, idSanitized): GetJsonLDThing(node, idSanitized));
            }

            //assemble the final json
            var jsonLd = new JsonObject
            {
                ["@context"] = context,
                ["@graph"] = finalNodes
            };

            return jsonLd;
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


                graph.NamespaceMap.AddNamespace(string.IsNullOrWhiteSpace(prefix.ToString()) ? $"blankNodePrefix_{ontologyId}" : prefix.ToString(), new Uri(uri.ToString()));
            }
        }

        private void LoadNamespaceIntoGraph(JsonObject namespaces, IGraph graph, string ontologyId)
        {
            foreach (var (prefix, uri) in namespaces)
            {
                if(uri is not JsonObject)
                    graph.NamespaceMap.AddNamespace(string.IsNullOrWhiteSpace(prefix.ToString()) ? $"blankNodePrefix_{ontologyId}" : prefix.ToString(), new Uri(uri.ToString()));
            }
        }

        private void LoadNamespaceIntoGraph(JsonNode namespaces, IGraph graph, string ontologyId)
        {
            if(namespaces is JsonArray nsArr)
            {
                LoadNamespaceIntoGraph(nsArr, graph, ontologyId);
            }else if(namespaces is JsonObject nsObj)
            {
                LoadNamespaceIntoGraph(nsObj, graph, ontologyId);
            }
        }

        public async Task<VDS.RDF.Graph> GetRDFGraphFromJson(JsonObject json, string id, bool ld = false)
        {
            var idSanitized = SanitizeTypeAndUIDValues(id);
            var store = new TripleStore();
            var jsonLd = ld ? json : await GetJsonLDFromRegularJson(json, idSanitized);
            var jsonString = System.Text.Json.JsonSerializer.Serialize(jsonLd);
            var parser = new VDS.RDF.Parsing.JsonLdParser();
            using var reader = new StringReader(jsonString);
            parser.Load(store, reader);

            var mergedGraph = new VDS.RDF.Graph();
            //extract the namespaces from the Json
            
            JsonArray nsArr = new JsonArray();
            json.TryGetPropertyValue(ld ? "@context" :"namespace", out var nsJson);
            var ns = nsJson ?? new JsonObject();
            
            //load namespaces into graph for eventual prefix parsing to uri
            LoadNamespaceIntoGraph(ns, mergedGraph, idSanitized);

            foreach (var g in store.Graphs)
            {
                mergedGraph.Merge(g, true); // true = keep namespace mappings
            }
            return mergedGraph;

        }

        public async Task<MemoryStream> GetTTLFileFromRegularJson(string id, JsonObject json, bool ld = false)
        {
            var mergedGraph = await GetRDFGraphFromJson(json, id, ld);

            var ttlWriter = new VDS.RDF.Writing.CompressingTurtleWriter();
            using var sw = new StringWriter();
            ttlWriter.Save(mergedGraph, sw);
            string ttlString = sw.ToString();

            //convert the string to bytes
            var ttlBytes = System.Text.Encoding.UTF8.GetBytes(ttlString);
            var stream = new MemoryStream(ttlBytes);

            return stream;
        }
        
        public async Task<SparqlResultSet?> RunSparQLQuery(string id, JsonElement? ns, SparqlQuery query)
        {
            var json = ns is null ? await getJsonWithoutNamespace(id) : await getJsonWithNamespace(id, ns);
            if (json is null)
                return null;
            var graph = await GetRDFGraphFromJson(json, id);
            if(graph is null)
                return null;
            var store = new TripleStore();
            store.Add(graph);
            var dataset = new InMemoryDataset(store, true);
            var processor = new LeviathanQueryProcessor(dataset);
            var results = (SparqlResultSet)processor.ProcessQuery(query);

            return results;
        }
    }
}