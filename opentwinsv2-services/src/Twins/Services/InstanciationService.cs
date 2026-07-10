using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AngleSharp.Common;
using Api;
using Json.More;
using Lucene.Net.Util;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Builders;
using OpenTwinsV2.Twins.Models;
using OpenTwinsV2.Twins.Services;
using Pb;
using Twins.Models;
using Twins.Services;
using VDS.Common.References;
using VDS.RDF;
using VDS.RDF.Shacl;
using VDS.RDF.Shacl.Validation;

namespace OpenTwinsV2.Twins.Services
{
    public class InstanciationService
    {
        private readonly DGraphService _dgraphService;
        private readonly ThingsService _thingsService;
        private readonly ExportService _exportService;

        public InstanciationService(DGraphService dgraphService, ThingsService thingsService, ExportService exportService)
        {
            _dgraphService = dgraphService;
            _thingsService = thingsService;
            _exportService = exportService;
        }

        /// <summary>
        /// Checks if there is any id that appears more than once in a given Graph.
        /// </summary>
        /// <param name="subgraph">The Graph with Things.</param>
        /// <returns>
        /// Returns true if there is any repeated id.<br/>
        /// Returns false if every id appears only once.
        /// </returns>
        public bool AreThereConflictingIdsOnSubGraph(JsonArray subgraph)
        {
            return subgraph.Select(t => t!["@id"]?.GetValue<string>() ?? t!["id"]!.GetValue<string>())
            .Count(id => subgraph.Count(t => (t!["@id"]?.GetValue<string>() ?? t!["id"]!.GetValue<string>())==id)>1)>0;
        }

        public List<string> GetIdsFromGraph(JsonArray graph)
        {
            var idList = graph.Select(thing => thing!["@id"]?.GetValue<string>() ?? thing["id"]!.GetValue<string>()).ToList();
            foreach(var thing in graph)
            {
                var innerIdList = thing!.AsObject().ToDictionary().Where(pair => (pair.Key != "@id" || pair.Key != "id") && (pair.Value is JsonObject || pair.Value is JsonArray))
                    .SelectMany(pair => pair.Value is JsonObject valueObj ?
                        [valueObj["@id"]?.GetValue<string>() ?? valueObj["id"]?.GetValue<string>()]
                    :
                        pair.Value.AsArray().Select(arrObj => arrObj is JsonObject ? arrObj["@id"]?.GetValue<string>() ?? arrObj["id"]?.GetValue<string>() : null)
                    ).Distinct(); 
                
                if(innerIdList is null || innerIdList.Any(string.IsNullOrWhiteSpace))
                    throw new ArgumentException($"In the Thing {(thing["@id"] ?? thing["id"])!.GetValue<string>()} there's a relation that links to an objects without an identifier");
                
                idList.AddRange(innerIdList?.OfType<string>().Except(idList) ?? []);
            }
            return idList;
        } 

        public static bool IsRelationBidirectional(string sourceId, string targetId, string relName, Dictionary<string, ThingDescription> tds, JsonArray graph)
        {
            //Check first if the relation is defined in the ThingDescription, if it's not, then look in the Json Graph
            //every td exists and we have it

            if((tds[targetId].Links ?? []).Any(link => link.Rel == relName && link.Href.ToString() == sourceId))
                return true;
            try
            {
                var target = graph.FirstOrDefault(thing => ((thing!["@id"] ?? thing["id"])?.GetValue<string>() ?? "") == targetId) ?? throw new ArgumentNullException("target");
                var relation = target[relName] ?? throw new ArgumentNullException("relation");
                JsonArray targets = relation switch
                {
                    JsonObject obj => new JsonArray(obj.DeepClone()),
                    JsonArray arr => arr,
                    _ => []
                };
                return targets.Any(obj => ((obj!["@id"] ?? obj["id"])?.GetValue<string>() ?? "") == sourceId);
            }catch(ArgumentNullException ex){
                Console.WriteLine($"False because it failed. {ex.Message}");
                return false;
            }
        }

        public static JsonObject? GetUnidirectionalRelationPayload(string sourceUid, string targetUid, string relName, JsonArray payload)
        {
            return payload.FirstOrDefault(p => p is JsonObject pObj && 
                (pObj["dgraph.type"]?.AsArray() ?? []).Any(node => node?.GetValue<string>() == "Relation") &&
                (pObj["Relation.name"]?.GetValue<string>() ?? "") == relName &&
                (pObj["relatedTo"]?.AsArray().Any(obj => (obj!["uid"]?.GetValue<string>() ?? "") == targetUid) ?? false) && 
                (pObj["relatedFrom"]?.AsArray().Any(obj => (obj!["uid"]?.GetValue<string>() ?? "") == sourceUid) ?? false))?.AsObject();
        }

