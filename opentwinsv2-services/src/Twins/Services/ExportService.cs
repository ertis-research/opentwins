using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Api;
using Json.More;
using Lucene.Net.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using OpenTwinsV2.Shared.Constants;
using Twins.Builders;
using Twins.Services;
using VDS.Common.Collections.Enumerations;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Query.Expressions.Functions.XPath.Cast;

namespace OpenTwinsV2.Twins.Services
{
    /// <summary>
    /// Reunites all export functions common to Ontologies, Twins and Shapes controllers.
    /// </summary>
    public class ExportService
    {
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;

        public ExportService(DGraphService dgraphService, ThingsService thingsService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
        }

        /// <summary>
        /// Checks the prefixes controlling if they are present in the namespace dictionary, valid, etc and replaces it from the parent Json Node.
        /// </summary>
        /// <param name="node">The original Json Node that is being checked.</param>
        /// <param name="ns">The dictionary of namespaces.</param>
        /// <param name="defaultPrefix">The default prefix that will be set if the node doesn't have one.</param>
        /// <param name="defaultUri">The default uri that will be set if the node doesn't have one.</param>
        /// <param name="pType">OPTIONAL. The type of Json Node we are checking.</param>
        /// <param name="pName">OPTIONAl. The name or identifier of the Json Node we are checking.</param>
        /// <returns>
        /// Returns the cloned Json Node that was provided but with the necessary changes in the prefix field and changes on the namespace dictionary if needed.
        /// </returns>
        private void CheckPrefixes(JsonNode? node, Dictionary<string, string> ns, string defaultPrefix, string defaultUri, string pType = "", string pName = "")
        {
            if(node is null)
                return;
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
                    string? newName = null;
                    if (obj.TryGetPropertyValue(propertyName, out var name) && name is not null)
                        (pref, newName) = FormatService.GetLocalName(name.GetValue<string>(), defaultPrefix);
                    

                    if (pref.Equals("otv2"))
                    {
                        uri = "http://opentwinsv2.org/"; //otv2 placeholder uri!!!!!!!!!
                        if (newName is not null)
                            obj[propertyName] = newName;
                    }
                    else
                        pref = defaultPrefix;
                    
                        
                    //first, check if it has the otv2 prefix
                    //if not, we create the attribute with the info from the default's context
                    obj[$"{type}.prefix"] = new JsonObject //sample context prefix node
                    {
                        ["prefix"] = pref.Equals("otv2") ? pref : defaultPrefix,
                        ["uri"] = pref.Equals("otv2") ? uri : defaultUri 
                    };
                }
                if (!ns.ContainsKey(pref))
                    ns.Add(pref, uri);
            }
        }

        /// <summary>
        /// Obatins the raw Json from a Twin.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns the raw Json if something was obtained from DGraph.
        /// Returns null if null or whitespace was recieved from DGraph.
        /// </returns>
        private async Task<string?> GetTwinRawJson(string twinId)
        {
            var rawJson = await _dgraphService.GetThingsInTwinNQUADSAsync(twinId);

            return string.IsNullOrWhiteSpace(rawJson) ? null : rawJson;
        }

        /// <summary>
        /// Obtains the Twin's state dictionary.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns the dictionary of the states of the Twin, being the keys the ThingIds.
        /// </returns>
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
        
        /// <summary>
        /// Obtains and returns the Twin in Json format.
        /// </summary>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns>
        /// Returns the Json Object with the info of the Twin and its states.<br/>
        /// Returns null if something went wrong while obtaining the states and Twin dictionary.
        /// </returns>
        private async Task<JsonObject?> GetTwinJson(string twinId)
        {
            var dict = await GetTwinDict(twinId);
            return dict is null ? null : dict.AsJsonElement().AsNode()!.AsObject();
        }

        /// <summary>
        /// Formats the information of the Twin or Ontology into a Json.
        /// </summary>
        /// <param name="id">The identifier of the Twin or Ontology.</param>
        /// <param name="ns">The namespace dicitionary. If it's null, the Json returned will be of a Twin, otherwise it will be of an Ontology.</param>
        /// <returns>
        /// Returns the Twin's Json if the namespace dictionary was not defined.<br/>
        /// Returns the Ontology's Json if the namespace dictionary was defined.
        /// </returns>
        public async Task<JsonObject> GetJsonWithNamespace(string id, JsonElement? ns)
        {
            var finalNode = new JsonObject
            {
                [ns is not null ? "ontologyId" : "twinId"] = id,
                ["namespace"] = ns is null ? new JsonArray() : JsonNode.Parse(ns.Value.GetProperty("namespace").GetRawText()),
                ["things"] = new JsonArray()
            };
            var idSanitized = FormatService.SanitizeTypeAndUIDValues(id);
            var defaultPrefix = $"pref{idSanitized}";
            var defaultUri = $"http://example.org/twin/{idSanitized}/";
            var nsDic = new Dictionary<string, string>
            {
                { defaultPrefix, defaultUri }
            };

            var things = ns is null ? await _dgraphService.GetThingsInTwinAsync(id) : (await _dgraphService.GetThingsInOntologyAsync(id)).TryGetProperty("things", out var thingsInOntology) ? [.. thingsInOntology.EnumerateArray()] : [];

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
                            CheckPrefixes(thing, nsDic, defaultPrefix, defaultUri);
                            
                            //ATTRIBUTES
                            JsonArray attrs = new JsonArray();
                            JsonNode? state = new JsonObject();
                            if(thingStates is not null)
                            {
                                //load states of the thing
                                thingStates.TryGetPropertyValue(thingId.ToString(), out state);
                            }
                            if(thing!["hasAttribute"] is not null)
                            {
                                foreach (var attr in thing["hasAttribute"]!.AsArray())
                                {
                                    JsonObject newAttr = attr!.DeepClone().AsObject();
                                    CheckPrefixes(newAttr, nsDic, defaultPrefix, defaultUri);
                                    if (state is not null && state is JsonObject stateObj && stateObj.Count > 0 && attr!.AsObject().TryGetPropertyValue("Attribute.key", out var attrKey) && stateObj.TryGetPropertyValue(attrKey!.GetValue<string>(), out var stateInfo))
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
                                        stateObj.Remove(attrKey!.GetValue<string>());
                                    }
                                    attrs.Add(newAttr);
                                }
                            }

                            if(state is not null && state is JsonObject stateObj2 && stateObj2.Count > 0)
                            {
                                //there are states remaining -> Add attributes
                                foreach((string key, JsonNode? stateInfo) in stateObj2)
                                {
                                    if(stateInfo is not null)
                                    {
                                        var stateNode = stateInfo.DeepClone();
                                        stateNode.AsObject().Add("Attribute.key", key);
                                        CheckPrefixes(stateNode, nsDic, defaultPrefix, defaultUri);
                                        attrs.Add(stateNode);
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
                            var flattenedPart = new JsonArray();
                            foreach (var item in group)
                            {
                                var arr = item?["relatedTo"]?.AsArray();
                                if (arr is not null)
                                {
                                    foreach (var entry in arr)
                                    {
                                        var nodeToAdd = entry!.DeepClone();
                                        if(ns is null)
                                            CheckPrefixes(entry, nsDic, defaultPrefix, defaultUri);
                                        flattenedRel.Add(nodeToAdd);
                                    }
                                }
                                var arrChild = item?["hasChild"]?.AsArray();
                                if(arrChild is not null)
                                {
                                    foreach (var entry in arrChild)
                                    {
                                        var nodeToAdd = entry!.DeepClone();
                                        if(ns is null)
                                            CheckPrefixes(entry, nsDic, defaultPrefix, defaultUri);
                                        flattenedChild.Add(nodeToAdd);
                                    }
                                }
                                var arrPart = item?["hasPart"]?.AsArray();
                                if(arrPart is not null)
                                {
                                    foreach (var entry in arrPart)
                                    {
                                        var nodeToAdd = entry!.DeepClone();
                                        if(ns is null)
                                            CheckPrefixes(entry, nsDic, defaultPrefix, defaultUri);
                                        flattenedPart.Add(nodeToAdd);
                                    }
                                }
                            }
                            if (flattenedRel.Count > 0)
                                groupObj["relatedTo"] = flattenedRel;
                                
                            if(flattenedChild.Count>0)
                                groupObj["hasChild"] = flattenedChild;

                            if(flattenedPart.Count>0)
                                groupObj["hasPart"] = flattenedPart;
                            JsonNode? groupNode = groupObj;

                            if (ns is null)
                                CheckPrefixes(groupNode, nsDic, defaultPrefix, defaultUri);

                            if(groupNode is not null)
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

        /// <summary>
        /// Returns the Twin's Json.
        /// </summary>
        /// <param name="id">The identifier of the Twin.</param>
        /// <returns>
        /// Returns the Json of the Twin.
        /// </returns>
        public async Task<JsonObject?> GetJsonWithoutNamespace(string id)
        {
            return await GetJsonWithNamespace(id, null);
        }

        /// <summary>
        /// Extracts the Json Node of the value of the Constraint depending on its type. 
        /// </summary>
        /// <param name="types">Array of possible predicates of the constraint.</param>
        /// <param name="node">The Json Object we are extracting the value from.</param>
        /// <returns>
        /// Returns the Json Node of the value.
        /// </returns>
        private JsonNode? GetConstraintValue(string[] types, JsonObject node)
        {
            JsonNode? res = null;
            int i=0;
            while(res is null && i<types.Length)
            {
                node.TryGetPropertyValue(types[i], out res);
                i++;
            }
            return res;
        }

        /// <summary>
        /// Obtains the Json Node of the value of the constraint.
        /// </summary>
        /// <param name="cons">The Json Object of the constraint.</param>
        /// <param name="type">The type of the cosntraint.</param>
        /// <returns>
        /// Returns the Json Node of the value of the constraint.
        /// </returns>
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

        /// <summary>
        /// Duplicates the parent Json Object with the parsed value.
        /// </summary>
        /// <param name="value">The Json Node of the value.</param>
        /// <param name="parent">The Json Node of the parent.</param>
        /// <param name="key">The key of the value that will be added or overrided in the parent.</param>
        /// <returns>
        /// Returns the duplicated parent Json Object with the parsed value.
        /// </returns>
        private void GetCastedValue(JsonNode value, JsonNode parent, string key)
        {
            (_, var val) = FormatService.GetLocalName(value["value"]!.ToString(), "");
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
        }

        private void MergeIntoParent(JsonObject parent, JsonNode element, string key)
        {
            if(parent[key] is not null)
            {
                if(parent[key] is not JsonArray)
                    parent[key] = new JsonArray{parent[key]!.DeepClone()};
                parent[key]!.AsArray().Add(element.DeepClone());
            }else
                parent[key] = JsonNode.Parse(element.ToJsonString());
        }

        /// <summary>
        /// Flattens the Shape Constraint, applying the changes into the parent's duplicated Json Object.
        /// </summary>
        /// <param name="parent">The Json Obejct of the parent of the constraint.</param>
        /// <param name="og">The original Json Node of the constraint.</param>
        /// <param name="nsDic">The namespace dictionary.</param>
        /// <param name="defaultPrefix">The default prefix that will be applied if the node desn't have one.</param>
        /// <param name="defaultUri">The default uri that will be applied if the node doesn't have one.</param>
        /// <returns>Returns the duplicated parent Json Object with the flattened constraint.</returns>
        private JsonObject GetFlattenedShapeConstraint(JsonObject parent, JsonObject og,  Dictionary<string, string> nsDic, string defaultPrefix, string defaultUri){
            if (og.TryGetPropertyValue("constraintId", out var constraintId) && constraintId is not null)
            {
                var constraintIdStr = constraintId.GetValue<string>();
                var cons = og.DeepClone();
                CheckPrefixes(cons, nsDic, defaultPrefix, defaultUri, "ShapeConstraint", constraintIdStr);
                
                //build constraint key with prefix and constraintId
                var key = $"{(cons!.AsObject().TryGetPropertyValue("ShapeConstraint.prefix", out var consPrefix) ? consPrefix!["prefix"] : defaultPrefix)}:{constraintIdStr}";
                
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
            
                if(value is JsonArray valueArr && valueArr.Count>1)
                {
                    var newValueArray = new JsonArray();
                    foreach(var valueElement in valueArr)
                    {
                        JsonNode? valueToAdd = null;
                        var valueType = valueElement!["dgraph.type"]!.AsArray()[0]!.GetValue<string>();
                        if (valueType.Equals("NodeShape"))
                        {
                            valueToAdd = GetFlattenedNodeShape(valueElement, nsDic, defaultPrefix, defaultUri);
                        }else if (valueType.Equals("ShapeProperty"))
                        {
                            valueToAdd = GetFlattenedShapeProperty(valueElement!.AsObject(), nsDic, defaultPrefix, defaultUri);
                        }else if (valueType.Equals("Value"))
                        {
                            GetCastedValue(valueElement, parent, key);
                        }else if (valueType.Equals("Reference"))
                        {
                            var valueName = valueElement["Target.name"]!.GetValue<string>();
                            // valueElement = CheckPrefixes(valueElement, nsDic, defaultPrefix, defaultUri, "Target", valueName);
                            var valuePrefix = valueElement!["Target.prefix"]!["prefix"]!.GetValue<string>();
                            valueToAdd = (new JsonObject{["@id"] = $"{valuePrefix}:{valueName}"});
                        }else if (valueType.Contains("Constraint"))
                        {
                            
                            var constraintKey = valueElement["constraintId"]?.GetValue<string>();
                            if(constraintKey is null)
                                continue;
                            var existing = newValueArray.FirstOrDefault(o => o is JsonObject obj && obj.Count == 1 && obj[$"sh:{constraintKey}"] is not null);
                            valueToAdd = GetFlattenedShapeConstraint(existing?.AsObject() ?? [], valueElement.AsObject(), nsDic, defaultPrefix, defaultUri);
                            if(existing is null)
                                newValueArray.Add(valueToAdd.DeepClone());
                            continue;
                        }

                        if(valueToAdd is not null)
                        {
                            var existing = newValueArray.Where(o => o is JsonObject obj && obj.Count == 1 && obj[key] is not null);
                            if(existing.Any())
                                MergeIntoParent(existing.First()!.AsObject(), valueToAdd, key);
                            else    
                                newValueArray.Add(valueToAdd);
                        }
                    }
                    
                    MergeIntoParent(parent, newValueArray.Count == 1 ? newValueArray.First()! : newValueArray, key);

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
                        GetCastedValue(value, parent, key);
                        
                    }else if (nodeValType.Equals("Reference"))
                    {
                        var valueName = value["Target.name"]!.GetValue<string>();
                        CheckPrefixes(value, nsDic, defaultPrefix, defaultUri, "Target", valueName);
                        var valuePrefix = value!["Target.prefix"]!["prefix"]!.GetValue<string>();
                        MergeIntoParent(parent, $"{valuePrefix}:{valueName}", key);
                    }else if (nodeValType.Equals("NodeShape"))
                    {
                        //recursive call to flattened Node Shape
                        MergeIntoParent(parent, GetFlattenedNodeShape(value, nsDic, defaultPrefix, defaultUri), key);
                    }else if (nodeValType.Equals("ShapeProperty"))
                    {
                        MergeIntoParent(parent, GetFlattenedShapeProperty(value.AsObject(), nsDic, defaultPrefix, defaultUri), key);
                    }else if(nodeValType.Contains("Constraint"))
                        MergeIntoParent(parent, GetFlattenedShapeConstraint([], value.AsObject(), nsDic, defaultPrefix, defaultUri),key);
                }     
            }
            return parent;
        }
        /// <summary>
        /// Flattens the Shape Property Json Object.
        /// </summary>
        /// <param name="og">The original Shape Property Json Object.</param>
        /// <param name="nsDic">The namespace dictionary.</param>
        /// <param name="defaultPrefix">The default prefix that will be applied if the node desn't have one.</param>
        /// <param name="defaultUri">The default uri that will be applied if the node doesn't have one.</param>
        /// <returns>Returns the flattend Shape Property Json Object.</returns>
        private JsonObject GetFlattenedShapeProperty(JsonObject og, Dictionary<string,string> nsDic, string defaultPrefix, string defaultUri)
        {
            
            /*iterate through its contents
                path
                description (optional)
                constraints (call to GetFlattenedConstraint)
                ShapeProperty.prefix (check if it's there, if not -> desfault)
            */

            CheckPrefixes(og, nsDic, defaultPrefix, defaultUri, "ShapeProperty", "property");
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

            return prop.DeepClone().AsObject();
        }

        /// <summary>
        /// Falttens the Shape Node Json Node.
        /// </summary>
        /// <param name="nodeShape">The original Shape Node Json Node.</param>
        /// <param name="nsDic">The namespace dictionary.</param>
        /// <param name="defaultPrefix">The default prefix that will be applied if the node desn't have one.</param>
        /// <param name="defaultUri">The default uri that will be applied if the node doesn't have one.</param>
        /// <returns>Returns the flattened Node Shape Json Object.</returns>
        private JsonObject GetFlattenedNodeShape(JsonNode nodeShape, Dictionary<string,string> nsDic, string defaultPrefix, string defaultUri)
        {
            //save the uid of the defaultProperty so we don't add a duplicate
            var defaultPropertyUid = nodeShape!["defaultProperty"]!["uid"]!.GetValue<string>();

            //check namespace so we add it or not in the dictionary
            //we'll use the method checkPrefix (now adapted so it recieves the type)
            JsonObject flattenedNodeShape = nodeShape.DeepClone().AsObject();
            CheckPrefixes(flattenedNodeShape, nsDic, defaultPrefix, defaultUri, "NodeShape");
            flattenedNodeShape["properties"] = new JsonArray();
            //then we iterate through the properties
            foreach(var propertyNode in nodeShape!["properties"]!.AsArray())
            {
                if(propertyNode is null)
                    continue;
                if (propertyNode["uid"]!.GetValue<string>().Equals(defaultPropertyUid))
                {
                    //if it's default property -> add it directly into flattenedNodeShape
                    var flattenedDefaultProperty = GetFlattenedShapeProperty(propertyNode.AsObject(), nsDic, defaultPrefix, defaultUri); 
                    
                    //add constraints array directly into flattenedNodeShape
                    foreach(var (key, defConstraintNode) in flattenedDefaultProperty)
                    {

                        flattenedNodeShape[key] = defConstraintNode!.DeepClone();
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
            flattenedNodeShape["nodeShapeId"] = $"{(nodeShape.AsObject().TryGetPropertyValue("NodeShape.prefix", out var nodeShapePrefix) && nodeShape is not null ? nodeShapePrefix!["prefix"]?.GetValue<string>() ?? defaultPrefix : defaultPrefix)}:{nodeShape!["NodeShape.name"]!.GetValue<string>()}";

            //delete all other unnecessary data
            flattenedNodeShape.Remove("NodeShape.name");
            flattenedNodeShape.Remove("NodeShape.prefix");
            flattenedNodeShape.Remove("NodeShape.createdAt");
            flattenedNodeShape.Remove("dgraph.type");
            flattenedNodeShape.Remove("uid");
            return flattenedNodeShape;
        }

        /// <summary>
        /// Flattens the Shape Graph Json.
        /// </summary>
        /// <param name="id">The identifier of the Shape Graph.</param>
        /// <returns>Returns the flattend Json Object of the Shape Graph.</returns>
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

        /// <summary>
        /// Obtains the context of a JsonLD.
        /// </summary>
        /// <param name="json">The Json in JsonLD format.</param>
        /// <param name="idSanitized">The sanitized identifier of the object.</param>
        /// <returns>
        /// Returns the context of the JsonLD in a Json Object.
        /// </returns>
        private static JsonObject GetJsonLDContext(JsonObject json, string idSanitized)
        {
            var namespaces = json["namespace"]?.AsArray() ?? new JsonArray();
            var context = new JsonObject();

            foreach (var ns in namespaces)
            {
                if (ns == null)
                    continue;
                
                var prefix = ns["prefix"]?.GetValue<string>() is null ? $"blankNodePrefix_{idSanitized}" : ns["prefix"]?.ToString();
                if(string.IsNullOrWhiteSpace(prefix))
                    prefix = "@vocab";
                var uri = ns["uri"]?.GetValue<string>();

                if (prefix is not null && uri is not null)
                {
                    context[prefix] = uri;
                }
            }
            return context;
        }

        /// <summary>
        /// Formats the Thing Json Node in regular Json format into a JsonLD format.
        /// </summary>
        /// <param name="thingInfo">The Thing in regular Json format.</param>
        /// <param name="thing">The Json of the Thing in regular Json format.</param>
        /// <param name="idSanitized">The sanitized identifier of the object.</param>
        /// <returns>
        /// Returns the Thing in JsonLD format in Json Obejct.
        /// </returns>
        private static void GetJsonLDThing(JsonNode thingInfo, JsonObject thing, string idSanitized)
        {
            //obtain prefix
            var prefix = thingInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
            prefix ??= $"blankNodePrefix_{idSanitized}";
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
                        GetJsonLDTypes(typeInfo, thing, idSanitized, types.AsArray().Count);
                }
            }

            //Inheritance
            var parent = thingInfo?["inheritsFrom"];
            if (parent is not null)
                foreach(var parentInfo in parent is JsonArray ? parent.AsEnumerable() : Enumerable.Repeat(parent.AsObject(), 1))
                    if(parentInfo is not null)
                        GetJsonLdThingInheritance(parentInfo, thing, idSanitized);

            //Attributes:
            var attributes = thingInfo?["hasAttribute"];
            if ((attributes is not null) && attributes.AsArray().Count > 0)
            {
                //this thing has attributes
                foreach (var attributeInfo in attributes.AsArray())
                {
                    if(attributeInfo is not null)
                        GetJsonLDAttribute(attributeInfo, thing, idSanitized);
                }
            }
            var relations = thingInfo?["relations"];
            if ((relations is not null) && relations.AsArray().Count > 0)
            {
                //this thing has relations with other things
                foreach (var relationInfo in relations.AsArray())
                {
                    if(relationInfo is not null)
                        GetJsonLDRelation(relationInfo, thing, idSanitized);
                }
            }
        }

        /// <summary>
        /// Modifies the Thing Json Node to obtain and format properly the type info of the Thing.
        /// </summary>
        /// <param name="typeInfo">The Json Node of the type of the Thing.</param>
        /// <param name="thing">The Json Node of the Thing.</param>
        /// <param name="idSanitized">The sanitized identifier od the object.</param>
        /// <param name="typeCount">Number of types that the Thing has.</param>
        /// <returns>
        /// Returns the duplicated Thing Json Node with the parsed type info.
        /// </returns>
        private static void GetJsonLDTypes(JsonNode typeInfo, JsonNode thing, string idSanitized, int typeCount)
        {
            var typeName = typeInfo?["name"]?.GetValue<string>();
            var typePrefix = typeInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
            typePrefix ??= $"blankNodePrefix_{idSanitized}";

            if (typeName is not null && (typeName.Length > 0))
            {
                if (typeCount==1)
                {
                    //only one type
                    thing["@type"] = $"{typePrefix ?? $"blankNodePrefix_{idSanitized}"}:{typeName}";
                }
                else
                {
                    if (thing["@type"] is null)
                        thing["@type"] = new JsonArray();
                    //more than one type
                    thing["@type"]?.AsArray().Add($"{typePrefix ?? $"blankNodePrefix_{idSanitized}"}:{typeName}");
                }
            }
        }

        /// <summary>
        /// Modifies the Thing Json Node to format the Attribute info into a JsonLD format.
        /// </summary>
        /// <param name="attributeInfo">The Json Node with the info of the Attribute.</param>
        /// <param name="thing">The Json Node of the Thing.</param>
        /// <param name="idSanitized">The sanitized identifier of the object.</param>
        /// <returns>
        /// Returns the modified Thing Json Node with the parsed Attribute info in JsonLD format.
        /// </returns>
        private static void GetJsonLDAttribute(JsonNode attributeInfo, JsonNode thing, string idSanitized)
        {
            var key = attributeInfo?["Attribute.key"]?.GetValue<string>();
            var value = attributeInfo?["Attribute.value"]?.GetValue<string>();
            var attPrefix = attributeInfo?["Attribute.prefix"]?["prefix"]?.GetValue<string>();
            attPrefix ??= $"blankNodePrefix_{idSanitized}";

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
        }

        /// <summary>
        /// Modifies the Thing Json Node to format the Relation info into a JsonLD format.
        /// </summary>
        /// <param name="relationInfo">The Json Node with the info of the Relation.</param>
        /// <param name="thing">The Json Node of the Thing.</param>
        /// <param name="idSanitized">The sanitized identifier of the object.</param>
        /// <returns>
        /// Returns the modified Thing Json Node with the parsed Relation info in JsonLD format.
        /// </returns>
        private static void GetJsonLDRelation(JsonNode relationInfo, JsonNode thing, string idSanitized)
        {
            var name = relationInfo?["Relation.name"]?.GetValue<string>();
            var relPrefix = relationInfo?["Relation.prefix"]?["prefix"]?.GetValue<string>();
            relPrefix ??= $"blankNodePrefix_{idSanitized}";
            JsonArray relatedNode = [];
            var relationKeys = new[] { "relatedTo", "hasChild", "hasPart" };

            foreach (var key in relationKeys)
            {
                var sourceArray = relationInfo?[key]?.AsArray();
                if (sourceArray != null)
                    foreach (var item in sourceArray)
                        if (item != null)
                            relatedNode.Add(item.DeepClone()); 
            }
            
            if (relatedNode is null)
                return;
            
            //check if there is only one lement or more
            if (name is not null && name.Length > 0)
            {
                List<string> relatedThingstr = new List<string>();
                foreach (var relatedThing in relatedNode.AsArray())
                {
                    var relatedName = relatedThing?["name"]?.GetValue<string>();
                    var relatedPrefix = relatedThing?["Thing.prefix"]?["prefix"]?.GetValue<string>();
                    relatedPrefix ??= $"blankNodePrefix_{idSanitized}";
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
        }

        private static void GetJsonLdThingInheritance(JsonNode parentInfo, JsonNode thing, string idSanitized)
        {
            var name = parentInfo?["name"]?.GetValue<string>();
            // Console.WriteLine($"name: {name}");
            var prefix = parentInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
            prefix ??=  $"blankNodePrefix_{idSanitized}";

            if(name is null)
                return;

            var inheritanceNode = thing["rdfs:subClassOf"];
            JsonObject idNode = new() { ["@id"] = $"{prefix}:{name}" };

            if(inheritanceNode is null)
                thing["rdfs:subClassOf"] = idNode;
            else if(inheritanceNode is JsonObject inheritanceObject)
                thing["rdfs:subClassOf"] = new JsonArray{inheritanceObject, idNode};
            else if(inheritanceNode is JsonArray inheritanceArray)
                inheritanceArray.Add(idNode);
        }

        /// <summary>
        /// Adds the value into the array identified by the key in the Shape Json Node.
        /// </summary>
        /// <param name="shape">The Json Node of the shape.</param>
        /// <param name="key">The key for the value.</param>
        /// <param name="value">The Json Node of the value.</param>
        /// <returns>
        /// Returns the modified Shape Json Node with the added value into the key array.
        /// </returns>
        private static void AddNodeIntoJsonArray(JsonObject shape, string key, JsonNode value)
        {
            if(shape[key] is null)
                shape[key] = new JsonArray();
            shape[key]!.AsArray().Add(value);
        }

        /// <summary>
        /// Obtains and formats into JsonLD the info of the Shape Node.
        /// </summary>
        /// <param name="shapeInfo">The Json Node with the Shape Node info in regular Json format.</param>
        /// <param name="shape">The Json of the shape in regular Json format.</param>
        /// <param name="idSanitized">The sanitized identifier of the object.</param>
        /// <returns>
        /// Returns the formatted Json Object of the Shapa Node in JsonLD format.
        /// </returns>
        private static void GetJsonLDNodeShape(JsonNode shapeInfo, JsonObject shape, string idSanitized)
        {
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
                                {
                                    var prop = new JsonObject();
                                    GetJsonLDShapeProperty(propertyInfo.AsObject(), prop, idSanitized);
                                    AddNodeIntoJsonArray(shape, "sh:property", prop);
                                }       
                            }
                        }
                        else
                        {
                            GetJsonLDShapeConstraint(key, shapeField, shape, idSanitized);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Obtains the formatted JsonLD equivalent Json Node of the Shape Property.
        /// </summary>
        /// <param name="propertyInfo">The Json Object with the info in regular Json format.</param>
        /// <param name="prop">The Json of the property in regular Json format.</param>
        /// <param name="idSanitized">The sanitized identifier of the object.</param>
        /// <returns>
        /// Returns the Json Object of the Shape Property in JsonLD format.
        /// </returns>
        private static void GetJsonLDShapeProperty(JsonObject propertyInfo, JsonObject prop, string idSanitized)
        {
            foreach(var (propKey, propField) in propertyInfo)
            {
                if(propField is not null)
                    GetJsonLDShapeConstraint(propKey, propField, prop, idSanitized);
            }
        }

        private static bool IsShaClConstraint(string id)
        {
            return id switch
            {
                "and" or "or" or "not" or "xone" => true,
                _ => false
            };
        }

        private static bool IsAConstraintObject(JsonNode node)
        {
            if(node is JsonObject obj)
                if(obj.Count!=1){
                    return false;
                }else
                {
                    foreach((var key, _) in obj) //always only one
                    {
                        var index = key.IndexOf(':');
                        if( index == key.Length-1)
                            return false;
                        return key is not null && IsShaClConstraint(key[(index == -1 ? 0 : index+1)..]);
                    }
                        
                }
            
            return false;
            
        }

        private static bool IsAPropertyObject(JsonNode node)
        {
            if(node is JsonObject shapeObject)
                return shapeObject.Any(p => p.Key.EndsWith("path", StringComparison.Ordinal));
            return false;
        }

        /// <summary>
        /// Modifies the Shape json Node to include the formatted JsonLD equivalent Json Node of the Shape Constraint.
        /// </summary>
        /// <param name="key">The constraint key.</param>
        /// <param name="shapeField">The value of the Shape Constraint.</param>
        /// <param name="shape">The original Shape Json Node.</param>
        /// <param name="idSanitized">The sanitized identifier of the object.</param>
        /// <param name="isArray">OPTIONAl. Whether the constraint value is known to be an array</param>
        /// <returns>Returns the modified Shape Json Node with the Shape Constraint in JsonLD format.</returns>
        private static void GetJsonLDShapeConstraint(string key, JsonNode shapeField, JsonObject shape, string idSanitized, bool isArray = false)
        {
            if(shapeField is JsonValue shapeValue)
            {
                //key: value
                switch (key.Contains(':') ? key.Split(":").Last().ToLower() : key)
                {
                    case "node":
                    case "and":
                    case "or":
                    case "not":
                    case "xone":
                    case "path":
                    case "datatype":
                    case "class":
                    case "nodekind":

                        //set that the value should be { "@id": value }
                        if((key.Contains("datatype") || key.Contains("nodekind", StringComparison.InvariantCultureIgnoreCase)) && shapeValue.GetValue<string>().StartsWith("//"))
                            shapeValue = JsonValue.Create("http:" + shapeValue.GetValue<string>());
                        
                        if (isArray)
                        {
                            AddNodeIntoJsonArray(shape, key, new JsonObject{["@id"] = shapeValue.DeepClone()});
                        }
                        else
                        {
                            shape[key] = new JsonObject{["@id"] = shapeValue.DeepClone()};
                        }
                        break;
                    default:
                        if (isArray)
                        {
                            if(key.Contains("target", StringComparison.InvariantCultureIgnoreCase))
                                AddNodeIntoJsonArray(shape, key, new JsonObject{["@id"] = shapeValue.DeepClone()});
                            else
                                AddNodeIntoJsonArray(shape, key, shapeValue.DeepClone()) ;
                        }
                        else
                        {
                            shape[key] = key.Contains("target", StringComparison.InvariantCultureIgnoreCase) ? new JsonObject{["@id"] = shapeValue.DeepClone()} : shapeValue.DeepClone();
                        }
                        break;
                }
            }else if(shapeField is JsonArray)
            {
                //foreach through the array recursively calling
                foreach(var element in shapeField.AsArray())
                {
                    if(element is not null)
                        GetJsonLDShapeConstraint(key, element, shape, idSanitized, true);
                }

            }else if(shapeField is JsonObject shapeObject)
            {
                //We don't know whether it's a nodeshape, property or a constraint
                //properties always have path field
                var node = new JsonObject();
                
                if(IsAPropertyObject(shapeObject))
                    GetJsonLDShapeProperty(shapeObject, node, idSanitized);
                else if(IsAConstraintObject(shapeObject))
                    GetJsonLDShapeConstraint(shapeObject.Select(pair => pair.Key).First()!, shapeObject[shapeObject.Select(pair => pair.Key).First()!]!, node, idSanitized, shapeObject[shapeObject.Select(pair => pair.Key).First()!] is JsonArray);
                else
                    GetJsonLDNodeShape(shapeObject, node, idSanitized);
                if (isArray)
                    AddNodeIntoJsonArray(shape, key, node);
                else
                    shape[key] = node;
            }
        }

        /// <summary>
        /// Formats the provided Json in regular Json format into its equivalent in JsonLD format.
        /// </summary>
        /// <param name="json">The original Json.</param>
        /// <param name="id">The identifier of the object.</param>
        /// <param name="shape">OPTIONAl. Whether the Json corresponds to a Shape Graph or not.</param>
        /// <returns>Returns the JsonLd equivalent to the Json provided.</returns>
        public static JsonObject GetJsonLDFromRegularJson(JsonObject json, string id, bool shape = false)
        {
            string idSanitized = FormatService.SanitizeTypeAndUIDValues(id);
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
                {
                    var insert = new JsonObject();
                    if(shape)
                        GetJsonLDNodeShape(node, insert, idSanitized);
                    else
                        GetJsonLDThing(node, insert, idSanitized);
                    
                    finalNodes.Add(insert);
                }
            }

            //assemble the final json
            var jsonLd = new JsonObject
            {
                ["@context"] = context,
                ["@graph"] = finalNodes
            };

            return jsonLd;
        }

        public async Task<JsonObject> BuildShapeGraphFromOntology(string ontologyId)
        {
            var ns = await _dgraphService.GetNamespacesInOntologyAsync(ontologyId) ?? throw new Exception($"Got null namespace of {ontologyId} Ontology");
            var json = await GetJsonWithNamespace(ontologyId, ns) ?? throw new Exception($"Got null regular Json of {ontologyId} Ontology");
            var jsonLd = GetJsonLDFromRegularJson(json, ontologyId) ?? throw new Exception($"Got null JsonLD from the {ontologyId} Ontology Json");

            var context = jsonLd["@context"]!.DeepClone();
            if(context["xsd"] is not null)
                context["xsd"] = "http://www.w3.org/2001/XMLSchema#";
            context["sh"]="http://www.w3.org/ns/shacl#";
            var graphArray = new JsonArray();
            var shapeGraph = new JsonObject{
                ["@context"] = context,
                ["@graph"] = graphArray
            };

            var graphIndex = graphArray
                .Where(n => n is not null && n?["@id"] != null)
                .ToDictionary(n => n!["@id"]!.GetValue<string>(), n => n!.AsObject())!;  
            

            foreach(var thing in jsonLd["@graph"]!.AsArray())
            {
                if(thing is null)
                    continue;
                ShapeBuilder.BuildShapeAlgorithm(ontologyId, thing, graphArray, graphIndex);
                ShapeBuilder.BuildShapeAlgorithm(ontologyId, thing, graphArray, graphIndex, relations:false);
            }
            
            return shapeGraph;
        }

        /// <summary>
        /// Loads into the RDF Graph the namespaces provided.
        /// </summary>
        /// <param name="namespaces">The array of the namespaces.</param>
        /// <param name="graph">The graph object.</param>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        private static void LoadNamespaceIntoGraph(JsonArray namespaces, IGraph graph, string ontologyId)
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


                graph.NamespaceMap.AddNamespace((prefix ?? $"blankNodePrefix_{ontologyId}").ToString(), new Uri(uri.ToString()));
            }
        }

        /// <summary>
        /// Loads into the RDF Graoh the namespaces provided.
        /// </summary>
        /// <param name="namespaces">The JsonObject of the namespaces.</param>
        /// <param name="graph">The graoh object.</param>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        public static void LoadNamespaceIntoGraph(JsonObject namespaces, IGraph graph, string ontologyId)
        {
            foreach (var (prefix, uri) in namespaces)
            {
                if(uri is not JsonObject)
                    graph.NamespaceMap.AddNamespace((prefix ?? $"blankNodePrefix_{ontologyId}").ToString(), new Uri(uri!.ToString()));
            }
        }

        /// <summary>
        /// Loads into the RDF GRaph the namespaces provided.
        /// </summary>
        /// <param name="namespaces">The JsonNode of the namespaces. Must be either Json Array or Json Object.</param>
        /// <param name="graph">The graoh object.</param>
        /// <param name="ontologyId">The identiier of the Ontology.</param>
        public static void LoadNamespaceIntoGraph(JsonNode namespaces, IGraph graph, string ontologyId)
        {
            if(namespaces is JsonArray nsArr)
            {
                LoadNamespaceIntoGraph(nsArr, graph, ontologyId);
            }else if(namespaces is JsonObject nsObj)
            {
                LoadNamespaceIntoGraph(nsObj, graph, ontologyId);
            }
        }
        
        /// <summary>
        /// Runs a SparQL query on either an ontology or a Twin and obtains its result.
        /// </summary>
        /// <param name="id">The identifier of the Twin or the Ontology.</param>
        /// <param name="ns">The namespace dicitionary. If it's defined it will assume it's an Ontology, if not a Twin.</param>
        /// <param name="query">The SparQl query.</param>
        /// <returns>
        /// Returns the result of running the query.<br/>
        /// Returns null if any issue was encountered while running the query.
        /// </returns>
        public async Task<SparqlResultSet?> RunSparQLQuery(string id, JsonElement? ns, SparqlQuery query)
        {
            var json = ns is null ? await GetJsonWithoutNamespace(id) : await GetJsonWithNamespace(id, ns);
            if (json is null)
                return null;
            var graph = FormatService.GetRDFGraphFromJson(json, id);
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