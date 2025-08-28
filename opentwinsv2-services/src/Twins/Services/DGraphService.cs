using System.Text.Json;
using System.Text.Json.Nodes;
using Api;
using Dgraph4Net;
using Google.Protobuf;
using Grpc.Core;

namespace OpenTwinsV2.Twins.Services
{
    public class DGraphService : IDisposable
    {
        private readonly Dgraph4NetClient _client;
        private readonly Channel _channel;

        public DGraphService(IConfiguration configuration)
        {
            var dgraphUrl = configuration["DGraph:Url"] ?? throw new Exception("[ERROR] DGraph URL is not defined");
            // Vamos a usar este cliente porque el oficial no esta actualizado: https://github.com/schivei/dgraph4net
            _channel = new Channel(dgraphUrl, ChannelCredentials.Insecure);
            _client = new Dgraph4NetClient(_channel);
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

        public async Task<string?> GetUidByThingIdAsync(string thingId)
        {
            var query = $@"
            {{
                node(func: eq(thingId, ""{thingId}"")) {{
                    uid
                }}
            }}";

            var response = await _client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("node", out var nodeArray) && nodeArray.ValueKind == JsonValueKind.Array && nodeArray.GetArrayLength() > 0)
            {
                return nodeArray[0].GetProperty("uid").GetString();
            }

            return null;
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

        public async Task<Response> AddThingToTwinAsync(string thingId, string twinId)
        {
            var twinUid = await GetUidByThingIdAsync(twinId);
            var thingUid = await GetUidByThingIdAsync(thingId);

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
            var twinUid = await GetUidByThingIdAsync(twinId);
            var thingUid = await GetUidByThingIdAsync(thingId);

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

    }
}

