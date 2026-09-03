using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using k8s.Models;
using System.Text.Json.Serialization;
using System.ComponentModel;
using Orchestration.Services;
using System.Security.Cryptography;
using System.Text;

namespace OpenTwinsV2.Orchestration.Formatters
{
    public static class BenthosConfigParser
    {

        private sealed class ThingDescription
        {
            public string Id {get; init;} = default!;
            public Dictionary<string, TdProperty> Properties {get; init;} = new();
        }

        private sealed class TdProperty
        {
            public string Type {get; init;} = default!;
            public object Const {get; init;} = default!;
        } 

        private sealed class TdInput
        {
            public List<TdForm> Forms {get; init;} = new();
        }

        private sealed class TdForm
        {

            [JsonPropertyName("op")]
            public List<string> Op {get; init;} = new();
            
            [JsonExtensionData]
            public Dictionary<string, JsonElement> rawConfig {get; init;} =new();
        }

        private sealed class BenthosConfig
        {
            public BenthosLogger Logger {get; init;} = default!;
            public Dictionary<string, object> Input {get; init;} = default!;
            public BenthosPipeline Pipeline {get; init;} = default!;
        }

        private sealed class BenthosProcessor
        {
            public string Bloblang {get; init;} = default!;
        }

        private sealed class BenthosPipeline
        {
            public List<BenthosProcessor> Processors {get; init;} = new();
        }

        private sealed class BenthosLogger
        {
            public string Level { get; set; } = "INFO";
            public string Format { get; set; } = "json";
            
            // YamlDotNet handles snake_case automatically if you configured 
            // UnderscoredNamingConvention.Instance, so this becomes 'add_timestamp'
            public bool AddTimestamp { get; set; } = true; 
        }

        private static ThingDescription? DeserializeJsonThingDescription(JsonNode json)
        {
            return JsonSerializer.Deserialize<ThingDescription>(json, new JsonSerializerOptions {PropertyNameCaseInsensitive = true});
        }

        #region Aux Methods

        private static void SoftValidateProtocolFields(string protocol, JsonNode config)
        {
            // Map of Protocol -> Required Keys
            // This is easy to maintain and scale. You can even load this from a config file!
            var requiredKeys = new Dictionary<string, string[]>
            {
                { "mqtt",  new[] { "urls", "topics" } },
                { "kafka", new[] { "addresses", "topics", "consumer_group" } },
                { "amqp",  new[] { "url", "exchanges" } },
                { "nats",  new[] { "urls", "subject" } }
            };

            // If we don't know the protocol, we assume it's a custom/new one and let it pass (Future-proofing!)
            if (!requiredKeys.TryGetValue(protocol, out var keys)) 
                return;

            var configObj = config.AsObject(); // We already validated it's an object above

            foreach (var key in keys)
            {
                // Check if key exists (case-insensitive check is safer for user error)
                if (!configObj.Any(k => k.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Protocol '{protocol}' configuration is missing required field: '{key}'");
                }
            }
        }

        private static Dictionary<string, object> ValidateThingDescription(ThingDescription td, string input, bool authEnabled = false)
        {
            if(td is null)
                throw new ArgumentNullException("The ThingDescription Object ob5tained was null");

            if(string.IsNullOrWhiteSpace(td.Id))
                throw new InvalidOperationException("The Id in ThingDescription Object was null or empty");

            if(td.Properties is null)
                throw new InvalidOperationException("The ThingDescription must have Properties");

            JsonNode configJson = JsonSerializer.SerializeToNode(td.Properties)!;
            
            //if the input is known, will check for bare minimum requirements, if not, it will return
            
            SoftValidateProtocolFields(input, configJson);

            var cleanConfig = new Dictionary<string, object>();

            foreach (var kvp in td.Properties.Keys)
            {
                try
                {
                    cleanConfig[kvp] = ConvertWOTType(td.Properties[kvp].Const, td.Properties[kvp].Type) ?? throw new Exception();
                }catch(Exception){continue;}
            }

            if (authEnabled)
            {
                //depending on the protocol, the auth parameters are in one form or another
                switch (input)
                {
                    case "mqtt":
                        cleanConfig["user"] = "${USERNAME}";
                        cleanConfig["password"] = "${PASSWORD}";
                        break;

                    case "kafka": 
                        cleanConfig["sasl"] = new Dictionary<string, object>
                        {
                            ["mechanism"] = "PLAIN",
                            ["user"] = "${USERNAME}",
                            ["password"] = "${PASSWORD}"
                        };
                        //TODO: Enforce tls?
                        break;
                    default: Console.WriteLine($"WARNING: Auth not available for {input} protocol.");
                        break;
                }
            }

            return cleanConfig;
        }

        private static object? ConvertWOTType(object element, string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "string":
                case "datetime":
                    return element.ToString();
                case "boolean":
                case "integer":
                case "unsignedint":
                case "double":
                    return ConvertJsonNode(JsonSerializer.SerializeToNode(element));
                case "array":
                    //Json Array --> List<object> so it is parsed correctly in the input configMap
                    JsonNode nodeArr = JsonSerializer.SerializeToNode(element) ?? throw new ArgumentException("Cannot convert into Json Node");
                    if(nodeArr is JsonArray arr)
                    {
                        return arr
                        // 3. Recursively converts each item (in case it's a nested object or list)
                        .Select(ConvertJsonNode)
                        // 4. Returns a standard C# List that YamlDotNet understands
                        .ToList();
                    }
                    break;
                case "map":
                case "class":
                    //Json Object first, then Dictionary<string, object> so it is parsed correctly, recursively parse the things inside to a proper type
                    JsonNode node = JsonSerializer.SerializeToNode(element) ?? throw new ArgumentException("Cannot convert into Json Node");
                    if(node is JsonObject obj)
                    {
                        return obj.ToDictionary(kvp=>kvp.Key, kvp=> ConvertJsonNode(kvp.Value));
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Cannot convert type '{type}' to a valid type for Benthos Config.");
            }
            throw new InvalidOperationException($"Cannot convert type '{type}' to a valid type for Benthos Config.");
        }

        private static object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(k => k.Name, v => ConvertJsonElement(v.Value)),
                _ => element.ToString()
            };
        }

