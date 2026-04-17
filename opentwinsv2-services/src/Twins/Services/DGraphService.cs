using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Api;
using Dapr;
using Dgraph4Net;
using Google.Protobuf;
using Grpc.Core;
using Json.More;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Builders;
using OpenTwinsV2.Twins.Models;
using Twins.Models;
using VDS.RDF;

namespace OpenTwinsV2.Twins.Services
{
    public class DGraphService : IDisposable
    {
        private readonly Dgraph4NetClient _client;
        private readonly Channel _channel;
        private readonly ILogger<DGraphService> _logger;

        private readonly ThingsService _thingsService;

        public DGraphService(IConfiguration configuration, ILogger<DGraphService> logger, ThingsService thingsService)
        {
            var dgraphUrl_gRPC = configuration["DGraph:URL_gRPC"] ?? throw new Exception("[ERROR] DGraph URL is not defined");
            // Vamos a usar este cliente porque el oficial no esta actualizado: https://github.com/schivei/dgraph4net
            _channel = new Channel(dgraphUrl_gRPC, ChannelCredentials.Insecure);
            _client = new Dgraph4NetClient(_channel);
            _logger = logger;
            _thingsService = thingsService;
        }

        #region Schema

        public async Task<Payload> InitSchemaAsync()
        {
            string schema = @"
                namespaceId: string @index(exact) .
                Namespace.name: string @index(term) .
                prefix: string @index(term) .
                uri: string .
                createdAt: datetime .

                thingId: string @index(exact) .
                name: string @index(term) .
                Namespace.createdAt: datetime .
                Thing.prefix: uid .

                hasType: [uid] @reverse .
                hasAttribute: [uid] @reverse .
                hasAction: [uid] @reverse .
                hasEvent: [uid] @reverse .

                ontologyId: string @index(exact) .
                Ontology.name: string @index(term) . 
                hasThing: [uid] @reverse .
                namespace: [uid] @reverse .
                defaultShapeGraph: [uid] @reverse .

                twins: [uid] @reverse .
                domains: [uid] @reverse .

                Attribute.key: string @index(term) .
                Attribute.type: string @index(term) .
                Attribute.value: string .
                Attribute.prefix: uid .

                Action.name: string @index(term) .
                Action.payload: string .

                Event.name: string @index(term) .
                Event.data: string .

                Relation.name: string @index(term) .
                Relation.createdAt: datetime .
                Relation.attributes: string .
                Relation.prefix: uid .

                hasPart: [uid] @reverse .
                hasChild: [uid] @reverse .
                relatedTo: [uid] @reverse .
                relatedFrom: [uid] @reverse .
                inheritsFrom: uid @reverse .

                domainId: string @index(exact) .
                Domain.name: string @index(term) .

                shapeId: string @index(exact) .
                Shape.name: string @index(term) .
                Shape.createdAt: datetime .
                shapes: [uid] @reverse .

                targetId: string @index(exact) .
                Target.prefix: uid .
                Target.name: string @index(term) .

                valueId: string @index(exact) .
                Value.type: string .
                value: string .

                nodeShapeId: string @index(exact) .
                NodeShape.name: string @index(term) .
                NodeShape.createdAt: datetime .
                NodeShape.prefix: uid .
                target: [uid] @reverse .
                properties: [uid] @reverse .
                defaultProperty: uid .
                description: string .

                path: uid .
                ShapeProperty.prefix: uid .
                constraints: [uid] @reverse .

                constraintId: string @index(exact) .
                ShapeConstraint.prefix: uid .

                minCount: [uid] .
                maxCount: [uid] .

                datatype: [uid] .
                nodeKind: [uid] .
                class: [uid] .
                node: [uid] .    

                in: [uid] .
                hasValue: [uid] .

                and: [uid] .
                or: [uid] .
                not: uid .
                xone: [uid] .

                equals: [uid] .
                disjoint: [uid] .    
                lessThan: [uid] .
                lessThanOrEquals: [uid] .

                GenericConstraint.value: [uid] .

                type Shape{
                    shapeId
                    name
                    createdAt
                    shapes
                }

                type Reference{
                    targetId
                    Target.prefix
                    Target.name
                }

                type Value{
                    valueId
                    Value.type
                    value
                }

                type NodeShape{
                    nodeShapeId
                    NodeShape.name
                    NodeShape.createdAt
                    NodeShape.prefix
                    target
                    properties
                    defaultProperty
                    description
                }

                type ShapeProperty{
                    path
                    description
                    constraints
                    ShapeProperty.prefix
                }

                type CardinalityConstraint {
                    constraintId
                    ShapeConstraint.prefix
                    minCount
                    maxCount
                }

                type StructureConstraint{
                    constraintId
                    ShapeConstraint.prefix
                    datatype
                    nodeKind
                    class
                    node
                }

                type SetConstraint{
                    constraintId
                    ShapeConstraint.prefix
                    in
                    hasValue
                }

                type LogicalConstraint{
                    constraintId
                    ShapeConstraint.prefix
                    and
                    or
                    not
                    xone
                }

                type ValueConstraint{
                    constraintId
                    ShapeConstraint.prefix
                    equals
                    disjoint
                    lessThan
                    lessThanOrEquals
                }

                type GenericConstraint{
                    constraintId
                    ShapeConstraint.prefix
                    GenericConstraint.value
                }

                type Namespace {
                    namespaceId
                    prefix
                    uri
                    Namespace.name
                    Namespace.createdAt
                }

                type Thing {
                    thingId
                    name
                    createdAt
                    hasType
                    hasAttribute
                    hasAction
                    hasEvent
                    twins
                    domains
                    Thing.prefix
                    inheritsFrom
                }

                type Ontology {
                    ontologyId
                    Ontology.name
                    namespace
                    createdAt
                    hasThing
                    defaultShapeGraph
                } 

                type Twin {
                    thingId
                    name
                    createdAt
                    hasType
                    hasAttribute
                    hasAction
                    hasEvent
                    twins
                    domains
                }

                type RealObject {
                    thingId
                    name
                    createdAt
                    hasType
                    hasAttribute
                    hasAction
                    hasEvent
                    twins
                    domains
                }

                type Resource {
                    thingId
                    name
                    createdAt
                    hasType
                    hasAttribute
                    hasAction
                    hasEvent
                    twins
                    domains
                }

                type Attribute {
                    Attribute.key
                    Attribute.type
                    Attribute.value
                    Attribute.prefix
                }

                type Action {
                    Action.name
                    Action.payload
                }

                type Event {
                    Event.name
                    Event.data
                }

                type Relation {
                    Relation.name
                    Relation.createdAt
                    Relation.attributes
                    Relation.prefix
                    relatedTo
                    relatedFrom
                    hasPart
                    hasChild
                }
            ";

            var op = new Operation { Schema = schema };
            return await _client.Alter(op);
        }

        #endregion

        #region Auxiliars

        public void Dispose()
        {
            _channel?.ShutdownAsync().Wait();
        }

        public Dgraph4NetClient GetClient() => _client;

