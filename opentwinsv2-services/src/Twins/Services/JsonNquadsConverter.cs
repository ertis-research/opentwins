using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTwinsV2.Twins.Services
{
    public interface IJsonNquadsConverter
    {
        string JsonToNquads(string json, Dictionary<string, JsonElement> attributes);
    }

    public class JsonNquadsConverter : IJsonNquadsConverter
    {
        private readonly ILogger<JsonNquadsConverter> _logger;

        public JsonNquadsConverter(ILogger<JsonNquadsConverter> logger)
        {
            _logger = logger;
        }

        public string JsonToNquads(string json, Dictionary<string, JsonElement> attributes)
        {
            var doc = JsonNode.Parse(json)!;
            var things = doc["things"]?.AsArray();
            if (things is null) return string.Empty;

            var triples = new List<string>();
            var relations = new Dictionary<string, (string from, string? to, string? name)>();
            var uidToThingId = BuildUidThingIdMap(things);

            // Convert twins to N-Quads
            foreach (var thingWrapper in things)
            {
                var twins = thingWrapper?["~twins"]?.AsArray();
                if (twins is null) continue;

                foreach (var twin in twins)
                {
                    var subj = ResolveId(twin?["uid"]?.ToString(), uidToThingId);

                    foreach (var prop in twin!.AsObject())
                    {
                        if (prop.Key is "uid" or "thingId") continue;
                        ProcessProperty(subj, prop, uidToThingId, triples, relations);
                    }
                }
            }

            // Add external attributes (from JSON dictionary)
            AppendExternalAttributes(uidToThingId, attributes, triples);

            // Flatten relations
            AppendFlattenedRelations(relations, uidToThingId, triples);

            return string.Join("\n", triples);
        }

        #region Helpers

        private static string Predicate(string key) => $"<http://example.org/{key}>";

        private static Dictionary<string, string> BuildUidThingIdMap(JsonArray things)
        {
            var map = new Dictionary<string, string>();

            foreach (var thingWrapper in things)
            {
                var twins = thingWrapper?["~twins"]?.AsArray();
                if (twins is null) continue;

                foreach (var twin in twins)
                {
                    var uid = twin?["uid"]?.ToString();
                    var thingId = twin?["thingId"]?.ToString();

                    if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(thingId))
                        map[uid!] = thingId!;
                }
            }

            return map;
        }

        private static string ResolveId(string? uid, Dictionary<string, string> uidToThingId) =>
            uid != null && uidToThingId.TryGetValue(uid, out string? value)
                ? $"<{value}>"
                : $"<{uid}>";

        private static void ProcessProperty(
            string subj,
            KeyValuePair<string, JsonNode?> prop,
            Dictionary<string, string> uidToThingId,
            List<string> triples,
            Dictionary<string, (string from, string? to, string? name)> relations)
        {
            if (prop.Value is JsonValue literalVal)
            {
                triples.Add($"{subj} {Predicate(prop.Key)} \"{literalVal.ToString()}\" .");
            }
            else if (prop.Value is JsonArray arr)
            {
                foreach (var child in arr)
                {
                    var objUid = child?["uid"]?.ToString();
                    var obj = ResolveId(objUid, uidToThingId);

                    if (objUid is not null)
                        triples.Add($"{subj} {Predicate(prop.Key)} {obj} .");

                    foreach (var childProp in child!.AsObject())
                    {
                        if (childProp.Key is "uid" or "thingId") continue;

                        if (childProp.Value is JsonValue cv)
                        {
                            triples.Add($"{obj} {Predicate(childProp.Key)} \"{cv.ToString()}\" .");

                            if (childProp.Key == "Relation.name")
                            {
                                relations[objUid!] = (
                                    relations.ContainsKey(objUid!) ? relations[objUid!].from : "",
                                    relations.ContainsKey(objUid!) ? relations[objUid!].to : null,
                                    cv.ToString()
                                );
                            }
                        }
                    }
                }
            }
        }

        private void AppendExternalAttributes(Dictionary<string, string> uidToThingId, Dictionary<string, JsonElement> attributes, List<string> triples)
        {
            foreach (var thingId in uidToThingId.Values)
            {
                if (!attributes.TryGetValue(thingId, out var attrJson)) continue;

                if (attrJson.ValueKind != JsonValueKind.Object) continue;

                foreach (var attr in attrJson.EnumerateObject())
                {
                    var attrName = attr.Name; // ej. "collapsed", "occupied", "flying"
                    if (attr.Value.ValueKind != JsonValueKind.Object) continue;

                    foreach (var subAttr in attr.Value.EnumerateObject())
                    {
                        var pred = Predicate($"{attrName}.{subAttr.Name}");
                        var subj = $"<{thingId}>";
                        var val = FormatJsonValue(subAttr.Value);
                        if (val != null)
                            triples.Add($"{subj} {pred} {val} .");
                    }
                }

                _logger.LogInformation("Added external attributes for {ThingId}", thingId);
            }
        }

        private static string? FormatJsonValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => $"\"{EscapeString(value.GetString()!)}\"",
                JsonValueKind.Number => value.TryGetInt64(out var intVal)
                    ? $"\"{intVal}\"^^<http://www.w3.org/2001/XMLSchema#integer>"
                    : $"\"{value.GetDouble()}\"^^<http://www.w3.org/2001/XMLSchema#double>",
                JsonValueKind.True => "\"true\"^^<http://www.w3.org/2001/XMLSchema#boolean>",
                JsonValueKind.False => "\"false\"^^<http://www.w3.org/2001/XMLSchema#boolean>",
                JsonValueKind.Object => $"\"{EscapeString(value.GetRawText())}\"",
                JsonValueKind.Array => $"\"{EscapeString(value.GetRawText())}\"",
                JsonValueKind.Null => null, // ignorar null
                _ => $"\"{EscapeString(value.ToString())}\""
            };
        }

        private static string EscapeString(string input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private void AppendFlattenedRelations(
            Dictionary<string, (string from, string? to, string? name)> relations,
            Dictionary<string, string> uidToThingId,
            List<string> triples)
        {
            foreach (var kv in relations)
            {
                var uid = kv.Key;
                var (from, to, name) = kv.Value;

                var fromId = ResolveId(from, uidToThingId);
                var toId = ResolveId(to, uidToThingId);

                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to) && !string.IsNullOrEmpty(name))
                {
                    triples.Add($"{fromId} {Predicate(name)} {toId} .");
                    _logger.LogInformation("Flattened relation into triple: {From} <{Name}> {To}", fromId, name, toId);
                }
            }
        }

        #endregion
    }
}
