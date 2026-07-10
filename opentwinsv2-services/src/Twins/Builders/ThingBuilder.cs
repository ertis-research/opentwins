using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Services;
using VDS.RDF;

namespace OpenTwinsV2.Twins.Builders
{
    public static class ThingBuilder
    {
        public static JsonObject BuildThing(string id, string? uid = null, string? typeUid=null, string? twinUid = null)
        {
            var payload = new JsonObject
            {
                ["dgraph.type"] = new JsonArray("Thing"),
                ["thingId"] = id,
                ["name"] = id,
                ["createdAt"] = DateTime.UtcNow.ToString("o"),
                ["twins"] = twinUid is null ? [] : new JsonArray{new JsonObject{["uid"]=twinUid}},
                ["domains"] = new JsonArray(),
                ["hasType"] = typeUid is null ? [] : new JsonArray{new JsonObject{["uid"]=typeUid}}
            };
            if(!string.IsNullOrWhiteSpace(uid))
                payload["uid"] = uid;
            
            return payload;
        }

        public static JsonObject BuildAttribute(int attrCount, string key, string type, string? value)
        {
            var attribute = new JsonObject
            {
                ["uid"] = $"_:attr{attrCount}",
                ["dgraph.type"] = new JsonArray("Attribute"),
                ["Attribute.key"] = key,
                ["Attribute.type"] = type ?? "string",
            };       
                
            if(value is not null)
                attribute["Attribute.value"] = value;

            return attribute;
        }

        private static JsonObject InitAndAddType(string thingId, string type, string? typeUid = null, string? uid = null)
        {
            JsonObject obj = BuildThing(thingId, uid: uid, typeUid: typeUid);

            if (obj["dgraph.type"] is JsonArray types)
            {
                types.Add(type);
            }

            return obj;
        }

        public static JsonObject BuildTwin(string twinId, string? uid = null)
        {
            return InitAndAddType(twinId, "Twin", uid: uid);
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
        private static JsonObject BuildRelationNode(string relationName)
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
                ["dgraph.type"] = new JsonArray{"Thing", "Placeholder"},
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

        public static JsonObject BuildDeleteRelation(string relationUid)
        {
            return new JsonObject
            {
                ["uid"] = relationUid
            };
        }

        public static JsonObject BuildRelation(Link link, string targetUid, string sourceUid = "_:source", int relCounter = 1, bool bidir=true)
        {
            return BuildRelation(link.Rel, targetUid, sourceUid, relCounter, bidir);
        }

        public static JsonObject BuildRelation(string? rel, string targetUid, string sourceUid = "_:source", int relCounter = 1, bool bidir = true)
        {
            if (rel == null) return []; 
            var relNode = BuildRelationNode(rel);
            relNode["uid"] = $"_:rel{relCounter}"; //TODO: Recieve an uid, and if it's null, build a relative one (the current flow)

            string edgeName = MapRelToEdge(rel);

            if (bidir)
            {
                relNode[edgeName] = new JsonArray(
                    new JsonObject { ["uid"] = targetUid },
                    new JsonObject { ["uid"] = sourceUid }
                );
            }
            else
            {
                relNode[edgeName] = new JsonArray(new JsonObject { ["uid"] = targetUid });
                relNode["relatedFrom"] = new JsonArray(new JsonObject { ["uid"] = sourceUid });
            }

            return relNode;
        }

        public static void TurnUnidirectionalRelationPayloadIntoBidirectional(string sourceUid, JsonObject payload)
        {
            var newObjectives = payload["relatedTo"]! switch
            {
                JsonObject objObj => new JsonArray(objObj.DeepClone()),
                JsonArray array => array,
                _ => throw new Exception($"Payload malformed: {payload}")
            };
            
            newObjectives.Add(new JsonObject{["uid"]= sourceUid});
            if(payload["relatedTo"] is JsonObject)
                payload["relatedTo"] = newObjectives;

            payload.Remove("relatedFrom");
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
            Dictionary<string, string> uidTargets,
            ref int relCounter, ref int targetCounter,
            string? uid = null, JsonArray? currentPayload = null)
        {
            // 1. Source Thing
            var sourceThing = MapToThing(td);
            sourceThing["uid"] = uid ?? "_:source";
            sourceThing = AddTwinToThing(sourceThing, twinUid);

            var payload = new JsonArray { sourceThing };

            Console.WriteLine($"TD: {td}");
            if (td.Links == null || td.Links.Count == 0)
                return payload;

            foreach (var link in td.Links)
            {
                Console.WriteLine($"link: {link.Rel.ToSafeString()}");
                if (link.Rel == null) continue;
                Console.WriteLine("No me salgo");
                relCounter++;
                var source = link.Href.ToString();
                string targetUid;
                if (uidTargets.TryGetValue(source, out var foundUid))
                {
                    targetUid = foundUid;
                }
                else
                {
                    Console.WriteLine($"PLACEHOLDER ID: {link.Href}");
                    targetCounter++;
                    string blankTarget = $"_:t{targetCounter}";
                    var placeholder = BuildPlaceholderThing(source);
                    placeholder["uid"] = blankTarget;
                    payload.Add(placeholder);
                    targetUid = blankTarget;
                }
                Console.WriteLine($"TARGET ADDED: {targetUid}");

                var bid = InstanciationService.GetBidirectionalRelationPayload(uid ?? "_:source", targetUid, link.Rel, currentPayload ?? []);
                if(bid is null){
                    var unidir = InstanciationService.GetUnidirectionalRelationPayload(targetUid, uid ?? "_:source", link.Href.ToString(), currentPayload ?? []);
                    if(unidir is not null)
                    {
                        TurnUnidirectionalRelationPayloadIntoBidirectional(uid ?? "_:source", unidir);
                    }
                    else
                    {
                        var relation = BuildRelation(link, targetUid, sourceUid: uid ?? "_:source", relCounter: relCounter, bidir: false);
                        Console.WriteLine($"RELATION ADDED: {relation}");
                        payload.Add(relation);
                    }   
                }
            }
            return payload;
        }
    }
}