        public async Task<Payload> DropAllAsync()
        {
            return await _client.Alter(new Operation { DropAll = true });
        }

        public async Task<string> GetEverythingAsync()
        {
            string query = @"
            {
                all(func: has(thingId), first: 1000) {
                    uid
                    dgraph.type
                    expand(_all_) {
                        uid
                        dgraph.type
                        expand(_all_)
                    }
                }
            }";

            var response = await _client.NewTransaction().Query(query);
            return response.Json.ToStringUtf8();
        }

        private string ListNQuadsToMutationFormat(List<string> nquads)
        {
            StringBuilder res = new StringBuilder();
            foreach (var triple in nquads)
            {
                res.Append(triple);
                res.Append('\n');
            }
            return res.ToString();
        }

        public async Task<Response> AddNQuadTripleAsync(List<string> nquads)
        {
            var txn = _client.NewTransaction();
            try
            {
                var triples = ListNQuadsToMutationFormat(nquads);

                var mutation = new Mutation
                {
                    SetNquads = ByteString.CopyFromUtf8(triples)
                };
                var response = await txn.Mutate(mutation);
                await txn.Commit();
                foreach (var kv in response.Uids)
                {
                    Console.WriteLine($"{kv.Key} => {kv.Value}");
                }
                return response;
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        private List<string> getUidListFromRootJSON(JsonDocument doc)
        {
            var uids = new List<string>();
            if (doc.RootElement.TryGetProperty("ontologies", out JsonElement ontologies))
            {
                foreach (var ontology in ontologies.EnumerateArray())
                {
                    //collect all uids and add it to the list
                    if (ontology.TryGetProperty("uid", out var ontUid))
                        uids.Add(ontUid.ToString());

                    if(ontology.TryGetProperty("namespace", out var namespaces))
                    {
                        
                        foreach(var ns in namespaces.EnumerateArray()){
                            if (ns.TryGetProperty("uid", out var nsUid))
                                uids.Add(nsUid.ToString());
                        }
                    }

                    if (ontology.TryGetProperty("hasThing", out var things))
                    {
                        //iterate through all posible relations of things
                        foreach (var thing in things.EnumerateArray())
                        {
                            if (thing.TryGetProperty("uid", out var thingUid))
                                uids.Add(thingUid.ToString());
                            if (thing.TryGetProperty("~relatedTo", out var rels))
                            {
                                foreach (var rel in rels.EnumerateArray())
                                {
                                    if (rel.TryGetProperty("uid", out var relUid))
                                        uids.Add(relUid.ToString());
                                }
                            }
                            if (thing.TryGetProperty("hasAttribute", out var attributes))
                            {
                                foreach (var attr in attributes.EnumerateArray())
                                {
                                    if (attr.TryGetProperty("uid", out var attrUid))
                                        uids.Add(attrUid.ToString());
                                }
                            }
                            if (thing.TryGetProperty("~hasType", out var types))
                            {
                                foreach (var type in types.EnumerateArray())
                                {
                                    if (type.TryGetProperty("uid", out var typeUid))
                                        uids.Add(typeUid.ToString());
                                }
                            }
                        }
                    }

                }
            }
            return uids;
        }

        public async Task<Response> AddEntitiesAsync(JsonArray entities)
        {
            var txn = _client.NewTransaction();
            try
            {
                var json = JsonSerializer.Serialize(entities);
                _logger.LogDebug("DGraph mutation JSON: {Json}", json);

                var mutation = new Mutation
                {
                    SetJson = ByteString.CopyFromUtf8(json)
                };
                var response = await txn.Mutate(mutation);
                await txn.Commit();
                return response;
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<Response> DeleteEntitiesAsync(JsonArray entities)
        {
            var txn = _client.NewTransaction();
            try
            {
                var json = JsonSerializer.Serialize(entities);
                _logger.LogInformation("DGraph delete JSON: {Json}", json);

                var mutation = new Mutation
                {
                    DeleteJson = ByteString.CopyFromUtf8(json)
                };
                var response = await txn.Mutate(mutation);
                await txn.Commit();
                return response;
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        private static IEnumerable<string> GetUidFromJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.NameEquals("uid") && prop.Value.ValueKind == JsonValueKind.String)
                            yield return prop.Value.GetString() ?? "";

                        foreach (var value in GetUidFromJsonElement(prop.Value))
                            yield return value;
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        foreach (var value in GetUidFromJsonElement(item))
                            yield return value;
                    }
                    break;
            }
        }

        private (int, int, int, string) NormalizePaginationParameters(int page, int pageSize, string? searchTerm, string filterField)
        {
            if (page < 1) 
                page = 1;
            if (pageSize < 1) 
                pageSize = 10;

            var offset = (page - 1) * pageSize;

            searchTerm = searchTerm?.Trim();
            var filter = "";
            if(!string.IsNullOrWhiteSpace(searchTerm))
                filter = $"@filter(regexp({filterField}, /.*{searchTerm}.*/i))";

            return (page, pageSize, offset, filter);
        }

        private JsonElement FlattenJsonElement<T>(JsonElement json, string parentProperty, string innerProperty, string? groupbyClause = null)
        {
            JsonNode newJson = json.AsNode()!.DeepClone();
            if(!json.TryGetProperty(parentProperty, out var propVal))
                return JsonSerializer.Deserialize<JsonElement>(newJson);
            var cleanVal = propVal.EnumerateArray().Select(item => item.GetProperty(innerProperty).AsNode()!.GetValue<T>()).Where(value => value is not null).ToList();
            if (cleanVal.Count > 1)
            {
                newJson[parentProperty] = JsonSerializer.SerializeToNode(cleanVal);
            }
            else
            {
                newJson[parentProperty] = JsonSerializer.SerializeToNode(cleanVal.Single());
            }
            return JsonSerializer.Deserialize<JsonElement>(newJson);
        }

        private JsonElement FlattenJsonElementArray<T>(JsonElement jsonArr, string parentProperty, string innerProperty, string? groupByClause = null, T? reflexiveFallback=default) where T:notnull
        {
            //flattened
            var query = jsonArr.EnumerateArray().Select(item => new
            {
                Key = !string.IsNullOrWhiteSpace(groupByClause) && item.TryGetProperty(groupByClause, out var key) && key.AsNode() is JsonValue val ? val.GetValue<string>() : null,
                Value = item.TryGetProperty(parentProperty, out var parValue) ? (parValue[0].TryGetProperty(innerProperty, out var innerVal) ? innerVal.Deserialize<T>() : default) : item.TryGetProperty("isReflexive", out var reflexiveClause) && reflexiveClause.AsNode()!.GetValue<bool>() == true ? JsonSerializer.SerializeToNode(reflexiveFallback).Deserialize<T>() : default
            }).Where(x => x.Value is not null && x.Key is not null && x.Key.ToString() is not null);

            if (string.IsNullOrWhiteSpace(groupByClause))
                return JsonSerializer.SerializeToElement(query.Select(x=>x.Value).ToList());

            JsonObject finalRes = new JsonObject();

            var grouped = query.GroupBy(x => x.Key).ToDictionary(
                group =>group.Key!.ToString()!,
                group =>
                {
                    var values = group.Select(x => x.Value).ToList();
                    return values.Count > 1 ? JsonSerializer.SerializeToNode(values) : JsonSerializer.SerializeToNode(values.First());
                }
            );
            
            return JsonSerializer.SerializeToElement(grouped);
        }

        private JsonElement IncorporateNewJsonElement(JsonElement parent, JsonElement child, string property)
        {
            JsonNode newJson = parent.AsNode()!.DeepClone();
            newJson[property] = child.AsNode()!.DeepClone();
            return JsonSerializer.Deserialize<JsonElement>(newJson);
        }


        // public async Task<JsonObject> GetFromUid(string uid)
        // {
            
        // }

        #endregion

        #region Things

        public async Task<Response> AddThingAsync(JsonObject entity)
        {
            var txn = _client.NewTransaction();
            try
            {
                Console.WriteLine(JsonSerializer.Serialize(entity));
                var mutation = new Mutation
                {
                    SetJson = ByteString.CopyFromUtf8(JsonSerializer.Serialize(entity))
                };
                var response = await txn.Mutate(mutation);
                await txn.Commit();
                return response;
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<Response> DeleteThingAsync(string thingId)
        {
            if (string.IsNullOrWhiteSpace(thingId))
                throw new ArgumentException("ThingId cannot be null or empty", nameof(thingId));

            using var txn = _client.NewTransaction();

            var query = $@"
            query {{
                q(func: eq(thingId, ""{thingId}"")) {{
                    uid
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();
            var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("q");

            if (arr.GetArrayLength() == 0)
                throw new KeyNotFoundException($"ThingId {thingId} not found");

            var uid = arr[0].GetProperty("uid").GetString();

            var mu = new Mutation
            {
                DelNquads = ByteString.CopyFromUtf8($"<{uid}> * * ."),
                CommitNow = true
            };

            return await txn.Mutate(mu);
        }

        private async Task<List<string>> GetRelationUidsByThingAsync(string thingUid)
        {
            var txn = _client.NewTransaction();
            try
            {
                var query = $@"
                {{
                    relations(func: type(Relation)) @filter(uid_in(relatedTo, {thingUid})) {{
                        uid
                    }}
                }}";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("relations", out var relArray))
                    return new List<string>();

                return [.. relArray.EnumerateArray().Select(r => r.GetProperty("uid").GetString()!)];
            }
            finally
            {
                await txn.DisposeAsync();
            }
        }

        public async Task<bool> ExistsThingByIdAsync(string thingId)
        {
            var query = $@"
            {{
                exists(func: eq(thingId, ""{thingId}"")) {{
                    uid
                }}
            }}";

            var response = await _client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("exists", out var existsArray) && existsArray.ValueKind == JsonValueKind.Array)
            {
                return existsArray.GetArrayLength() > 0;
            }

            return false;
        }

        #endregion

        #region Twins

        public async Task<PagedResult<JsonElement>> GetAllTwinsAsync(int page, int pageSize, string? searchTerm)
        {
            
            (page, pageSize, int offset, string filter) = NormalizePaginationParameters(page, pageSize, searchTerm, "thingId");

            var txn = _client.NewTransaction();
            
            try
            {
                var query = $@"
                {{
                    totalCount(func: type(Twin)) {filter}{{
                        count(uid)
                    }}

                    twins(func: type(Twin), first:{pageSize}, offset: {offset}, orderasc:thingId) {filter}{{
                        thingId
                        countThings: count(~twins)
                        thingsOfTwin: ~twins{{
                            name
                        }}
                    }}
                }}";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();
                var doc = JsonDocument.Parse(json);

                var totalCount = doc.RootElement.GetProperty("totalCount")[0].GetProperty("count").GetInt32();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                
                
                var result = new List<JsonElement>();

                if(doc.RootElement.TryGetProperty("twins", out var twins))
                {
                    foreach(var twin in twins.EnumerateArray())
                        result.Add(FlattenJsonElement<string>(twin, "thingsOfTwin", "name"));
                }

                return new PagedResult<JsonElement>(result, totalCount, page, pageSize, totalPages);
            }
            finally
            {
                await txn.DisposeAsync();
            }
        }

        public async Task<bool> ThingBelongsToTwinAsync(string twinId, string thingId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                twin as var(func: eq(thingId, ""{twinId}""))

                thing(func: eq(thingId, ""{thingId}"")) @filter(uid_in(twins, uid(twin))) {{
                    uid
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("thing", out JsonElement thingArray))
                return false;

            return thingArray.GetArrayLength() > 0;
        }

        public async Task<bool> ExistsTwinAsync(string twinId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var query = $@"
                {{
                    twin(func: eq(thingId, ""{twinId}"")) @filter(type(Twin)){{
                        uid
                        thingId
                    }}
                }}";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if(!root.TryGetProperty("twin", out var twinArr))
                    return false;
                
                return twinArr.AsNode()!.AsArray().Count==1;
            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<List<JsonElement>> GetThingsInTwinAsync(string twinId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                things(func: eq(thingId, ""{twinId}"")) {{
                    ~twins {{
                        uid
                        thingId
                        name
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Acceder a things[0]["~twins"]
            if (!root.TryGetProperty("things", out JsonElement thingsArray) || thingsArray.GetArrayLength() == 0)
                return [];

            var twinsProp = thingsArray[0].GetProperty("~twins");
            var twins = JsonSerializer.Deserialize<List<JsonElement>>(twinsProp.GetRawText());

            return twins ?? [];
        }

        // Este metodo y el anterior se repiten, hay que refactorizar!!!!!! que pereza (es que el converter va con este metodo)
        public async Task<string?> GetThingsInTwinNQUADSAsync(string twinId)
        {
            string query = $@"
            {{
                twin as var(func: eq(thingId, ""{twinId}""))

                things(func: uid(twin)) {{
                    ~twins {{
                        uid
                        thingId
                        name
                        createdAt

                        hasType {{ uid expand(_all_) }}
                        hasAttribute {{ uid expand(_all_) }}
                        hasAction {{ uid expand(_all_) }}
                        hasEvent {{ uid expand(_all_) }}
                        domains {{ uid expand(_all_) }}

                        ~relatedTo {{  
                            uid 
                            dgraph.type 
                            Relation.name 
                            Relation.attributes
                            relatedTo {{ uid thingId name }}
                            hasChild {{ uid thingId name }}
                            hasPart {{ uid thingId name }}
                        }}
                    }}
                }}
            }}";

            _logger.LogDebug("Executing Dgraph query for twinId={TwinId}", twinId);

            using var txn = _client.NewTransaction();

            var res = await txn.Query(query);
            var rawJson = res.Json.ToStringUtf8();

            _logger.LogDebug(
                "Query completed. Received {Length} characters of JSON for twinId={TwinId}",
                rawJson.Length, twinId
            );

            return rawJson;
        }

        public async Task<JsonElement?> GetThingInTwinByIdAsync(string twinId, string thingId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                twin as var(func: eq(thingId, ""{twinId}""))

                thing(func: eq(thingId, ""{thingId}"")) @filter(uid_in(twins, uid(twin))) {{
                    uid
                    thingId
                    name
                    createdAt

                    hasType {{
                        uid
                        name
                    }}
                    hasAttribute {{
                        Attribute.key
                        Attribute.type
                        Attribute.value
                    }}
                    hasAction {{
                        Action.name
                        Action.payload
                    }}
                    hasEvent {{
                        Event.name
                        Event.data
                    }}
                    domains {{
                        uid
                        domainId
                        Domain.name
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("thing", out JsonElement thingArray) || thingArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(thingArray[0].GetRawText());
        }

        //TODO refactor
        public async Task<JsonElement?> GetThingInTwinByIdForJsonAsync(string twinId, string thingId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                things as var(func: eq(thingId, ""{thingId}"")) @cascade {{
                    twins @filter(eq(thingId, ""{twinId}""))
                }}

                thing(func: uid(things)){{
                    thingId
                    name
                    createdAt
                    hasType{{
                        thingId
                        name
                        Thing.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                    }}
                    Thing.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                    hasAttribute{{
                        Attribute.key
                        Attribute.value
                        Attribute.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                    }}
                    ~relatedTo @filter(not has(relatedFrom)){{
                        uid
                        Relation.name
                        Relation.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                        relatedTo @filter(NOT uid(things)){{
                            name
                            thingId
                            Thing.prefix{{
                                namespaceId
                                prefix
                                uri
                            }}
                        }}
                        hasChild{{
                            name
                            thingId
                            Thing.prefix{{
                                namespaceId
                                prefix
                                uri
                            }}
                        }}
                    }}

                    ~relatedFrom {{
                        uid
                        Relation.name
                        Relation.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                        relatedTo{{
                            name
                            thingId
                            Thing.prefix{{
                                namespaceId
                                prefix
                                uri
                            }}
                        }}
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("thing", out JsonElement twinArray) || twinArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(twinArray[0].GetRawText());
        }

        public async Task<Response> AddThingToTwinAsync(string thingId, string twinId)
        {
            var uids = await GetUidsByThingIdsAsync([twinId, thingId]);
            if (!uids.TryGetValue(thingId, out var thingUid) || !uids.TryGetValue(twinId, out var twinUid)) throw new KeyNotFoundException("Twin or Thing not found");

            var mutation = new
            {
                uid = thingUid,
                twins = new[]
                {
                new { uid = twinUid }
            }
            };

            var mu = new Mutation
            {
                SetJson = ByteString.CopyFromUtf8(JsonSerializer.Serialize(mutation))
            };

            using var txn = _client.NewTransaction();
            var response = await txn.Mutate(mu);
            await txn.Commit();

            return response;

        }

        public async Task<Response> RemoveThingFromTwinAsync(string thingId, string twinId)
        {
            var uids = await GetUidsByThingIdsAsync([twinId, thingId]);
            if (!uids.TryGetValue(thingId, out var thingUid) || !uids.TryGetValue(twinId, out var twinUid)) throw new KeyNotFoundException("Twin or Thing not found");
            // 1. Eliminar la relación contains entre Twin y Thing
            var txn = _client.NewTransaction();

            var deleteJson = $@"
            [
                {{
                    ""uid"": ""{thingUid}"",
                    ""twins"": [ {{ ""uid"": ""{twinUid}"" }} ]
                }},
                {{
                    ""uid"": ""{twinUid}"",
                    ""twins"": [ {{ ""uid"": ""{thingUid}"" }} ]
                }}
            ]";

            var mutation = new Mutation
            {
                DeleteJson = ByteString.CopyFromUtf8(deleteJson)
            };

            try
            {
                var response = await txn.Mutate(mutation);
                await txn.Commit();
                return response;
            }
            catch (Exception ex)
            {
                await txn.DisposeAsync();
                throw new Exception("Error removing thing: " + ex.Message);
            }
        }

        public async Task<string?> GetTwinUidAsync(string twinId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var query= $@"
                {{
                    twin(func: eq(thingId, ""{twinId}"")) @filter(type(Twin)){{
                        uid
                    }}
                }}
                ";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if(!(root.TryGetProperty("twin", out var twin) && twin.TryGetProperty("uid", out var uid)))
                    return null;
                return uid.GetString();
            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        #endregion

        #region Ontologies

        public async Task<PagedResult<JsonElement>> GetAllOntologiesIdsAsync(int page, int pageSize, string? searchTerm)
        {
            (page, pageSize, int offset, string filter) = NormalizePaginationParameters(page, pageSize, searchTerm, "ontologyId");
            var txn = _client.NewTransaction();
            try
            {
                var query = $@"
                    {{
                        totalCount(func: type(Ontology)) {filter}
                        {{
                            count(uid)
                        }}

                        ontologies(func: type(Ontology), first:{pageSize}, offset: {offset}, orderasc:ontologyId) {filter}{{
                            ontologyId
                            countThings: count(hasThing)
                            thingsOfOntology: hasThing{{
                                thingId
                            }}
                        }}
                    }}     
                ";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();
                var doc = JsonDocument.Parse(json);

                var totalCount = doc.RootElement.GetProperty("totalCount")[0].GetProperty("count").GetInt32();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var result = new List<JsonElement>();
                if(doc.RootElement.TryGetProperty("ontologies", out var ontologies))
                {
                    foreach(var ontology in ontologies.EnumerateArray())
                        result.Add(FlattenJsonElement<string>(ontology, "thingsOfOntology", "thingId"));
                }
                return new PagedResult<JsonElement>(result, totalCount, page, pageSize, totalPages);
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        } 

        private async Task<string?> GetOntologyUid(string ontologyId)
        {
            using var txn = _client.NewTransaction();
            var query = $@"
            {{
                ontology(func: eq(ontologyId, ""{ontologyId}"")){{
                    uid
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if(!root.TryGetProperty("ontology", out var ontology))
                return null;

            return ontology.EnumerateArray().Select(s => s.TryGetProperty("uid", out var uid) ? uid.GetString() : null).FirstOrDefault(uid => uid is not null);
        }

        public async Task<List<string>> GetAllThingFromOntologyNodesUidAsync(string ontologyId, string thingId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var query = $@"
                    {{
                        ontology as var (func: eq(ontologyId, ""{ontologyId}""))
                        thing as var (func: eq(thingId, ""{thingId}""))

                        ontologies(func: uid(ontology)){{
                            hasThing @filter(uid(thing)){{
                                uid
                                hasAttribute{{
                                    uid
                                }}
                                ~relatedTo{{
                                    uid
                                }}
                                ~hasType{{
                                    uid
                                }}
                            }}
                        }}
                    }}     
                ";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                // var root = doc.RootElement;

                return getUidListFromRootJSON(doc);
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }
        
        public async Task<List<string>> GetAllOntologyRelatedNodesUidAsync(string ontologyId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var query = $@"
                    {{
                        ontology as var (func: eq(ontologyId, ""{ontologyId}""))

                        ontologies(func: uid(ontology)){{
                            uid
                            namespace{{
                                uid
                            }}
                            hasThing{{
                                uid
                                hasAttribute{{
                                    uid
                                }}
                                inheritsFrom{{
                                    uid
                                }}
                                ~relatedTo{{
                                    uid
                                }}
                                ~hasType{{
                                    uid
                                }}
                            }}
                        }}
                    }}     
                ";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                // var root = doc.RootElement;

                return getUidListFromRootJSON(doc);
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<Response> DeleteByOntologyId(string ontologyId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var uids = GetAllOntologyRelatedNodesUidAsync(ontologyId).Result;
                Console.WriteLine(uids.Count);

                var deleteObjects = new List<Dictionary<string, string>>();

                foreach (var uid in uids)
                {
                    // Each object = one node to delete
                    deleteObjects.Add(new Dictionary<string, string> { { "uid", uid } });
                }

                // Serialize to JSON
                var deleteJson = JsonSerializer.Serialize(deleteObjects);
                var mutation = new Mutation
                {
                    DeleteJson = ByteString.CopyFromUtf8(deleteJson)
                };

                try
                {
                    var response = await txn.Mutate(mutation);
                    await txn.Commit();
                    return response;
                }
                catch (Exception ex)
                {
                    await txn.DisposeAsync();
                    throw new Exception("Error removing ontology: " + ex.Message);
                }

            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }
        
        public async Task<Response> DeleteByOntologyIdAndThingId(string ontologyId, string thingId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var uids = GetAllThingFromOntologyNodesUidAsync(ontologyId, thingId).Result;

                var deleteObjects = new List<Dictionary<string, string>>();

                foreach (var uid in uids)
                {
                    // Each object = one node to delete
                    deleteObjects.Add(new Dictionary<string, string> { { "uid", uid } });
                }

                // Serialize to JSON
                var deleteJson = JsonSerializer.Serialize(deleteObjects);
                var mutation = new Mutation
                {
                    DeleteJson = ByteString.CopyFromUtf8(deleteJson)
                };

                try
                {
                    var response = await txn.Mutate(mutation);
                    await txn.Commit();
                    return response;
                }
                catch (Exception ex)
                {
                    await txn.DisposeAsync();
                    throw new Exception("Error removing thing from ontology: " + ex.Message);
                }

            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<bool> ThingBelongsToOntologyAsync(string ontologyId, string thingId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                ontologies(func: eq(ontologyId, ""{ontologyId}"")) {{
                    uid
                    hasThing @filter(eq(thingId, ""{thingId}"")) {{
                        uid
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ontologies", out JsonElement ontologiesArray))
                return false;

            if (ontologiesArray.GetArrayLength() == 0)
                return false;

            if (!ontologiesArray[0].TryGetProperty("hasThing", out JsonElement thingArray))
                return false;

            return thingArray.GetArrayLength() > 0;
        }

        public async Task<Dictionary<string, string>> GetUidsByThingIdsAsync(IEnumerable<string> thingIds)
        {
            var ids = thingIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (ids == null || ids.Count == 0) return [];

            var joinedIds = string.Join("\", \"", ids);

            var query = $@"
            {{
                node(func: eq(thingId, [""{joinedIds}""])) {{
                    uid
                    thingId
                }}
            }}";

            using var txn = _client.NewTransaction();
            var response = await txn.Query(query);
            using var doc = JsonDocument.Parse(response.Json.ToStringUtf8());

            return doc.RootElement
                    .GetProperty("node")
                    .EnumerateArray()
                    .Where(e => e.TryGetProperty("thingId", out _) && e.TryGetProperty("uid", out _))
                    .ToDictionary(
                        e => e.GetProperty("thingId").GetString()!,
                        e => e.GetProperty("uid").GetString()!
                    );
        }

        public async Task<bool> ExistsOntologyByIdAsync(string ontologyId)
        {
            var query = $@"{{
                exists(func: eq(ontologyId, ""{ontologyId}"")){{
                    uid
                }}
            }}";

            var response = await _client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("exists", out var existsArray) && existsArray.ValueKind == JsonValueKind.Array)
            {
                return existsArray.GetArrayLength() > 0;
            }

            return false;
        }

        public async Task<bool> ExistsThingInOntologyByIdAsync(string ontologyId, string thingId)
        {
            var query = $@"
            {{
                ontology as var(func: eq(ontologyId, ""{ontologyId}""))

                exists(func: eq(thingId, ""{thingId}"")) @cascade{{
                     uid
    		         thingId
    		        ~hasThing @filter(uid(ontology))
                }}
            }}";

            var response = await _client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("exists", out var existsArray) && existsArray.ValueKind == JsonValueKind.Array)
            {
                return existsArray.GetArrayLength() > 0;
            }

            return false;
        }

        public async Task<JsonElement> GetThingsInOntologyAsync(string ontologyId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                ontologies(func: eq(ontologyId, ""{ontologyId}"")) {{
                    defaultShapeGraph{{
                        shapeId
                    }}
                    things: hasThing{{
                        uid
                        name
                        thingId
                        inheritsFrom{{
                            thingId
                        }}
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ontologies", out JsonElement ontologies) || ontologies.GetArrayLength() == 0)
            {
                return new JsonElement();
            }
            return ontologies[0].Deserialize<JsonElement>();
        }

        public async Task<List<string>> GetOntologiesOfTwinAsync(string twinId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                things(func: eq(thingId, ""{twinId}"")) {{
                    ~hasThing{{
                        ontologyId
                    }}
                    ~twins {{
                        uid
                        ~hasThing{{
                            ontologyId
                        }}
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Acceder a things[0]["~twins"]
            if (!root.TryGetProperty("things", out JsonElement thingsArray) || thingsArray.GetArrayLength() == 0)
                return [];

            var ontologyIds = new HashSet<string>();
            JsonElement ontologyId;
            if(thingsArray[0].TryGetProperty("~hasThing", out ontologyId))
                ontologyIds.Add(ontologyId.GetProperty("ontologyId").ToString());
            var twinsProp = thingsArray[0].GetProperty("~twins");
            var twins = JsonSerializer.Deserialize<List<JsonElement>>(twinsProp.GetRawText()) ?? [];
            foreach(var thing in twins)
            {
                if(thing.TryGetProperty("~hasThing", out ontologyId))
                    ontologyIds.Add(ontologyId.GetProperty("ontologyId").ToString());
            }
            return ontologyIds.ToList();
        }

        public async Task<JsonElement?> GetNamespacesInOntologyAsync(string ontologyId)
        {
            var txn = _client.NewTransaction();
            string query = $@"
                {{
                    ontology(func: eq(ontologyId, ""{ontologyId}"")){{
                        namespace{{
                            namespaceId
                            prefix
                            uri
                        }}
                    }}
                }}
            ";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Acceder a things[0]["~twins"]
            if (!root.TryGetProperty("ontology", out JsonElement ontologyArray) || ontologyArray.GetArrayLength() == 0)
            {
                return null;
            }

            // if (!root.TryGetProperty("namespace", out JsonElement nsArray) || nsArray.GetArrayLength() == 0)
            // {
            //     return null;
            // }

            return JsonSerializer.Deserialize<JsonElement>(ontologyArray[0].GetRawText());
        }

        public async Task<JsonElement?> GetThingInOntologyByIdAsync(string ontologyId, string thingId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                things as var(func: eq(thingId, ""{thingId}"")) @cascade {{
                    ~hasThing @filter(eq(ontologyId, ""{ontologyId}""))
                }}

                thing(func: uid(things)){{
                    thingId
                    name
                    createdAt
                    hasType{{
                        thingId
                        name
                        Thing.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                    }}
                    inheritsFrom{{
                        thingId
                        name
                        Thing.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                    }}
                    Thing.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                    hasAttribute{{
                        Attribute.key
                        Attribute.value
                        Attribute.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                    }}
                    ~relatedTo @filter(not has(relatedFrom)){{
                        uid
                        Relation.name
                        Relation.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                        relatedTo @filter(NOT uid(things)){{
                            name
                            thingId
                            Thing.prefix{{
                                namespaceId
                                prefix
                                uri
                            }}
                        }}
                    }}

                    ~relatedFrom {{
                        uid
                        Relation.name
                        Relation.prefix{{
                            namespaceId
                            prefix
                            uri
                        }}
                        relatedTo{{
                            name
                            thingId
                            Thing.prefix{{
                                namespaceId
                                prefix
                                uri
                            }}
                        }}
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("thing", out JsonElement ontologyArray) || ontologyArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(ontologyArray[0].GetRawText());
        }

        public async Task<JsonElement?> GetRelationByName(string ontologyId, string relationName)
        {
            var txn = _client.NewTransaction();

            var query = $@"
            {{
                relations(func: eq(Relation.name, ""{relationName}"")) {{
                    relatedFrom @filter(uid_in(~hasThing, uid(ontology))) {{
                        uid
                        thingId
                    }}
                    relatedTo @filter(uid_in(~hasThing, uid(ontology))) {{
                        uid
                        thingId
                    }}
                }}

                ontology as var(func: eq(ontologyId, ""{ontologyId}""))
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Console.WriteLine(json);

            if (!root.TryGetProperty("relations", out JsonElement relationArray) || relationArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(relationArray.GetRawText()); ;
        }



        public async Task<JsonElement?> GetAttributeByName(string ontologyId, string attributeName)
        {
            var txn = _client.NewTransaction();


            var query = $@"
            {{
                ontology as var(func: eq(ontologyId, ""{ontologyId}""))

                attributes(func: eq(Attribute.key, ""{attributeName}"")) {{

                Attribute.key
                Attribute.type
                Attribute.value

                thing: ~hasAttribute @filter(uid_in(~hasThing, uid(ontology))) {{
                    uid
                    thingId
                }}
            }}
            }}";


            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("attributes", out JsonElement attributeArray) || attributeArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(attributeArray.GetRawText()); ;
        }

        public async Task<JsonElement?> GetThingAttributesByIdAsync(string ontologyId, string thingId)
        {
            var txn = _client.NewTransaction();

            var query = $@"
            {{
                ontology as var(func: eq(ontologyId, ""{ontologyId}""))

                thing(func: eq(thingId, ""{thingId}""))
                @filter(uid_in(~hasThing, uid(ontology))) 
                {{
                    hasAttribute{{
                        Attribute.key
                        Attribute.type
                        Attribute.value
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("thing", out JsonElement thingArray) || thingArray.GetArrayLength() == 0)
                return null;

            if (!thingArray[0].TryGetProperty("hasAttribute", out JsonElement attributeArray) || attributeArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(attributeArray.GetRawText());
        }

        public async Task<string?> GetRelationUidByThingIdsAsync(string sourceThingId, string targetThingId, string relationName)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                source as var(func: eq(thingId, ""{sourceThingId}""))
                target as var(func: eq(thingId, ""{targetThingId}""))

                relations(func: eq(Relation.name, ""{relationName}"")) {{
                    uid
                    relatedTo @filter(uid(source) OR uid(target))
                    hasChild @filter(uid(target))
                }}
            }}
            ";

            _logger.LogDebug("GetRelationUidByThingIdsAsync query: {Json}", query);

            var response = await txn.Query(query);
            var json = response.Json.ToStringUtf8();

            _logger.LogDebug("GetRelationUidByThingIdsAsync response: {Json}", json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("relations", out var relations) || relations.GetArrayLength() == 0)
                return null;

            var rel = relations[0];
            if (rel.TryGetProperty("uid", out var uidProp))
                return uidProp.GetString();

            return null;
        }

        public async Task CreateInstanciatedThingAsync(string thingId, string id, string? ontologyId = null, string? twinUid = null)
        {
            var txn = _client.NewTransaction();
            try
            {
                string? uid = string.IsNullOrWhiteSpace(ontologyId) ? null : await GetThingInOntologyUidAsync(ontologyId, thingId);

                var mutation = new Mutation
                {
                    SetJson = ByteString.CopyFromUtf8(JsonSerializer.Serialize(ThingBuilder.BuildThing(thingId, id, typeUid: uid, twinUid: string.IsNullOrWhiteSpace(twinUid) ? null : twinUid)))
                };
                await txn.Mutate(mutation);
                await txn.Commit();
            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<(bool, bool)> ExistsInstanciatedThingOfOntology(string ontologyId, string thingId, string id)
        {
            //First bool: whether if it exists (conflict) or not
            //Second bool: only checked if the first one is true, if the existing thing is correct (all relations are there)

            var txn = _client.NewTransaction();
            try
            {
                ThingDescription? thingDescription = null;
                try
                {
                    thingDescription = await _thingsService.GetThingAsync(id);

                    //if it's not of type thing Id, we take it as conflict   
                    if(thingDescription.TypeAnnotation is null || !thingDescription.TypeAnnotation.Contains(thingId))
                        return (true, false);

                }catch(KeyNotFoundException){
                    return (false, false);
                }

                

                //we check the Thing in the Onology to quicklycheck that all relations are in order and correct
                var ontologyThing = await GetDependenciesOfThingInOntology(ontologyId, thingId);
                if(ontologyThing.TryGetProperty("unidirectionals", out var unidir))
                    foreach(var rel in JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(unidir)!.Keys){
                        if(thingDescription.Links is null || !thingDescription.Links.Any(l => l.Rel is not null && l.Rel == $"{ontologyId}:{rel}"))
                            return (true, false);
                        
                            
                    }
                if(ontologyThing.TryGetProperty("bidirectionals", out var bidir))
                    foreach(var rel in JsonSerializer.Deserialize<Dictionary<string, JsonNode>>(bidir)!.Keys)
                        if(thingDescription.Links is null || !thingDescription.Links.Any(l => l.Rel is not null && l.Rel == $"{ontologyId}:{rel}"))
                            return (true, false);

                var query = $@"
                {{
                    thing(func: eq(thingId, ""{thingId}"")) @filter(eq(name, ""{id}"")) @cascade{{
                        thingId
                        hasType @filter(eq(thingId, ""{thingId}"")){{
                            thingId
                        }}
                    }}
                }}
                ";

                var response = await txn.Query(query);
                var json = response.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if(root.TryGetProperty("thing", out var thing) || thing.AsNode()!.AsArray().Count()>0)
                    return (true, true);
                
                await CreateInstanciatedThingAsync(thingId, id, ontologyId: ontologyId);
                return (true, true);
            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<JsonElement> GetDependenciesOfThingInOntology(string ontologyId, string thingId, HashSet<string>? codependencies = null, ValidationBody? validation = null)
        {
            //gets the necessary relations and things for the Thing to be instanciated correctly
            var txn = _client.NewTransaction();
            try
            {
                var filterOntology = @$"~hasThing @filter(eq(ontologyId, ""{ontologyId}""))";
                var filterThings = codependencies is null ? "" : @$"avoidingThings as var(func: eq(thingId, [{(codependencies.Count()==0 ? "\"\"" : string.Join(", ", codependencies.Select(id => $"\"{id}\"")))}])) @cascade {{
                    {filterOntology}
                }}";

                var query = @$"
                {{
                    things as var(func: eq(thingId, ""{thingId}"")) @cascade{{
                        {filterOntology}
                    }}

                    {filterThings}

                    thing(func: uid(things)){{
                        thingId
                        typeThing: hasType{{
                            thingId
                        }}
                        unidirectionals: ~relatedFrom (orderasc: Relation.name){{
                            Relation.name
                            relatedTo {(!string.IsNullOrWhiteSpace(filterThings) ? "@filter(not uid(avoidingThings))" : "")}{{
                                thingId
                            }}
                            cb as count(relatedTo @filter(not uid(things) {(!string.IsNullOrWhiteSpace(filterThings) ? "and not uid(avoidingThings)" : "")}))
                            isReflexive: math(cb == 0)
                        }}
                        bidirectionals: ~relatedTo @filter(not has(relatedFrom)){{
                            Relation.name
                            relatedTo @filter(not uid(things) {(!string.IsNullOrWhiteSpace(filterThings) ? "and not uid(avoidingThings)" : "")}){{
                                thingId
                            }}
                            cu as count(relatedTo @filter(not uid(things) {(!string.IsNullOrWhiteSpace(filterThings) ? "and not uid(avoidingThings)" : "")}))
                            isReflexive: math(cu == 0)
                        }}
                    }}
                }}
                ";

                var response = await txn.Query(query);
                var json = response.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var thing = root.GetProperty("thing")[0];
                var flatThing = FlattenJsonElement<string>(thing, "typeThing", "thingId");
                if(flatThing.TryGetProperty("unidirectionals", out var unidir))
                    flatThing = IncorporateNewJsonElement(flatThing, FlattenJsonElementArray(unidir, "relatedTo", "thingId", "Relation.name", thingId), "unidirectionals");
                if(flatThing.TryGetProperty("bidirectionals", out var bidir))
                    flatThing = IncorporateNewJsonElement(flatThing, FlattenJsonElementArray(bidir, "relatedTo", "thingId", "Relation.name", thingId), "bidirectionals");                
                
                return flatThing;
            }catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<string?> GetThingInOntologyUidAsync(string ontologyId, string thingId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var query = @$"
                {{
                    things as var(func: eq(thingId, ""{thingId}"")) @cascade {{
                        ~hasThing @filter(eq(ontologyId, ""{ontologyId}""))
                    }}

                    thing(func: uid(things)){{
                        uid
                    }}
                }}
                ";

                var response = await txn.Query(query);
                var json = response.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if(!root.TryGetProperty("thing", out var thing) || thing.AsNode()!.AsArray().Count == 0)
                    return null;
                
                return thing.AsNode()!.AsArray().First()!["uid"]!.GetValue<string>();

            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetThingsChildrenInOntology(string ontologyId, string thingId)
        {
            using var txn = _client.NewTransaction();
            try
            {
                var query = @$"
                    {{
                        thing as var(func: eq(thingId, ""{thingId}"")) @cascade{{
                            ~hasThing @filter(eq(ontologyId, ""{ontologyId}""))
                        }}

                        children (func: has(inheritsFrom)) @cascade{{
                            thingId
                            inheritsFrom @filter(uid(thing))
                        }}
                    }}
                ";

                var response = await txn.Query(query);
                var json = response.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if(!root.TryGetProperty("children", out var children) || !children.EnumerateArray().Any())
                    return [];

                var childrenNode = children.AsNode();
                if(childrenNode is null || childrenNode is not JsonArray)
                    return [];

                return [.. children.EnumerateArray()
                    .Where(child => child.TryGetProperty("thingId", out _))
                    .Select(child => child.GetProperty("thingId").GetString() ?? "")
                    .Where(id => !string.IsNullOrWhiteSpace(id))];
            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<string?> GetThingsParentInOntology(string ontologyId, string thingId)
        {
            using var txn = _client.NewTransaction();
            try
            {
                var query = @$"
                {{
                    thing as var(func: eq(thingId, ""{thingId}"")) @cascade{{
                        ~hasThing @filter(eq(ontologyId, ""{ontologyId}""))
                    }}

                    thingInfo (func: uid(thing)){{
                        inheritsFrom{{
                            thingId
                        }}
                    }}
                }}
                ";

                var response = await txn.Query(query);
                var json = response.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if(!root.TryGetProperty("thingInfo", out var thing))
                    return null;

                Console.WriteLine(thing);

                return thing.EnumerateArray().Where(p => {Console.WriteLine(p); return p.TryGetProperty("inheritsFrom", out _);})
                    .Select(p=> {
                        var arr = p.GetProperty("inheritsFrom").EnumerateArray();
                        if(arr.Any() && arr.First().TryGetProperty("thingId", out var id))
                            return id.GetString();
                        return null;
                    }).FirstOrDefault();
            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetOntologysDefaultShapeGraphs(string ontologyId)
        {
            using var txn = _client.NewTransaction();
            try
            {
                var query = @$"
                {{

                    ontology (func: eq(ontologyId, ""{ontologyId}"")){{
                        defaultShapeGraph{{
                            shapeId
                        }}
                    }}
                }}
                ";

                var response = await txn.Query(query);
                var json = response.Json.ToStringUtf8();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if(!root.TryGetProperty("ontology", out var ontology) && !ontology.EnumerateArray().Any())
                    return [];

                var notempty = ontology.EnumerateArray()
                    .Any(o => o.TryGetProperty("defaultShapeGraph", out _));

                return notempty ? [.. ontology.EnumerateArray()
                    .First(o => o.TryGetProperty("defaultShapeGraph", out _))
                    .GetProperty("defaultShapeGraph").EnumerateArray()
                    .Select(ds => ds.GetProperty("shapeId").GetString() ?? "")
                    .Where(id => !string.IsNullOrWhiteSpace(id))] : [];
            }
            catch (Exception)
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<bool> AddDefaultShapeGraphInOntology(string ontologyId, string shapeId)
        {
            string? shapeUid = await GetShapeGraphUid(shapeId);
            string? ontologyUid = await GetOntologyUid(ontologyId);

            if(shapeUid is null || ontologyUid is null)
                return false;

            try
            {
                await AddNQuadTripleAsync([$"<{ontologyUid}> <defaultShapeGraph> <{shapeUid}> ."]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteDefaultShapeGraphInOntology(string ontologyId, string shapeId)
        {
            string? shapeUid = await GetShapeGraphUid(shapeId);
            string? ontologyUid = await GetOntologyUid(ontologyId);

            if(shapeUid is null || ontologyUid is null)
                return false;

            try
            {
                var txn = _client.NewTransaction();
                var mutation = new Mutation
                {
                    DelNquads = ByteString.CopyFromUtf8($"<{ontologyUid}> <defaultShapeGraph> <{shapeUid}> .")
                };

                await txn.Mutate(mutation);
                await txn.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return false;
            }

            return true;
        }

        #endregion

        #region Shape Graphs
        // ------------------------ Specific Methods for Shape Graphs ------------------------

        public async Task<bool> ExistsShapeGraphByIdAsync(string shapeId)
        {
            var query = $@"{{
                exists(func: eq(shapeId, ""{shapeId}"")){{
                    uid
                }}
            }}";

            var response = await _client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("exists", out var existsArray) && existsArray.ValueKind == JsonValueKind.Array)
            {
                return existsArray.GetArrayLength() > 0;
            }

            return false;
        }

        public async Task<PagedResult<JsonElement>> GetAllShapeGraphsAsync(int page, int pageSize, string? searchTerm)
        {
            (page, pageSize, int offset, string filter) = NormalizePaginationParameters(page, pageSize, searchTerm, "shapeId");
            var txn = _client.NewTransaction();
            try
            {
                var query = $@"
                    {{

                        totalCount(func: type(Shape)) {filter}{{
                            count(uid)
                        }}

                        shapegraphs(func: type(Shape), first:{pageSize}, offset:{offset}, orderasc:shapeId) {filter}{{
                            shapeId
                            countShapes: count(shapes)
                            shapesOfGraph: shapes{{
                                nodeShapeId
                            }}
                        }}
                    }}     
                ";

                var res = await txn.Query(query);
                var json = res.Json.ToStringUtf8();

                var doc = JsonDocument.Parse(json);

                var totalCount = doc.RootElement.GetProperty("totalCount")[0].GetProperty("count").GetInt32();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var result = new List<JsonElement>();
                if(doc.RootElement.TryGetProperty("shapegraphs", out var nodeshapes))
                {
                    foreach(var nodeshape in nodeshapes.EnumerateArray())
                        result.Add(FlattenJsonElement<string>(nodeshape, "shapesOfGraph", "nodeShapeId"));
                }
                return new PagedResult<JsonElement>(result, totalCount, page, pageSize, totalPages);                
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }
    
        public async Task<List<JsonElement>> GetShapesFromShapeGraph(string shapeId)
        {
            var fullJson = await GetShapeGraphNestedFullJson(shapeId);
            if(fullJson is null)
                return [];

            
            if(fullJson.Value.TryGetProperty("shapes", out var shapes))
                return JsonSerializer.Deserialize<List<JsonElement>>(shapes.GetRawText()) ?? [];
            return [];
            
        }

        public async Task<JsonElement?> GetShapeGraphNestedFullJson(string shapeId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                shapeGraphs(func: eq(shapeId, ""{shapeId}"")) @recurse(depth: 100, loop: true) {{
                    uid
                    dgraph.type
                    expand(_all_)
                    ~*
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Acceder a things[0]["~twins"]
            if (!root.TryGetProperty("shapeGraphs", out JsonElement shapeGraphsArray) || shapeGraphsArray.GetArrayLength() == 0)
            {
                return null;
            }


            var shapeProp = shapeGraphsArray[0];
            var shapes = JsonSerializer.Deserialize<JsonElement>(shapeProp.GetRawText());
            return shapes;
        } 

        public async Task<bool> ExistsNodeShapeInShapeGraphAsync (string shapeId, string nodeShapeId){
            var query = $@"
            {{
                shapeGraph as var(func: eq(shapeId, ""{shapeId}""))

                exists(func: eq(nodeShapeId, ""{nodeShapeId}"")) @cascade{{
                    uid
                    nodeShapeId
                    ~shapes @filter(uid(shapeGraph))
                }}
            }}";

            var response = await _client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("exists", out var existsArray) && existsArray.ValueKind == JsonValueKind.Array)
            {
                return existsArray.GetArrayLength() > 0;
            }

            return false;
        }

        public async Task<JsonElement?> GetNodeShapeFromShapeGraphByIdAsync (string shapeId, string nodeShapeId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                nodeshapes as var(func: eq(nodeShapeId, ""{nodeShapeId}"")) @cascade {{
                    ~shapes @filter(eq(shapeId, ""{shapeId}""))
                }}

                nodeshape(func: uid(nodeshapes)) @recurse(depth: 100, loop: true) {{
                    uid
                    dgraph.type
                    expand(_all_)
                    ~*
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("nodeshape", out JsonElement nodeshapeArray) || nodeshapeArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(nodeshapeArray[0].GetRawText());
        }

        public async Task<string?> GetShapeGraphUid(string shapeId)
        {
            using var txn = _client.NewTransaction();
            var query = $@"
            {{
                shapeGraph(func: eq(shapeId, ""{shapeId}"")){{
                    uid
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if(!root.TryGetProperty("shapeGraph", out var shapeGraph))
                return null;

            return shapeGraph.EnumerateArray().Select(s => s.TryGetProperty("uid", out var uid) ? uid.GetString() : null).FirstOrDefault(uid => uid is not null);
        }

        private async Task<List<string>> GetAllShapeGraphRelatedNodesUidAsync(string shapeId)
        {
            var uids = new List<string>();

            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                shapeGraphs(func: eq(shapeId, ""{shapeId}"")) @recurse(depth: 100, loop: true) {{
                    uid
                    dgraph.type
                    expand(_all_)
                    ~*
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return [.. GetUidFromJsonElement(root)];
        }

        public async Task<Response> DeleteShapeGraphByIdAsync(string shapeId)
        {
            var txn = _client.NewTransaction();
            try
            {
                var uids = GetAllShapeGraphRelatedNodesUidAsync(shapeId).Result;
                Console.WriteLine(uids.Count);

                var deleteObjects = new List<Dictionary<string, string>>();

                foreach (var uid in uids)
                {
                    // Each object = one node to delete
                    deleteObjects.Add(new Dictionary<string, string> { { "uid", uid } });
                }

                // Serialize to JSON
                var deleteJson = JsonSerializer.Serialize(deleteObjects);
                var mutation = new Mutation
                {
                    DeleteJson = ByteString.CopyFromUtf8(deleteJson)
                };

                try
                {
                    var response = await txn.Mutate(mutation);
                    await txn.Commit();
                    return response;
                }
                catch (Exception ex)
                {
                    await txn.DisposeAsync();
                    throw new Exception("Error removing shape graph: " + ex.Message);
                }

            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }
        
        #endregion
    }
}