        public static JsonObject? GetBidirectionalRelationPayload(string sourceUid, string targetUid, string relName, JsonArray payload)
        {
            return payload.FirstOrDefault(p => p is JsonObject pObj && 
                (pObj["dgraph.type"]?.AsArray() ?? []).Any(node => node?.GetValue<string>() == "Relation") &&
                (pObj["Relation.name"]?.GetValue<string>() ?? "") == relName &&
                (pObj["relatedFrom"] is null) &&
                pObj["relatedTo"] is JsonArray relatedTo && relatedTo.Any(obj => (obj!["uid"]?.GetValue<string>() ?? "") == targetUid) && relatedTo.Any(obj => (obj!["uid"]?.GetValue<string>() ?? "") == sourceUid)) 
                ?.AsObject();
        }

        /// <summary>
        /// Updates the Logs in Validation, instanciating any Data Structure that needs to and adding a new error message.
        /// </summary>
        /// <param name="validation">The Validation Object.</param>
        /// <param name="category">The group to which the new error belongs to.</param>
        /// <param name="errorMsg">The error description.</param>
        private void UpdateValidationLogs(ValidationBody validation, string category, string errorMsg)
        {
            ((validation.Logs ??= new()).GetValueOrDefault(category) ?? (validation.Logs![category] = new HashSet<string>())).Add(errorMsg);
        }

        /// <summary>
        /// Builds a record with a Thing's dependencies.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <param name="thing">The info of the Thing in Json format.</param>
        /// <param name="existingDependencies">Set with already explored Things.</param>
        /// <param name="validation">OPTIONAL. A Validation Object. By default: null, if not null then its inner graph will be validated at the same time the dependencies are being built.</param>
        /// <returns></returns>
        private async Task<ThingDependency> BuildThingDependency(string ontologyId, string thingId, JsonElement thing, HashSet<string> existingDependencies, ValidationBody? validation = null)
        {
            existingDependencies.Add(thingId);
            List<string>  types = new List<string>();
            if(thing.TryGetProperty("typeThing", out var typeEl))
                foreach(var type in typeEl.AsNode() is JsonArray arr ? arr : new JsonArray{typeEl.AsNode()!.GetValue<string>()})
                    types.Add(type!.GetValue<string>());
            List<RelationDependency> relDeps = new List<RelationDependency>();
            if(thing.TryGetProperty("unidirectionals", out var unidir))
                relDeps.AddRange(await BuildRelationDependency(ontologyId, thingId, unidir, existingDependencies, validation: validation));
            if(thing.TryGetProperty("bidirectionals", out var bidir))
                relDeps.AddRange(await BuildRelationDependency(ontologyId, thingId, bidir, existingDependencies, false, validation));
            //TODO: Check attribute dependencies
            return new ThingDependency(thingId, types, relDeps);
        }

        /// <summary>
        /// Validates a Thing's Ontology defined dependencies.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The dientifier of the Thing.</param>
        /// <param name="thing">The Thing info in Json format.</param>
        /// <param name="existingDependencies">Set with the types that have already been checked.</param>
        /// <param name="validation">The Validation Object.</param>
        /// <returns></returns>
        private async Task ValidateThingDependencies(string ontologyId, string thingId, JsonElement thing, HashSet<string> existingDependencies, ValidationBody validation)
        {
            await BuildThingDependency(ontologyId, thingId, thing, existingDependencies, validation);
        }

        /// <summary>
        /// Obtains a Thing's dependencies of an Ontology.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns a ThingDependency record with its direct and indirect dependencies.
        /// </returns>
        public async Task<ThingDependency> GetThingDependecies(string ontologyId, string thingId)
        {
            var flatThing = await _dgraphService.GetDependenciesOfThingInOntology(ontologyId, thingId);
            return await BuildThingDependency(ontologyId, thingId, flatThing, new HashSet<string>());
        }

