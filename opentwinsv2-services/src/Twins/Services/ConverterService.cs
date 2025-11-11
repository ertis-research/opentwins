/*
Class that reunites all export functions common to Ontologies and Twins controllers.
*/

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Json.More;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using OpenTwinsV2.Shared.Constants;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

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
            if(localName is null || localName.Length == 0)
            {
                localName = SanitizeTypeAndUIDValues(nodeUri);
            }
            return (prefix, localName);
        }

        private JsonNode checkPrefixes(JsonNode node, Dictionary<string, string> ns, string twinPrefix, string twinUri)
        {
            if (node is JsonObject obj)
            {
                var pref = twinPrefix;
                var uri = twinUri;
                var type = node["Relation.name"] is not null ? "Relation" : (node["Attribute.key"] is not null ? "Attribute" : "Thing");
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
                    string propertyName = type.Equals("Thing") ? "thingId" : (type.Equals("Relation") ? "Relation.name" : "Attribute.key");
                    string newName = null;
                    if (obj.TryGetPropertyValue(propertyName, out var name) && name is not null)
                        (pref, newName) = GetLocalName(name.GetValue<string>(), twinPrefix);
                    

                    if (pref.Equals("otv2"))
                    {
                        uri = "http://opentwinsv2.org/"; //otv2 placeholder uri!!!!!!!!!
                        if (newName is not null)
                            obj[propertyName] = newName;
                    }
                    else
                    {
                        pref = twinPrefix;
                    }
                        
                    //first, check if it has the otv2 prefix
                    //if not, we create the attribute with the info from the twin's context
                    obj[$"{type}.prefix"] = new JsonObject //sample context prefix node
                    {
                        ["prefix"] = pref.Equals("otv2") ? pref : twinPrefix,
                        ["uri"] = pref.Equals("otv2") ? uri : twinUri 
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

        public async Task<JsonObject?> GetJsonLDFromRegularJson(JsonObject json, string id)
        {
            string idSanitized = SanitizeTypeAndUIDValues(id);
            var namespaces = json["namespace"]?.AsArray() ?? new JsonArray();
            var things = json["things"]?.AsArray() ?? new JsonArray();

            //initialize the context with the namespaces info
            // prefix: uri

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

            var finalThings = new JsonArray();

            //iterate through the thing nodes 
            foreach (var thingInfo in things)
            {
                var thing = new JsonObject();

                //obtener prefijo
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
                        var typeName = typeInfo?["name"]?.GetValue<string>();
                        var typePrefix = typeInfo?["Thing.prefix"]?["prefix"]?.GetValue<string>();
                        typePrefix = string.IsNullOrWhiteSpace(typePrefix) ? $"blankNodePrefix_{idSanitized}" : typePrefix;

                        if (typeName is not null && (typeName.Length > 0))
                        {
                            if (types.AsArray().Count == 1)
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
                        relPrefix = string.IsNullOrWhiteSpace(relPrefix) ? $"blankNodePrefix_{idSanitized}" : relPrefix;
                        var relatedNode = relationInfo?["relatedTo"] ?? relationInfo?["hasChild"];
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

        public async Task<VDS.RDF.Graph> GetRDFGraphFromRegularJson(JsonObject json, string id)
        {
            var idSanitized = SanitizeTypeAndUIDValues(id);
            var store = new TripleStore();
            var jsonLd = await GetJsonLDFromRegularJson(json, idSanitized);
            var jsonString = JsonSerializer.Serialize(jsonLd);
            var parser = new VDS.RDF.Parsing.JsonLdParser();
            using var reader = new StringReader(jsonString);
            parser.Load(store, reader);

            var mergedGraph = new VDS.RDF.Graph();
            //extract the namespaces from the Json
            
            JsonArray nsArr = new JsonArray();
            json.TryGetPropertyValue("namespace", out var nsJson);
            var ns = (nsJson ?? new JsonArray()).AsArray();
            
            //load namespaces into graph for eventual prefix parsing to uri
            LoadNamespaceIntoGraph(ns, mergedGraph, idSanitized);

            foreach (var g in store.Graphs)
            {
                mergedGraph.Merge(g, true); // true = keep namespace mappings
            }

            return mergedGraph;

        }

        public async Task<MemoryStream> GetTTLFileFromRegularJson(string id, JsonObject json)
        {
            // var ns = json["namespace"]?.AsArray() ?? new JsonArray();
            var mergedGraph = await GetRDFGraphFromRegularJson(json, id);

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
            var graph = await GetRDFGraphFromRegularJson(json, id);
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