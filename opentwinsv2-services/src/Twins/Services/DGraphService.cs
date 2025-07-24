using System.Text.Json;
using Api;
using Dgraph4Net;
using Google.Protobuf;
using Grpc.Core;

namespace OpenTwinsV2.Twins.Services
{
    public class DGraphService : IDisposable
    {
        private readonly Dgraph4NetClient client;
        private readonly Channel _channel;

        public DGraphService(IConfiguration configuration)
        {
            var dgraphUrl = configuration["DGraph:Url"] ?? throw new Exception("[ERROR] DGraph URL is not defined");
            // Vamos a usar este cliente porque el oficial no esta actualizado: https://github.com/schivei/dgraph4net
            _channel = new Channel(dgraphUrl, ChannelCredentials.Insecure);
            client = new Dgraph4NetClient(_channel);
        }

        public void Dispose()
        {
            _channel?.ShutdownAsync().Wait();
        }

        public Dgraph4NetClient GetClient() => client;

        public async Task<string> AddThingAsync<ThingNode>(ThingNode entity)
        {
            var txn = client.NewTransaction();
            try
            {
                Console.WriteLine(JsonSerializer.Serialize(entity));
                var mutation = new Mutation
                {
                    SetJson = ByteString.CopyFromUtf8(JsonSerializer.Serialize(entity))
                };
                var response = await txn.Mutate(mutation);
                await txn.Commit();
                return response.ToString();
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }

        public async Task<string> LinkNodesAsync(string fromUid, string predicate, string toUid)
        {
            var txn = client.NewTransaction();
            try
            {
                var nquad = $"<{fromUid}> <{predicate}> <{toUid}> .";

                var mutation = new Mutation
                {
                    CommitNow = true,
                    SetNquads = ByteString.CopyFromUtf8(nquad)
                };

                var response = await txn.Mutate(mutation);
                return response.ToString();
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
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

            var response = await client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("exists", out var existsArray) && existsArray.ValueKind == JsonValueKind.Array)
            {
                return existsArray.GetArrayLength() > 0;
            }

            return false;
        }

        public async Task<string> QueryByPropertyAsync(string prop, string value)
        {
            var query = $@"
            {{
                nodes(func: eq({prop}, ""{value}"")) {{
                    uid
                    expand(_all_)
                }}
            }}";

            var response = await client.NewTransaction().Query(query);
            return response.Json.ToStringUtf8();
        }

        public async Task<string?> GetThingInTwinAsync(string twinId, string thingId)
        {
            var query = $@"
            {{
                twin(func: eq(thingId, ""{twinId}"")) @filter(eq(isTwin, true)) {{
                    contains @filter(eq(thingId, ""{thingId}"")) {{
                        uid
                        expand(_all_)
                    }}
                }}
            }}";

            var response = await client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("twin", out var twinArray) &&
                twinArray.ValueKind == JsonValueKind.Array &&
                twinArray.GetArrayLength() > 0)
            {
                var twin = twinArray[0];
                if (twin.TryGetProperty("contains", out var contains) &&
                    contains.ValueKind == JsonValueKind.Array &&
                    contains.GetArrayLength() > 0)
                {
                    var thingJson = contains[0].ToString(); // Devuelve el primer (único) thing completo
                    return thingJson;
                }
            }

            return null; // No encontrado o no pertenece a ese twin
        }

        public async Task<List<string>> GetThingsInTwinAsync(string thingId)
        {
            var query = $@"
            {{
                twin(func: eq(thingId, ""{thingId}"")) @filter(eq(isTwin, true)) {{
                    contains {{
                        thingId
                    }}
                }}
            }}";

            var response = await client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            var result = new List<string>();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("twin", out var twinArray) && twinArray.ValueKind == JsonValueKind.Array && twinArray.GetArrayLength() > 0)
            {
                if (twinArray[0].TryGetProperty("contains", out var contains) && contains.ValueKind == JsonValueKind.Array)
                {
                    foreach (var thing in contains.EnumerateArray())
                    {
                        if (thing.TryGetProperty("thingId", out var id))
                        {
                            var strId = id.GetString();
                            if (strId != null) result.Add(strId);
                        }
                    }
                }
            }