        private static object ConvertJsonNode(JsonNode? node)
        {
            if (node == null) return null!;
            if (node is JsonValue val)
            {
                if (val.TryGetValue<int>(out var i)) return i;
                if (val.TryGetValue<double>(out var d)) return d;
                if (val.TryGetValue<bool>(out var b)) return b;
                return val.ToString();
            }
            if (node is JsonArray arr) return arr.Select(ConvertJsonNode).ToList();
            if (node is JsonObject obj) return obj.ToDictionary(k => k.Key, v => ConvertJsonNode(v.Value));
            
            return node.ToString();
        }


        private static string GenerateBloblang(string thingId, string protocol)
        {
            // Map protocols to their Benthos metadata keys
            var metaKey = protocol switch 
            {
                "mqtt" => "mqtt_topic",
                "kafka" => "kafka_topic",
                "amqp" => "amqp_subject", 
                "nats" => "nats_subject",
                _ => "topic" // Fallback
            };

            return $$"""
                let topic_parts = metadata("{{metaKey}}").split("/")
                root = {
                    "specversion": "1.0",
                    "id": uuid_v4(),
                    "source": "{{thingId}}",
                    "type": "{{protocol}}:telemetry", 
                    "time": now(),
                    "datacontenttype": "application/json",
                    "data": this
                }
            """.Replace("\r", "");
        }

        private static string ParseToBenthosConfigYaml(string id, string protocol, Dictionary<string, object> config)
        {
            var benthosConfig = new BenthosConfig
            {
                Logger = new BenthosLogger
                {
                    Level = "INFO",
                    Format = "json",
                    AddTimestamp = true
                },
                Input = new Dictionary<string, object>
                {
                    [protocol] = config
                },
                Pipeline = new BenthosPipeline
                {
                    Processors = new()
                    {
                        new BenthosProcessor
                        {
                            Bloblang = GenerateBloblang(id, protocol)
                        }
                    }
                }
            };

            var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithNewLine("\n")
            .Build();

            return serializer.Serialize(benthosConfig).Replace("\r\n", "\n");
        }

        #endregion

        #region Getters

        /// <summary>
        /// Gets the configMap name from an identifier.
        /// </summary>
        /// <param name="id">The original identifier, can be a job or Thing identifier.</param>
        /// <returns>
        /// Returns the name of the configMap.
        /// </returns>
        public static string GetConfigName(string id)
        {
            return $"{KubernetesService.ToK8sLabelValue(id)}-config";
        }

        /// <summary>
        /// Gets the job identifier generated froma  Thing identifier.
        /// </summary>
        /// <param name="thingId">The identifier of a Thing.</param>
        /// <returns>Returns the generated job identifier.</returns>
        public static string GetJobIdFromThingId(string thingId)
        {
            return $"benthos-job-{thingId}";
        }

        /// <summary>
        /// Gets the secret name for the given Thing identifier.
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>Returns the generated secret name</returns>
        public static string GetSecretNameFronThingId(string thingId)
        {
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(thingId));
            string fullHash = Convert.ToHexString(hashBytes).ToLower();
            string shortHash = fullHash.Substring(0, 16);
            return $"auth-{shortHash}";
        }

        /// <summary>
        /// Extracts input and thingId from a Thing Description.
        /// </summary>
        /// <param name="thingDescription">The Json of the Thing Description.</param>
        /// <returns>Returns both the input and the thingId of the Connection Thing.</returns>
        /// <exception cref="ArgumentException">Thrown if there's any issue with the format of the id field in the Thing Description</exception>
        public static string GetInputFromThingDescription(JsonNode thingDescription)
        {
            var idNode = thingDescription["id"] ?? throw new ArgumentException("There's no field 'id' in the Thing Description");
            
            if(idNode is not JsonValue idVal || idVal.GetType().Equals(JsonValueKind.String))
                throw new ArgumentException("The id field in the Thing Desription must be a String Value");
            
            var id = idNode.GetValue<string>();
            
            if(string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("The id field in the Thing Description cannot be null or empty.");

            //Check if it starts with urn:connections
            if(!id.StartsWith("urn:connections:"))
                throw new ArgumentException("The id field in the Thing Description does not start with urn:connections");

            var noPrefix = id.Substring("urn:connections:".Length);
            var input = noPrefix.Substring(0, noPrefix.IndexOf(":"));

            if(string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("The input cannot be null or empty.");

            return input;
        }

        #endregion

        #region Parsing Config

        public static V1ConfigMap ParseBenthosConfig(JsonNode json, string namespaceName, string id, string input, V1Secret? authSecret)
        {
            var td = DeserializeJsonThingDescription(json) ?? throw new Exception("The ThingDescription obtained from the Json was null");
            
            Dictionary<string, object> configDic = ValidateThingDescription(td, input, authSecret is not null);

            var configYaml = ParseToBenthosConfigYaml(td.Id, input, configDic);
            
            return new V1ConfigMap
            {
                Metadata = new V1ObjectMeta
                {
                    Name= $"{id}-config",
                    NamespaceProperty = namespaceName
                },
                Data = new Dictionary<string, string>
                {
                    ["benthos.yaml"] = configYaml
                }
            };
        }

        #endregion
    }
}