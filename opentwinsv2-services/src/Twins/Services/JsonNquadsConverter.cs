// Esto lo ha generado ChatGPT, hay que mirarlo y optimizarlo en algun momento jeje
using System.Text.Json.Nodes;

namespace OpenTwinsV2.Twins.Services
{
    public interface IJsonNquadsConverter
    {
        string JsonToNquads(string json);
        //string NquadsToJson(string json);
    }

    public class JsonNquadsConverter : IJsonNquadsConverter
    {
        private readonly ILogger<JsonNquadsConverter> _logger;

        public JsonNquadsConverter(ILogger<JsonNquadsConverter> logger)
        {
            _logger = logger;
        }

        public string JsonToNquads(string json)
        {
            var doc = JsonNode.Parse(json)!;
            var things = doc["things"]?.AsArray();
            if (things is null) return string.Empty;

            var result = new List<string>();
            var relations = new Dictionary<string, (string from, string? to, string? name)>();
            var uidToThingId = new Dictionary<string, string>();

            // Función auxiliar: predicados con namespace RDF válido
            string Predicate(string key) => $"<http://example.org/{key}>";

            // Mapear uid -> thingId
            foreach (var thingWrapper in things)
            {
                var twins = thingWrapper?["~twins"]?.AsArray();
                if (twins is null) continue;

                foreach (var twin in twins)
                {
                    var uid = twin?["uid"]?.ToString();
                    var thingId = twin?["thingId"]?.ToString();

                    if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(thingId))
                        uidToThingId[uid!] = thingId!;
                }
            }

            // Función auxiliar: resolve uid -> <thingId>
            string ResolveId(string? uid) =>
                uid != null && uidToThingId.ContainsKey(uid)
                    ? $"<{uidToThingId[uid]}>"
                    : $"<{uid}>";

            // Generar triples
            foreach (var thingWrapper in things)
            {
                var twins = thingWrapper?["~twins"]?.AsArray();
                if (twins is null) continue;

                foreach (var twin in twins)
                {
                    var subj = ResolveId(twin?["uid"]?.ToString());

                    foreach (var prop in twin!.AsObject())
                    {
                        if (prop.Key is "uid" or "thingId") continue;

                        if (prop.Value is JsonValue literalVal)
                        {
                            result.Add($"{subj} {Predicate(prop.Key)} \"{literalVal.ToString()}\" .");
                        }
                        else if (prop.Value is JsonArray arr)
                        {
                            foreach (var child in arr)
                            {
                                var objUid = child?["uid"]?.ToString();
                                var obj = ResolveId(objUid);

                                if (objUid is not null)
                                    result.Add($"{subj} {Predicate(prop.Key)} {obj} .");

                                // Expandir propiedades del hijo
                                foreach (var childProp in child!.AsObject())
                                {
                                    if (childProp.Key is "uid" or "thingId") continue;

                                    if (childProp.Value is JsonValue cv)
                                    {
                                        result.Add($"{obj} {Predicate(childProp.Key)} \"{cv.ToString()}\" .");

                                        // Detectar nodos de relación
                                        if (childProp.Key == "Relation.name")
                                        {
                                            if (!relations.ContainsKey(objUid!))
                                                relations[objUid!] = (from: "", to: null, name: cv.ToString());
                                            else
                                                relations[objUid!] = (
                                                    relations[objUid!].from,
                                                    relations[objUid!].to,
                                                    cv.ToString()
                                                );
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Aplanar relaciones
            foreach (var kv in relations)
            {
                var uid = kv.Key;
                var (from, to, name) = kv.Value;

                var fromId = ResolveId(from);
                var toId = ResolveId(to);

                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to) && !string.IsNullOrEmpty(name))
                {
                    result.Add($"{fromId} {Predicate(name)} {toId} .");
                    _logger.LogInformation("Flattened relation into triple: {From} <{Name}> {To}", fromId, name, toId);
                }
            }

            return string.Join("\n", result);
        }
    }
}