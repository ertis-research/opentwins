using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTwinsV2.Shared.Models;

namespace OpenTwinsV2.Twins.Builders
{
    public static class ThingBuilder
    {
        public static JsonObject BuildThing(string thingId, string? name = null)
        {
            return new JsonObject
            {
                ["dgraph.type"] = new JsonArray("Thing"),
                ["thingId"] = thingId,
                ["name"] = name ?? thingId,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["twins"] = new JsonArray(),
                ["domains"] = new JsonArray()
            };
        }

        private static JsonObject InitAndAddType(string thingId, string type, string? name = null)
        {
            JsonObject obj = BuildThing(thingId, name);

            if (obj["dgraph.type"] is JsonArray types)
            {
                types.Add(type);
            }

            return obj;
        }

        public static JsonObject BuildTwin(string twinId)
        {
            return InitAndAddType(twinId, "Twin");
        }
        /*
                public static JsonObject BuildROThing(string roThingId)
                {
                    return InitAndAddType(roThingId, "RealObject");
                }

                public static JsonObject BuildResources(string resourceId)
                {
                    return InitAndAddType(resourceId, "Resource");
                }
        */

        /// <summary>
        /// Crea un nodo Thing básico desde una ThingDescription.
        /// </summary>
        public static JsonObject MapToThing(ThingDescription td)
        {
            return new JsonObject
            {
                ["dgraph.type"] = new JsonArray("Thing"),
                ["thingId"] = td.Id,
                ["name"] = td.Title,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["twins"] = new JsonArray(),
                ["domains"] = new JsonArray()
            };
        }

        /// <summary>
        /// Añade un twinUid al array "twins".
        /// </summary>
        public static JsonObject AddTwinToThing(JsonObject thing, string twinUid)
        {
            if (thing["twins"] is JsonArray twins)
            {
                twins.Add(new { uid = twinUid });
            }

            return thing;
        }

        /// <summary>
        /// Construye un nodo Relation vacío (sin edges todavía).
        /// </summary>
        public static JsonObject BuildRelationNode(string relationName)
        {
            return new JsonObject
            {
                ["dgraph.type"] = new JsonArray("Relation"),
                ["Relation.name"] = relationName,
                ["Relation.createdAt"] = DateTime.UtcNow.ToString("o"),
                //["Relation.attributes"] = metaJson
            };
        }

        /// <summary>
        /// Construye un nodo Thing de placeholder para targets inexistentes.
        /// </summary>
        public static JsonObject BuildPlaceholderThing(string thingId)
        {
            return new JsonObject
            {
                ["dgraph.type"] = new JsonArray("Thing"),
                ["thingId"] = thingId,
                ["name"] = thingId,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["twins"] = new JsonArray(),
                ["domains"] = new JsonArray()
            };
        }

        /// <summary>
        /// Decide el nombre de la arista según la relación.
        /// </summary>
        public static string MapRelToEdge(string? rel)
        {
            if (string.IsNullOrEmpty(rel)) return "relatedTo";
            var lower = rel.ToLowerInvariant();
            if (lower.Contains("child") || lower.Contains("parent") || lower.Contains("contains"))
                return "hasChild";
            if (lower.Contains("part") || lower.Contains("component"))
                return "hasPart";
            return "relatedTo";

        }

        /// <summary>
        /// Construye un payload JSON con sourceThing + placeholders + relation nodes.
        /// No toca DGraph ni hace logs. 
        /// Recibe un diccionario uidTargets (href->uid) de los que ya existen.
        /// Si un href no está en uidTargets, crea un placeholder con uid blank.
        /// </summary>
        public static JsonArray BuildPayloadWithLinks(
            ThingDescription td,
            string twinUid,
            Dictionary<string, string> uidTargets)
        {
            // 1. Source Thing
            var sourceThing = MapToThing(td);
            sourceThing["uid"] = "_:source";
            sourceThing = AddTwinToThing(sourceThing, twinUid);

            var payload = new JsonArray { sourceThing };

            if (td.Links == null || td.Links.Count == 0)
                return payload;

            int relCounter = 0;
            int targetCounter = 0;

            foreach (var link in td.Links)
            {
                if (link.Rel == null || !link.Rel.Contains("otv2:")) continue;

                relCounter++;
                var source = link.Href.ToString();

                var relNode = BuildRelationNode(link.Rel);
                relNode["uid"] = $"_:rel{relCounter}";

                string edgeName = MapRelToEdge(link.Rel);
                string targetUid;
                if (uidTargets.TryGetValue(source, out var foundUid))
                {
                    targetUid = foundUid;
                }
                else
                {
                    targetCounter++;
                    string blankTarget = $"_:t{targetCounter}";
                    var placeholder = BuildPlaceholderThing(source);
                    placeholder["uid"] = blankTarget;
                    payload.Add(placeholder);
                    targetUid = blankTarget;
                }

                relNode[edgeName] = new JsonArray(new JsonObject { ["uid"] = targetUid }); // add edge from relation node to target
                relNode["relatedTo"] = new JsonArray(new JsonObject { ["uid"] = "_:source" }); // also link the relation node to the source thing

                payload.Add(relNode);
            }

            return payload;
        }

    }
}