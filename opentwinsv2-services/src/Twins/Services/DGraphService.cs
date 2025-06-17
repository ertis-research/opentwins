using System.Text.Json;
using Api;
using Dgraph4Net;
using Google.Protobuf;
using Grpc.Core;

namespace OpenTwinsv2.Twins.Services
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
        /*
                public async Task<string> AddDataAsync(JsonNode newData)
                {
                    var txn = client.NewTransaction();
                    var mutation = new Mutation
                    {
                        SetJson = ByteString.CopyFromUtf8(newData.ToJsonString())
                    };

                    var response = await txn.Mutate(mutation);
                    await txn.Commit();
                    return response.ToString();
                }
        */
        public async Task<string> AddThingAsync<ThingNode>(ThingNode entity)
        {
            var txn = client.NewTransaction();
            try
            {
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
                var linkJson = new Dictionary<string, object>
                {
                    ["uid"] = fromUid,
                    [predicate] = new { uid = toUid }
                };

                var json = JsonSerializer.Serialize(linkJson);
                var mutation = new Mutation
                {
                    CommitNow = true,
                    SetJson = ByteString.CopyFromUtf8(json)
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
/*
        private Dictionary<string, object> ToFlatJson(GenericNode node)
        {
            var result = new Dictionary<string, object>
            {
                ["uid"] = node.Uid
            };

            foreach (var kv in node.Properties)
            {
                result[kv.Key] = kv.Value;
            }

            return result;
        }
*/
    }
}

