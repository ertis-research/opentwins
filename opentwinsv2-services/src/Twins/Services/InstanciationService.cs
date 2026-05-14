using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Api;
using Json.More;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Builders;
using OpenTwinsV2.Twins.Models;
using OpenTwinsV2.Twins.Services;
using Twins.Models;
using Twins.Services;
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
            return subgraph.Select(t => t!["@id"]!.GetValue<string>())
            .Count(id => subgraph.Count(t => t!["@id"]!.GetValue<string>()==id)>1)>0;
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
        /// <param name="thing">The Thing in Json format.</param>
        /// <returns>Returns the Json of the payload.</returns>
        private async Task<JsonNode> GetThingPayloadForInstanciation(string ontologyId, JsonNode thing)
        {
            var thingId = thing["@type"]!.GetValue<string>();
            var props = await GetWOTThingProperties(ontologyId, thingId);
            var links = GetWOTThingLinks(thing);
            var payload = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = thing["@id"]!.GetValue<string>(),
                ["title"] = "",
                ["@type"] = thingId,
                ["properties"] = props ?? new JsonObject(),
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { },
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
            var thingId = thing["@type"]!.GetValue<string>();
            var links = GetWOTThingLinks(thing);
            var payload = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = thing["@id"]!.GetValue<string>(),
                ["title"] = "",
                ["@type"] = thingId,
                ["properties"] = new JsonObject(),
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { },
                ["links"] = links
            };
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
                        //it is a relation (if JsonValue is an Attribute)
                        if(property.Value is JsonObject)
                            links.Add(new JsonObject
                            {
                                ["href"]=property.Value.AsObject()!["@id"]!.GetValue<string>(),
                                ["rel"] = property.Key
                            });
                        else if(property.Value is JsonArray arr)
                            foreach(var thingNode in arr)
                                links.Add(new JsonObject
                                {
                                    ["href"]=thingNode!.AsObject()!["@id"]!.GetValue<string>(),
                                    ["rel"] = property.Key
                                });
            return links;
            
        }

        //TODO: Make private when legacy method is deleted
        
        /// <summary>
        /// Obtains the default properties of a Thing from an Ontology in Web Of Things format.
        /// </summary>
        /// <param name="ontologyId">The identifier of the Ontology.</param>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>Returns the properties in Json and Web Of Things format.</returns>
        public async Task<JsonObject?> GetWOTThingProperties(string ontologyId, string thingId)
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

                    var valueElement = att.TryGetProperty("Attribute.value", out var valueJson) ? valueJson.ToString() : null;
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
    
        private async Task<string> CreateInstanciationTwin(string twinId)
        {
            var payload = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = twinId,
                ["title"] = "",
                ["properties"] = new JsonObject { },
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { }
            };
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
        //TODO: Only valid if it's only one type.
        
        public async Task InstanciateThingGraph(string ontologyId, JsonArray graph, string? twinId = null)
        {
            //Iterate through the graph and get each payload and instanciate in DGraph if not existent
            //create the twin
            string twinUid = "";
            if(!string.IsNullOrWhiteSpace(twinId))
                twinUid = await CreateInstanciationTwin(twinId);

            foreach(var thing in graph)
            {
                if(thing is null)
                    continue;
                var payload = await GetThingPayloadForInstanciation(ontologyId, thing);
                var thingId = thing!["@type"]!.GetValue<string>();
                var id = thing["@id"]!.GetValue<string>();
                if(!string.IsNullOrWhiteSpace(twinId))
                    await _dgraphService.CreateInstanciatedThingAsync(thingId, id, ontologyId: ontologyId, twinUid: twinUid);
                var thingsResponse = await _thingsService.CreateThingAsync(id, payload);
                if(!thingsResponse)
                    throw new Exception("Things Instanciation failed in Things Service");
            }
        }  

        /// <summary>
        /// Instanciates a Twin with a Thing Graph in both ThingsService and DGraph.
        /// </summary>
        /// <param name="graph">The Thing Graph to instanciate.</param>
        /// <param name="twinId">The identifier of the Twin.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown if any issue is encountered while instanciating the Things in thingsService.</exception>
        public async Task InstanciateThingGraph(JsonArray graph, string twinId)
        {
            string twinUid = await CreateInstanciationTwin(twinId);
            foreach(var thing in graph)
            {
                if(thing is null)
                    continue;

                var id = thing["@id"]!.GetValue<string>();
                var thingId = thing!["@type"]?.GetValue<string>() ?? "";
                Console.WriteLine($"Pruebo con thing id {id}");

                if(!await _dgraphService.ExistsThingByIdAsync(id))
                    await _dgraphService.CreateInstanciatedThingAsync(thingId, id, twinUid: twinUid);
                else //it already exists in dgraph, just create the link
                    await _dgraphService.AddThingToTwinAsync(id, twinId);
            }
            foreach(var thing in graph)
            {
                if(thing is null)
                    continue;
                var id = thing["@id"]!.GetValue<string>();
                try
                {
                    await _thingsService.GetThingAsync(id);
                    Console.WriteLine($"The Thing with id {id} already exists, so it will not be updated");
                    continue;
                }catch(KeyNotFoundException){} //only continues if the Thing does not exist

                var payload = GetThingPayloadForInstanciation(thing);
                var thingsResponse = await _thingsService.CreateThingAsync(id, payload);
                if(!thingsResponse)
                    throw new Exception("Things Instanciation failed in Things Service");
            }
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
            IGraph compound = FormatService.GetRDFGraphFromJson(graphObj, twinId, ld: true);
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