        /// <summary>
        /// Builds a record with a Relation's dependencies.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <param name="relJson">The Json with all the Thing's relations and their destiny Thing identifiers.</param>
        /// <param name="existingDependecies">Set with the identifiers of the Things that have already been checked.</param>
        /// <param name="unidir">OPTIONAL. Whether the relations obtained are unidirectional or not. By default: true.</param>
        /// <param name="validation">OPTIONAL. The Validation Object. By default: null, if not null, then it will validate its inner graph with information being obtained.</param>
        /// <returns>
        /// Returns a RelationDependecy record with all the necessary information.
        /// </returns>
        private async Task<List<RelationDependency>> BuildRelationDependency(string ontologyId, string thingId, JsonElement relJson, HashSet<string> existingDependecies, bool unidir = true, ValidationBody? validation = null)
        {
            var rels = new List<RelationDependency>();
            if(relJson.AsNode() is JsonObject obj)
            {
                //the plan is than in validation, for each relation (as of now), we check that the other thing: 
                //1. exists, 
                //2. if bidirectional, it also is related to this thing via the same relation
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(obj);
                foreach(var rel in dict!.Keys)
                {
                    //relName, bool true:unidirectional(relatedFrom), false:bidirectional(relatedTo)
                    if(rel is null)
                        continue;
                    var codependencies = new List<ThingDependency>();
                    foreach(var relThingNode in obj![rel] is JsonArray ? obj![rel]!.AsArray() : new JsonArray{obj![rel]!.DeepClone()})
                        if(relThingNode is JsonValue relThingId)
                        {
                            bool existing = true;
                            if(validation is not null)
                            {
                                var existingNodes = JsonSerializer.SerializeToNode(validation.Graph["@graph"]!.AsArray().Where(t=> t!["@type"]!.GetValue<string>() ==(thingId))) ?? new JsonArray();
                                existing = existingNodes.AsArray().Count()>0;
                                if(!existing)
                                    UpdateValidationLogs(validation, "general", $"There's a Thing of type {thingId} missing");
                                else
                                    await CheckRelationValidation(validation, ontologyId, rel, relThingId.GetValue<string>(), unidir, existingNodes);
                            }
                            if(!unidir && existing)
                                existingDependecies.Add(relThingId.GetValue<string>());
                            if(!thingId.Equals(relThingId.GetValue<string>()))
                                codependencies.Add(await BuildThingDependency(ontologyId, relThingId.GetValue<string>(), await _dgraphService.GetDependenciesOfThingInOntology(ontologyId, relThingId.GetValue<string>(), existingDependecies, validation), existingDependecies, validation));
                        }
                    rels.Add(new RelationDependency(rel, unidir, codependencies));
                }
            }
            return rels;
        }

        /// <summary>
        /// Validates the current nodes of the validation Object's inner graph and checks all necessary relations are defined as they should. 
        /// </summary>
        /// <param name="validation">The Validation Object.</param>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="relName">The name of the Relation.</param>
        /// <param name="dstThingType">The identifier of the Thing of the Ontology that defines the Relation's destiny type.</param>
        /// <param name="unidir">Whether the Relation is undiirectionar or bidirectional.</param>
        /// <param name="currentNode">The Json with the current nodes being validated.</param>
        /// <returns></returns>
        private async Task CheckRelationValidation(ValidationBody validation, string ontologyId, string relName, string dstThingType, bool unidir, JsonNode currentNode)
        {
            if(currentNode.AsArray().Count() == 0)                
                return;

            foreach(var nodeSrcThing in currentNode is JsonArray ? currentNode.AsArray() : new JsonArray { currentNode })
            {
                //we iterate through all things of the current type, as they all must satisfy the ontology type's dependencies
                if(nodeSrcThing!["@type"] is null)
                    continue;
                var thingId = nodeSrcThing!["@type"]!.GetValue<string>();
                var currentThingId = nodeSrcThing!["@id"]!.GetValue<string>();
                (var conflict, var correct) = await _dgraphService.ExistsInstanciatedThingOfOntology(ontologyId, nodeSrcThing!["@id"]!.GetValue<string>(), currentThingId);
                if(conflict)
                {
                    UpdateValidationLogs(validation, currentThingId, correct ? $"There is already a Thing with id {currentThingId} that is not of type {thingId}" : $"There is a Thing with id {currentThingId} that is type {thingId} but does not meet direct relational dependencies");
                    validation.CheckedThings.Add(thingId); //to say, we've already checked if a thing with the same id and type thingId and it came back true (if it's missing, possitive, if it's in the graph, bad (conflict))
                }
                
                if(nodeSrcThing![$"{ontologyId}:{relName}"] is null)
                {
                    UpdateValidationLogs(validation, currentThingId, $"It must have a relation {relName}");
                    continue;
                }
                var nodeDstThingsIds = nodeSrcThing![$"{ontologyId}:{relName}"] is JsonArray arr ? arr.Select(t => t!["@id"]!.GetValue<string>()).Where(id => validation.Graph!["@graph"]!.AsArray()!.Count(t => t!["@id"]!.GetValue<string>() == id)>0) : [nodeSrcThing![$"{ontologyId}:{relName}"]!["@id"]!.GetValue<string>()];
                //if it's not in the graph and it's not already instanciated, error
                if(nodeDstThingsIds.Count()==0)
                    UpdateValidationLogs(validation, currentThingId, $"It has a relation {relName} but is not related to another Thing");
                else{
                    //after having checked that the source thing has the relation, we check if the destiny Thing(s) exist(s)
                    nodeDstThingsIds = nodeDstThingsIds.Where(id => validation.Graph["@graph"]!.AsArray().Count(t=>t!["@type"]!.GetValue<string>()==dstThingType)>0);
                    if (nodeDstThingsIds.Count() == 0)
                    {
                        UpdateValidationLogs(validation, currentThingId, $"It has a relation {relName} but is not related to a Thing of type {dstThingType}");
                        continue;
                    }

                    int correctThings = 0;
                    var nodeDstThingNodes = (await Task.WhenAll(nodeDstThingsIds.Select(async id =>
                    {
                        if(validation.Graph["@graph"]!.AsArray().Count(t=> t!["@id"]!.GetValue<string>()==id) >= 1)
                        {
                            correctThings++;
                            return new{id, keep = true};
                        }else 
                        {
                            (conflict, correct) = await _dgraphService.ExistsInstanciatedThingOfOntology(ontologyId, dstThingType, id);
                            if(validation.CheckedThings.Contains(id) || conflict)
                            {
                                if(conflict && !correct)
                                    UpdateValidationLogs(validation, currentThingId, $"There is a Thing with id {id} that is type {dstThingType} but does not meet direct relational dependencies");
                                correctThings++;
                                validation.CheckedThings.Add(id);
                            }else
                                UpdateValidationLogs(validation, currentThingId, $"Is linked to an unexistent or invalid thing ({id}) by {relName} relation");
                            
                        }
                        return new{id, keep = false};
                            
                    }))).Where(x=>x.keep).Select(x=>x.id)
                    .Select(id => validation.Graph["@graph"]!.AsArray().Single(t=> t!["@id"]!.GetValue<string>()==id));

                    if(nodeDstThingNodes is null || nodeDstThingsIds.Count() != correctThings)
                        UpdateValidationLogs(validation, currentThingId, $"Relation {relName} has to link to another Thing of type {dstThingType}");
                    else if(!unidir)
                    {
                        //having checked all destiny Things exist, if the relation is bidirectional, we must check if said things are also related to the source Thing by the same relation                            
                        //We have to check bidirectionals because the query will omite them for efficiency
                        foreach(var nodeDstThing in nodeDstThingNodes)
                            if(nodeDstThing is null || nodeDstThing!["@id"] is null)
                                UpdateValidationLogs(validation, currentThingId, $"Is linked to an invalid thing by {relName} relation");
                            else if((nodeDstThing![$"{ontologyId}:{relName}"] is JsonArray arrNode && arrNode.Select(t => t!["@id"]!.GetValue<string>().Equals(currentThingId)).Count()==0) 
                            || nodeDstThing![$"{ontologyId}:{relName}"] is JsonObject obj && (obj["@id"] is null || !obj["@id"]!.GetValue<string>().Equals(currentThingId)))
                                UpdateValidationLogs(validation, nodeDstThing!["@id"]!.GetValue<string>(), $"Has relation {relName} but not linked to {currentThingId}");
                            //If it's already instanciated??????? supposedly it's not possible, they must coexist
                    }
                }
            }
        }        

