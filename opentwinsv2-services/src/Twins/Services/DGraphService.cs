using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api;
using Dapr;
using Dgraph4Net;
using Google.Protobuf;
using Grpc.Core;

namespace OpenTwinsV2.Twins.Services
{
    public class DGraphService : IDisposable
    {
        private readonly Dgraph4NetClient _client;
        private readonly Channel _channel;
        private readonly ILogger<DGraphService> _logger;

        public DGraphService(IConfiguration configuration, ILogger<DGraphService> logger)
        {
            var dgraphUrl_gRPC = configuration["DGraph:URL_gRPC"] ?? throw new Exception("[ERROR] DGraph URL is not defined");
            // Vamos a usar este cliente porque el oficial no esta actualizado: https://github.com/schivei/dgraph4net
            _channel = new Channel(dgraphUrl_gRPC, ChannelCredentials.Insecure);
            _client = new Dgraph4NetClient(_channel);
            _logger = logger;
        }

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

        public async Task<Payload> InitSchemaAsync()
        {
            string schema = @"
                thingId: string @index(exact) .
                name: string @index(term) .
                createdAt: datetime .

                hasType: [uid] @reverse .
                hasAttribute: [uid] @reverse .
                hasAction: [uid] @reverse .
                hasEvent: [uid] @reverse .

                ontologyId: string @index(exact) .
                Ontology.name: string @index(term) . 
                hasThing: [uid] @reverse .

                twins: [uid] @reverse .
                domains: [uid] @reverse .

                Attribute.key: string @index(term) .
                Attribute.type: string @index(term) .
                Attribute.value: string .

                Action.name: string @index(term) .
                Action.payload: string .

                Event.name: string @index(term) .
                Event.data: string .

                Relation.name: string @index(term) .
                Relation.createdAt: datetime .
                Relation.attributes: string .

                hasPart: [uid] @reverse .
                hasChild: [uid] @reverse .
                relatedTo: [uid] @reverse .

                domainId: string @index(exact) .
                Domain.name: string @index(term) .

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
                }

                type Ontology {
                    ontologyId
                    Ontology.name
                    createdAt
                    hasThing
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
                    relatedTo
                    hasPart
                    hasChild
                }
            ";

            var op = new Operation { Schema = schema };
            return await _client.Alter(op);
        }

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

        public async Task<List<JsonElement>> GetThingsInOntologyAsync(string ontologyId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                ontologies(func: eq(ontologyId, ""{ontologyId}"")) {{
                    hasThing{{
                        uid
                        name
                    }}
                }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8(); ;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Acceder a things[0]["~twins"]
            if (!root.TryGetProperty("ontologies", out JsonElement thingsArray) || thingsArray.GetArrayLength() == 0)
            {
                return [];
            }


            var ontologyProp = thingsArray[0].GetProperty("hasThing");
            var things = JsonSerializer.Deserialize<List<JsonElement>>(ontologyProp.GetRawText());
            return things ?? [];
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

        public async Task<JsonElement?> GetThingInOntologyByIdAsync(string ontologyId, string thingId)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
              ontology(func: eq(ontologyId, ""{ontologyId}"")) {{
                uid
                hasThing @filter(eq(thingId, ""{thingId}"")) {{
                  uid
                  thingId
                  name
                  createdAt
                  hasAttribute {{
                    Attribute.key
                    Attribute.value
                  }}
                  ~relatedTo {{
                    Relation.name
                    uid
                    relatedTo{{
                      name
                        uid
                    }}
                  }}
                }}
              }}
            }}";

            var res = await txn.Query(query);
            var json = res.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("ontology", out JsonElement ontologyArray) || ontologyArray.GetArrayLength() == 0)
                return null;

            if (!ontologyArray[0].TryGetProperty("hasThing", out JsonElement thingArray) || thingArray.GetArrayLength() == 0)
                return null;
            return JsonSerializer.Deserialize<JsonElement>(thingArray[0].GetRawText());
        }

        public async Task<JsonElement?> GetRelationByName(string ontologyId, string relationName)
        {
            var txn = _client.NewTransaction();

            var query = $@"
            {{
              relations(func: eq(Relation.name, ""{relationName}"")) {{
                
               things: relatedTo @filter(uid_in(~hasThing, uid(ontology))) {{
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
            Console.WriteLine(json);

            if (!root.TryGetProperty("attributes", out JsonElement attributeArray) || attributeArray.GetArrayLength() == 0)
                return null;

            return JsonSerializer.Deserialize<JsonElement>(attributeArray.GetRawText()); ;
        }

        public async Task<Response> AddThingToTwinAsync(string thingId, string twinId)
        {
            var twinUid = await GetUidsByThingIdsAsync([twinId]);
            var thingUid = await GetUidsByThingIdsAsync([thingId]);

            if (twinUid == null || thingUid == null)
                throw new Exception("Twin or Thing not found");

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
            var twinUid = await GetUidsByThingIdsAsync([twinId]);
            var thingUid = await GetUidsByThingIdsAsync([thingId]);

            if (twinUid == null || thingUid == null)
                throw new KeyNotFoundException("Twin or Thing not found");

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

        public async Task<string?> GetRelationUidByThingIdsAsync(string sourceThingId, string targetThingId, string relationName)
        {
            using var txn = _client.NewTransaction();

            var query = $@"
            {{
                source as var(func: eq(thingId, ""{sourceThingId}""))
                target as var(func: eq(thingId, ""{targetThingId}""))

                relations(func: eq(Relation.name, ""{relationName}"")) @cascade {{
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

    }
}