            return result;
        }

        public async Task<string> AddThingToTwinAsync(string twinId, string thingId)
        {
            var twinUid = await GetUidByThingIdAsync(twinId);
            var thingUid = await GetUidByThingIdAsync(thingId);

            if (twinUid == null || thingUid == null)
                throw new Exception("Twin or Thing not found");

            return await LinkNodesAsync(twinUid, "contains", thingUid);
        }


        // Esto hay que rafactorizarlo que lo ha hecho la chatty
        public async Task<string> RemoveThingFromTwinAsync(string twinId, string thingId)
        {
            var twinUid = await GetUidByThingIdAsync(twinId);
            var thingUid = await GetUidByThingIdAsync(thingId);

            if (twinUid == null || thingUid == null)
                throw new Exception("Twin or Thing not found");

            // 1. Eliminar la relación contains entre Twin y Thing
            var txn = client.NewTransaction();
            try
            {
                var deleteJson = new Dictionary<string, object>
                {
                    ["uid"] = twinUid,
                    ["contains"] = new[] { new Dictionary<string, object> { ["uid"] = thingUid } }
                };

                var mutation = new Mutation
                {
                    CommitNow = true,
                    DeleteJson = ByteString.CopyFromUtf8(JsonSerializer.Serialize(deleteJson))
                };

                await txn.Mutate(mutation);
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }

            // 2. Consultar si el thing sigue vinculado a otro twin
            var checkQuery = $@"
            {{
                thing(func: uid({thingUid})) {{
                    ~contains @filter(eq(isTwin, true)) {{
                        uid
                    }}
                }}
            }}";

            var checkResp = await client.NewTransaction().Query(checkQuery);
            var json = checkResp.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool isStillContained = false;

            if (root.TryGetProperty("thing", out var thingArray) &&
                thingArray.ValueKind == JsonValueKind.Array &&
                thingArray.GetArrayLength() > 0)
            {
                var thing = thingArray[0];
                if (thing.TryGetProperty("~contains", out var parents) &&
                    parents.ValueKind == JsonValueKind.Array &&
                    parents.GetArrayLength() > 0)
                {
                    isStillContained = true;
                }
            }

            // 3. Si ya no pertenece a ningún Twin, borrar el nodo
            if (!isStillContained)
            {
                await DeleteNodeAsync(thingUid);
                return $"Thing {thingId} was removed from Twin and deleted because it was not linked to any other Twin.";
            }

            return $"Thing {thingId} was removed from Twin {twinId}, but remains linked to other Twin(s).";
        }

        private async Task<string?> GetUidByThingIdAsync(string thingId)
        {
            var query = $@"
            {{
                node(func: eq(thingId, ""{thingId}"")) {{
                    uid
                }}
            }}";

            var response = await client.NewTransaction().Query(query);
            var json = response.Json.ToStringUtf8();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("node", out var nodeArray) && nodeArray.ValueKind == JsonValueKind.Array && nodeArray.GetArrayLength() > 0)
            {
                return nodeArray[0].GetProperty("uid").GetString();
            }

            return null;
        }

        public async Task<string> GetNodeByUidAsync(string uid)
        {
            var query = $@"
            {{
                node(func: uid({uid})) {{
                    uid
                    expand(_all_)
                }}
            }}";

            var response = await client.NewTransaction().Query(query);
            return response.Json.ToStringUtf8();
        }

        public async Task DeleteNodeAsync(string uid)
        {
            await using var txn = client.NewTransaction();
            try
            {
                var json = JsonSerializer.Serialize(new { uid });
                var mutation = new Mutation
                {
                    DeleteJson = ByteString.CopyFromUtf8(json)
                };

                var response = await txn.Mutate(mutation);
                await txn.Commit();
            }
            catch
            {
                await txn.DisposeAsync();
                throw;
            }
        }
    }
}