        /// <summary>
        /// Validates a Thing.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The dientifier of the Thing.</param>
        /// <param name="typesValidated">Set with the types that have already been checked.</param>
        /// <param name="validation">The Valdiation Object.</param>
        /// <returns></returns>
        private async Task ValidateThing(string ontologyId, string thingId, HashSet<string> typesValidated, ValidationBody validation)
        {
            var flatThing = await _dgraphService.GetDependenciesOfThingInOntology(ontologyId, thingId, typesValidated);
            validation.CurrentThings=JsonSerializer.SerializeToNode(validation.Graph["@graph"]!.AsArray().Where(t => t!["@type"]!.GetValue<string>()!.Equals(thingId)));
            await ValidateThingDependencies(ontologyId, thingId, flatThing, typesValidated, validation);
        }

        /// <summary>
        /// Validates a given Graph of Things. 
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="subgraph">The Graph.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">Thrown if the Graph is in an invalid format or any Ontology defined dependencies have not been satisfied.</exception>
        public async Task ValidateGraph(string ontologyId, JsonElement subgraph)
        {
            ValidationBody validation = new ValidationBody(subgraph.AsNode()!);
            HashSet<string> typesValidated = new HashSet<string>();
            var graphNode = subgraph.GetProperty("@graph").AsNode()!.AsArray();
            foreach(var thing in graphNode)
                if(thing is null || thing is not JsonObject || thing["@type"] is null || thing["@type"] is not JsonValue)
                    throw new InvalidDataException("The provided Json Graph has at least one node with invalid format.");
                else if(!typesValidated.Contains(thing["@type"]!.GetValue<string>()))
                    await ValidateThing(ontologyId, thing["@type"]!.GetValue<string>(), typesValidated, validation);
            if(validation.Logs is not null)
                throw new InvalidDataException(JsonSerializer.Serialize(new {
                    Message = "The Subgraph provided leave some dependencies unnattended",
                    validation.Logs
                    },new JsonSerializerOptions{WriteIndented=true}));
        }

        //TODO: Method for constructing the payload for ThingsService

        /// <summary>
        /// Obtains the payload for instanciating a given Thing.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="type">The ontology's Thing's identifier.</param>
        /// <param name="id">The identifier of the instanciated Thing.</param>
        /// <param name="thing">The Thing in Json format.</param>
        /// <returns>Returns the Json of the payload.</returns>
        private async Task<JsonObject> GetThingPayloadForInstanciation(string ontologyId, string type, string id, JsonObject thing)
        {
            JsonObject props = await GetWOTThingProperties(ontologyId, type);
            var links = GetWOTThingLinks(thing);
            var payload = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = id,
                ["@id"] = id,
                ["title"] = thing["title"]?.GetValue<string>() ?? id,
                ["@type"] = type,
                ["properties"] = props,
                ["links"] = links
            };
            return payload;
        }

        /// <summary>
        /// Builds a Thing payload for its instanciation.
        /// </summary>
        /// <param name="thing">The Json of the Thing.</param>
        /// <returns>Returns the Json payload of the Thing.</returns>
        private JsonNode GetThingPayloadForInstanciation(JsonNode thing)
        {
            var thingId = thing["@type"]?.GetValue<string>();
            var links = GetWOTThingLinks(thing);
            var payload = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = thing["@id"]!.GetValue<string>(),
                ["title"] = "",
                ["properties"] = new JsonObject(),
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { },
                ["links"] = links,
                ["otv2:rules"] = thing["otv2:rules"]?.DeepClone() ?? new JsonObject(),
                ["otv2:subscribedEvents"] = thing["otv2:subscribedEvents"]?.DeepClone() ?? new JsonArray()
            };
            if(thingId is not null)
                payload["@type"] = thingId; 
            return payload;
        }

        /// <summary>
        /// Obtains the Web Of Things Links Relations from the thing.
        /// </summary>
        /// <param name="thing">The Thing in Json format.</param>
        /// <returns>Returns the JsonArray of the links in Web Of Things format.</returns>
        private JsonArray GetWOTThingLinks(JsonNode thing)
        {
            //Parts:
            //href: MANDATORY. @id (urn:something)
            //rel: The rel name: prefix:rel
            var links = new JsonArray();
            if(thing is JsonObject thingObj)
                foreach(KeyValuePair<string, JsonNode?> property in thingObj)
                    if(property.Value is not null && property.Value is not JsonValue)
                        foreach(var thingNode in property.Value is JsonObject propObj ? [propObj] : property.Value.AsArray())
                            if(thingNode is JsonObject propArrObj && propArrObj["@id"] is not null)
                                links.Add(new JsonObject
                                    {
                                        ["href"]=propArrObj!["@id"]!.GetValue<string>(),
                                        ["rel"] = property.Key
                                    });
                               
            return links;            
        }

        
        /// <summary>
        /// Obtains the default properties of a Thing from an Ontology in Web Of Things format.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>Returns the properties in Json and Web Of Things format.</returns>
        private async Task<JsonObject> GetWOTThingProperties(string ontologyId, string thingId)
        {
            var attrs = await _dgraphService.GetAttributeDictionaryOfThingInOntology(ontologyId, thingId);
            JsonObject properties = [];
            foreach((var key, var values) in attrs)
                foreach((var type, var stringValue) in values)
                {
                    object? value;
                    switch (type?.ToLower())
                    {
                        case "int":
                        case "integer":
                            value = int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? int.Parse(stringValue) : stringValue;
                            break;
                        case "float":
                        case "double":
                            value = double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? float.Parse(stringValue) : stringValue;
                            break;
                        case "bool":
                            value = bool.TryParse(stringValue, out _) ? bool.Parse(stringValue) : stringValue;
                            break;
                        default:
                            value = stringValue;
                            break;
                    }
                    var obj = new JsonObject
                    {
                        ["type"] = type ?? "string"
                    };

                    obj["default"] = value is null ? null : JsonValue.Create(value);
                    properties[key] = obj;
                }
            return properties;
        }

        public JsonObject GetTwinInstanciationPayLoad(string twinId)
        {
            return new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = twinId,
                ["title"] = "",
                ["properties"] = new JsonObject { },
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { }
            };
        }
    
        public async Task<string> CreateInstanciationTwin(string twinId)
        {
            var payload = GetTwinInstanciationPayLoad(twinId);
            var thingsResponse = await _thingsService.CreateThingAsync(twinId, payload);
            if(!thingsResponse)
                throw new Exception("Response from Things Service while creating Twin Thing was unsuccessful.");
            var dgraphResponse = await _dgraphService.AddThingAsync(ThingBuilder.BuildTwin(twinId));
            if(dgraphResponse is null || dgraphResponse.Uids is null || dgraphResponse.Uids.Count()==0)
                throw new Exception("Response from DGraph Service while creating Twin was unsuccessful.");
            return dgraphResponse.Uids.FirstOrDefault().Value;
        }

        /// <summary>
        /// Instanciates a Graph in both ThingsService and DGraph.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="graph">The Graph in Json format.</param>
        /// <param name="twinId">OPTIONAL. The identifier of the Twin.</param>
        /// <returns></returns>
        
        public async Task InstanciateThingGraph(string ontologyId, JsonArray graph, string? twinId = null)
        {
            //Iterate through the graph and get each payload and instanciate in DGraph if not existent
            //create the twin
            string twinUid = "";
            JsonArray payload = [];
            JsonArray thingsPayload = [];
            bool createTwin = !string.IsNullOrWhiteSpace(twinId);

            if(createTwin){
                twinUid = $"_:twinUid{twinId}";
                payload.Add(ThingBuilder.BuildTwin(twinId!, twinUid));
                thingsPayload.Add(GetTwinInstanciationPayLoad(twinId!));
            }
            //Fail if there is any id in the relations that's not in the graph

            var completeIdList = GetIdsFromGraph(graph); //including nested ones (relation objectives)
            var idList = graph.Select(thing => (Id: (thing!["@id"] ?? thing["id"])?.GetValue<string>(), Type: thing["@type"]?.GetValue<string>(), Graph: thing.AsObject())).ToList() ?? [];
            
            if(idList.Count == 0 || idList.Any(id => id.Id is null))
                throw new ArgumentException("At least one element in the graph provided does not have an identifier");
            else if(idList.Any(id => id.Type is null))
                throw new ArgumentException("At least one element in the graph provided does not have a type");
            if(completeIdList.Any(id => !idList.Any(triple => triple.Id == id)))
                throw new ArgumentException("At least one inner identifier in the graph is not defined in it");

            var typeTasks = idList.Select(t => t.Type).ToHashSet().Select(async type =>
            {
                if(!await _dgraphService.ExistsThingInOntologyByIdAsync(ontologyId, type!))
                    throw new KeyNotFoundException($"There is no Thing with id {type} in the ontology {ontologyId}");
                JsonObject thing = (await _dgraphService.GetThingInOntologyByIdAsync(ontologyId, type!))?.AsNode()?.AsObject() 
                    ?? throw new ArgumentNullException(paramName: "thing", message:$"The thing {type} from the ontology {ontologyId} cound not be fetched and got null");
                return (Key: type!, Value: thing);
            });

            var types = (await Task.WhenAll(typeTasks)).ToDictionary(t => t.Key, t=> t.Value) ?? throw new ArgumentNullException(paramName: "types");

            var idTasks = idList.Select(async id => 
            {
                try
                {
                    await _thingsService.GetThingAsync(id.Id!);
                    throw new InvalidOperationException("There's already a Thing with at least one of the identifiers in the provided");
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidOperationException("There's already a Thing with at least one of the identifiers in the provided");
                }
                catch(KeyNotFoundException){} //Good case, thing not found
                
                return new { Key = id.Id!, Value = (Type: id.Type!, Graph: id.Graph) };
            });
            
            var dict = (await Task.WhenAll(idTasks)).ToDictionary(t => t.Key, t=> t.Value) ?? throw new ArgumentNullException(paramName: "dict");

            var uids = await _dgraphService.GetUidsByThingIdsAsync(completeIdList);
            if(uids.Keys.Count>0)
                throw new InvalidOperationException($"The following ids already exist on DGraph: {string.Join(", ", uids.Keys)}");

            uids = completeIdList.ToDictionary(id => id, id => $"_:relativeUid{id}");
            var typeUids = await _dgraphService.GetUidsByThingIdsAsync(types.Keys);

            var generalTask = idList.Select(async triple => (Key: triple.Id, Relations: await _dgraphService.GetRelationsDictionaryOfThingInOntology(ontologyId, triple.Type!), Attributes: await _dgraphService.GetAttributeDictionaryOfThingInOntology(ontologyId, triple.Type!)));
            var generalDict = (await Task.WhenAll(generalTask)).ToDictionary(x => x.Key!, x => (x.Relations, x.Attributes));

            int relationCounter = 0;
            // int attrCounter = 0;

            foreach((var id, (var type, var graphObject)) in dict)
            {
                //Check that every relation defined in the graph exists in the ontology
                var relationsDict = generalDict[id].Relations;
                var attributesDict = generalDict[id].Attributes;
                if(createTwin)
                {
                    var thingPayload = ThingBuilder.BuildThing(id, typeUid: typeUids[type], twinUid: twinUid);
                    
                    // ---------> As of now, Twin's Things attributes are exclusively stored in the statestore
                    // var attrArray = new JsonArray();
                    // foreach((var attrKey, var attrList) in attributesDict)
                    //     foreach((var attrType, var attrValue) in attrList)
                    //     {
                    //         attrCounter++;
                    //         payload.Add(ThingBuilder.BuildAttribute(attrCounter, attrKey, attrType, attrValue));
                    //         attrArray.Add(new JsonObject{["uid"] = $"_:attr{attrCounter}"});
                    //     }
                    // if(attrArray.Count>0)
                    //     thingPayload["hasAttribute"] = attrArray;

                    thingPayload["uid"] = uids[id];
                    payload.Add(thingPayload);
                }
                thingsPayload.Add(await GetThingPayloadForInstanciation(ontologyId, type, id, graphObject));
                foreach ((var rel, var obj) in graphObject)
                {
                    bool omit = rel switch
                    {
                        "@id" or "id" or "@type" => true,
                        _ => false
                    };


                    if(omit)
                        continue;

                    if(string.IsNullOrWhiteSpace(rel) || !relationsDict.TryGetValue(rel, out var objectiveTypes))
                        throw new ArgumentException($"The thing {id}'s relation {rel} is not defined in the ontology");
                    JsonArray objArr = obj switch
                    {
                        JsonObject targetObject => new JsonArray(targetObject.DeepClone()),
                        JsonArray objArray => objArray,
                        _ => []
                    };
                    
                    foreach(var objEl in objArr)
                        if(objEl is JsonObject objElObj && ((objElObj!["@id"] ?? objElObj["id"]) ?? "") is JsonValue targetObjId && !string.IsNullOrWhiteSpace(targetObjId.GetString()))
                        {
                            var targetId = targetObjId.GetString()!;
                            var objType = dict[targetId].Type;
                            if(string.IsNullOrWhiteSpace(objType))
                                throw new ArgumentException($"The thing {targetId} doesn't have a type");
                            else if(!objectiveTypes.Contains(objType!))
                                throw new ArgumentException($"The thing {id}'s relation {rel} for the objective is not defined in the ontology");
                            //Correct relation, so add it to the payload.

                            //If we are creating a Twin, then create the dgraphpayload
                            if (createTwin)
                            {
                                var targetRelationsDict = generalDict[targetId].Relations;
                                bool bidir = targetRelationsDict.TryGetValue(rel, out var targets) && targets.Contains(id);

                                //If it's bidir and in the graph's it's not, throw an error --> when the target thing is processed that will be checked
                                //Therefore, if it's not already defined, define the bidirectional or unidirectional payload directly (not converting it)

                                bool alreadyAdded = bidir ? 
                                    GetBidirectionalRelationPayload(uids[id], uids[targetId], rel, payload) is not null :
                                    GetUnidirectionalRelationPayload(uids[id], uids[targetId], rel, payload) is not null;

                                relationCounter++;

                                if (!alreadyAdded)
                                    payload.Add(ThingBuilder.BuildRelation(rel, uids[targetId], sourceUid: uids[id], relCounter: relationCounter,
                                    bidir: bidir));
                            }                            
                        }
                }
                
            }

            if(createTwin)
                await _dgraphService.AddEntitiesAsync(payload);

            bool thingsResponse = await _thingsService.CreateThingsAsync(thingsPayload);

            if(!thingsResponse)
                throw new Exception("Things Instanciation failed in Things Service");
        }

        public async Task InstanciateThingGraph(JsonArray graph, string twinId, Dictionary<string, ThingDescription> thingDescriptions)
        {
            var twinUid = (await _dgraphService.GetUidsByThingIdsAsync([twinId]))[twinId];

            //Get Uid dicts
            Dictionary<string, string> uids = await _dgraphService.GetUidsByThingIdsAsync(thingDescriptions.Keys);

            foreach(var thingId in thingDescriptions.Keys.Where(id => !uids.TryGetValue(id, out _)))
                uids[thingId] = $"_:relativeUid{thingId}";

            JsonArray payload = [];

            int relationCounter = 0, targetCounter = 0;

            foreach(var thingId in thingDescriptions.Keys)
            {
                //Get Thing DGraph Payload (ThingBuilder)
                //Get Relation from the ThingDescriptions payloads 
                payload.AddRange(ThingBuilder.BuildPayloadWithLinks(thingDescriptions[thingId], twinUid, uids, ref relationCounter, ref targetCounter, uids[thingId], payload).Select(p => p!.DeepClone()));

                //Get Twin exclusive Relations payloads
                JsonObject graphThing = [];
                try
                {
                    graphThing = graph.Single(thing => (thing!["@id"]?.GetValue<string>() ?? thing!["id"]!.GetValue<string>())==thingId)!.AsObject();
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Something went wrong while accessing the graph thing {thingId}: {ex.Message}");
                }
                
                //before this function it's been controlled that every id in the graph exists, that every property has an id, etc
                foreach((var rel, var obj) in graphThing)
                {
                    JsonArray objArr = obj switch
                    {
                        JsonObject targetObject => new JsonArray(targetObject.DeepClone()),
                        JsonArray objArray => objArray,
                        _ => []
                    };
                    foreach(var objEl in objArr)
                        if(objEl is JsonObject objElObj && ((obj!["@id"] ?? obj["id"]) ?? "") is JsonValue targetObjId && !string.IsNullOrWhiteSpace(targetObjId.GetString()))
                        {
                            var targetId = targetObjId!.GetString()!;
                            if(!graph.Any(thing => (thing!["@id"]?.GetValue<string>() ?? thing!["id"]!.GetValue<string>()) == targetId))
                                throw new ArgumentException($"The relation {rel} in Thing {thingId} links to a Thing that does not exist ({targetObjId})");
                            else
                            {
                                //TODO: Better control so hasChild and hasPart cannot be bidirectional (only relatedTo edge)
                                bool bidir = IsRelationBidirectional(thingId, targetId, rel, thingDescriptions, graph);
                                
                                var unidirectionalPayload = GetUnidirectionalRelationPayload(uids[targetId], uids[thingId], rel, payload);
                                relationCounter++;
                                if(bidir && unidirectionalPayload is not null)
                                {
                                    ThingBuilder.TurnUnidirectionalRelationPayloadIntoBidirectional(uids[thingId], unidirectionalPayload);
                                }
                                else
                                {
                                    if (bidir)
                                    {
                                        var bidirectional = GetBidirectionalRelationPayload(uids[thingId], uids[targetId], rel, payload);
                                        if(bidirectional is not null)
                                            continue;
                                    }
                                    payload.Add(ThingBuilder.BuildRelation(rel, uids[targetId], sourceUid: uids[thingId], relCounter: relationCounter,
                                    bidir: bidir));
                                }
                            }
                        }
                }
            }

            //payload ready
            await _dgraphService.AddEntitiesAsync(payload);
        }

        #region Shape Oriented

        private string BuildShapeValidationReport(Report results)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Validation FAILED:\n");

            foreach (var res in results.Results)
            {
                // Focus node (what failed)
                string focusNode = res.FocusNode?.ToSafeString() ?? "(unknown node)";

                // Property path (what property failed)
                string path = res.ResultPath?.ToSafeString() ?? "(no path)";

                // Constraint type (minCount, datatype, etc.)
                string constraint = res.SourceConstraintComponent?.ToSafeString()
                                    ?? "(unknown constraint)";

                // Shape
                string shape = res.SourceShape?.ToSafeString() ?? "(unknown shape)";

                // Severity
                string severity = res.Severity?.ToSafeString() ?? "Violation";

                report.AppendLine($"• Node: {focusNode}");
                report.AppendLine($"  Shape: {shape}");
                report.AppendLine($"  Property: {path}");
                report.AppendLine($"  Constraint: {constraint}");
                report.AppendLine($"  Severity: {severity}");
                report.AppendLine($"  Message: {res.Message}");
                report.AppendLine();
            }

            return report.ToString();
        }

        /// <summary>
        /// Validates a Twin's Graph through a valid Shape Graph.
        /// </summary>
        /// <param name="twinId">The identifier of the twin.</param>
        /// <param name="graph">The Graph of the Twin.</param>
        /// <param name="shapeId">The identifier of the Shape Graph.</param>
        /// <returns>
        /// Returns null if the Twin's Graph validates the given Shape Graph.<br/>
        /// Returns a string describing the errors encountered if the validation failed. 
        /// </returns>
        /// <exception cref="InvalidDataException">Thrown if the given Graph is of bad format.</exception>
        /// <exception cref="Exception">Thrown if any of the intermediate values obtained are null or invalid.</exception>
        public async Task<string?> ValidateGraphThroughShapeGraph(string twinId, JsonElement graph, string shapeId)
        {
            if(graph.AsNode() is not JsonObject graphObj)
                throw new InvalidDataException("The provided graph is of bad format");
            JsonObject json = await _exportService.GetShapeGraphFlattenedJson(shapeId) ?? throw new Exception($"The recieved flattened Json of the {shapeId} shape Graph is null");
            JsonObject jsonLd = ExportService.GetJsonLDFromRegularJson(json, shapeId, true) ?? throw new Exception($"The obtained JsonLd from the {shapeId} Shape Graph is null");
            var shapeRDFgraph = FormatService.GetRDFGraphFromJson(jsonLd, shapeId, ld:true) ?? throw new Exception("The graph obtained is null");
            ShapesGraph shapeGraph = new ShapesGraph(shapeRDFgraph) ?? throw new Exception("The Shape Graph obtained is null");
            
            //The twin doesn't exist yet, i have to get the Graph from the JsonLD graph directly
            IGraph compound = FormatService.GetRDFGraphFromJson(graphObj, twinId, ld: true, twin:true);
            List<string> ontologies = await _dgraphService.GetOntologiesOfTwinAsync(twinId);
            foreach(string ontologyId in ontologies)
            {
                var ontologyJson = await _exportService.GetJsonWithNamespace(ontologyId, await _dgraphService.GetNamespacesInOntologyAsync(ontologyId) ?? null) ?? throw new Exception($"The recieved Json of the {ontologyId} Ontology is null");
                var ontologyGraph = FormatService.GetRDFGraphFromJson(ontologyJson, ontologyId) ?? throw new Exception($"The recieved Graph of the {ontologyId} Ontology is null");
                compound.Merge(ontologyGraph, true);
            }
            var results = shapeGraph.Validate(compound);
            if(results.Conforms)
                return null;

            //if it doesn't conform, return report
            return BuildShapeValidationReport(results);
        }  

        #endregion
    }
}