using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Twins.Services;

namespace Twins.Builders
{
    public static class ShapeBuilder
    {
        /// <summary>
        /// Creates a basic Shape structure for creating a NodeShape in Json-LD.
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <param name="key">The name of the relation or attribute that will be checked in the Shape.</param>
        /// <returns>
        /// Returns the Json of the new Shape.
        /// </returns>
        public static JsonObject GetNewInnerBasicConstraint(string thingId, string key)
        {
            return new JsonObject
            {
                ["sh:description"]= $"Relates {thingId} with via {key}",
                ["sh:node"] = new JsonObject
                {
                    ["sh:class"] = new JsonObject{["@id"]=thingId},  
                    ["sh:property"] = new JsonObject
                    {
                        ["sh:path"] = new JsonObject{["@id"]=key},  
                    }
                }
            };
        }

        public static JsonObject GetBasicNodeShape(string newKey, string target = "SubjectsOf", bool withXone = false)
        {
            JsonObject obj = new JsonObject
            {
                ["@id"] = $"{newKey}{target}Shape",  
                ["@type"] = "sh:NodeShape",
                [$"sh:target{target}"] = new JsonObject { ["@id"] = newKey },  
            };
            if(withXone)
                obj["sh:xone"] = new JsonArray();
            return obj;

        }

        public static JsonObject GetLiteralValueShapeConstraints(string type, string pattern, string literalValue)
        {
            return new JsonObject{
                ["sh:datatype"] = new JsonObject{["@id"] = type}, 
                ["sh:pattern"] = pattern, 
                ["sh:hasValue"] = new JsonObject
                {
                    ["@value"] = literalValue,
                    ["@type"] = type
                }
            };
        }

        public static JsonObject GetBasicIdNode(string prefix, string name)
        {
            return new JsonObject
            {
                ["@id"] = $"{prefix}:{name}"
            };
        }

        public static string? GetShaClEquivalentFromLogicalOWL(string owl, bool moreThanOneObj = true)
        {
            return owl switch
            {
                "intersectionOf" => moreThanOneObj ? "sh:and" : null,
                "unionOf" => "sh:or",
                "oneOf" => "sh:in",
                "complementOf" => "sh:not",
                _ => null
            };
        }

        public static bool ExistsShapeProperty(JsonObject shape, (string Prefix, string Name) path, string predicate)
        {
            var prop = shape["sh:property"];
            if(prop is null)
                return false;
            if(prop is JsonArray propArr)
                return propArr.Any(p => p is JsonObject pObj && pObj["sh:path"]!["@id"]!.GetValue<string>() == $"{path.Prefix}:{path.Name}");
            if(prop is JsonObject propObj)
                return propObj["sh:path"] is not null && propObj["sh:path"]!["@id"]!.GetValue<string>()==$"{path.Prefix}:{path.Name}" && propObj[predicate] is not null;
            return false;
        }

        public static void MergeIntoShapeProperty(JsonObject shape, (string Prefix, string Name) path, string predicate, JsonNode value)
        {
            var propNode = shape["sh:property"];
            if(propNode is null)
            {
                propNode = new JsonObject
                {
                    ["sh:path"] = GetBasicIdNode(path.Prefix, path.Name),
                    [predicate] = value
                };
                shape["sh:property"] = new JsonArray{propNode};
            }
            else if(propNode is JsonArray propArr)
            {
                var existingProp = propArr.Where(p => p is JsonObject pObj && pObj["sh:path"]!["@id"]!.GetValue<string>() == $"{path.Prefix}:{path.Name}");
                if(!existingProp.Any()){
                    Console.WriteLine($"{shape} no habia otra property para {path}");
                    propArr.Add(new JsonObject
                    {
                        ["sh:path"] = GetBasicIdNode(path.Prefix, path.Name),
                        [predicate] = value.DeepClone()
                    });
                }else
                {
                    var conflictingPredProp = existingProp.Where(p=> (p is JsonObject pObj && pObj[predicate] is not null) || (p is JsonArray pArr && pArr.Any(pi => pi is not null && pi.Equals(value))));

                    if(!conflictingPredProp.Any())
                        existingProp!.First()![predicate] = value.DeepClone();
                    else if(conflictingPredProp.Any(p => (p is JsonObject pObj && pObj[predicate]!.Equals(value)) || (p is JsonArray pArr && pArr.Any(pi => pi is not null && pi.Equals(value)))))
                    //Parsear a sh:or { lista de predicates -> values}
                        return;
                    else{
                        var property = existingProp.FirstOrDefault(p => p is JsonObject pObj && pObj["sh:or"] is null) ?? existingProp.First();
                        var existingValue = property![predicate]!;
                        var orNode = property!["sh:or"];
                        if(orNode is null)
                        {
                            orNode = new JsonArray{new JsonObject{[predicate] = existingValue!.DeepClone()}, new JsonObject{[predicate] = value.DeepClone()}};
                        }
                        else
                        {
                            var andNode = property!["sh:and"];
                            var orLists = new JsonArray
                                {
                                    new JsonObject{["sh:or"] = orNode.DeepClone()},
                                    new JsonObject{["sh:or"] = new JsonArray{new JsonObject{[predicate] = existingValue!.DeepClone()}, new JsonObject{[predicate] = value.DeepClone()}}}
                                };
                            if(andNode is null)
                                property["sh:and"] = orLists;
                            else if(andNode is JsonArray andArr)
                                foreach(var orSubNode in orLists)
                                    if(orSubNode is not null)
                                        andArr.Add(orSubNode.DeepClone());
                        }
                    }
                }
            }
        }

        public static void GetDomainNode(string prefixPredicate, string predicate, string ontologyId, HashSet<(string Prefix, string Name)> domains, bool isRelation, JsonObject? shape = null)
        {
            //if shape object is null, then the shape does not exist and we have to create it
            
            var parsedDomain = new JsonArray([.. domains.Select(d => GetBasicIdNode(isRelation ? ontologyId : d.Prefix, d.Name))]);
            if(shape is null || shape.Count==0){
                shape ??= new JsonObject();
                shape["@id"] = $"{prefixPredicate}:{predicate}Shape";
                shape["sh:targetSubjectsOf"] = GetBasicIdNode(prefixPredicate, predicate);
                shape[isRelation ? "sh:class" : "sh:datatype"] = domains.Count == 1 ? parsedDomain.First()!.DeepClone() : parsedDomain;
                return;
            }
            //it's not null, then we have to MERGE

            //check if there is already a sh:class
            //if there is:
                //is it an array --> add
                //is it an object --> turn into an array
            //if not, regular creation
            var classNode = shape[isRelation ? "sh:class" : "sh:datatype"];
            if(classNode is null)
            {
                shape[isRelation ? "sh:class" : "sh:datatype"] = domains.Count == 1 ? parsedDomain.First() : parsedDomain;
                return;
            }else if(classNode is JsonObject)
                classNode = new JsonArray(classNode.DeepClone());
            
            
            if(classNode is JsonArray classArray)
                foreach(var domain in parsedDomain)
                    classArray.Add(domain);
        }

        public static void GetRangeNode(string prefixPredicate, string predicate, string ontologyId, HashSet<(string Prefix, string Name)> ranges, bool isRelation, JsonObject? shape = null)
        {
            var parsedRange = new JsonArray([.. ranges.Select(d => GetBasicIdNode(isRelation ? ontologyId : d.Prefix, d.Name))]);
            var rangeProperty = new JsonObject
                {
                    ["sh:path"] = GetBasicIdNode(prefixPredicate, predicate),
                    [isRelation ? "sh:class" : "sh:datatype"] = parsedRange.Count == 1 ? parsedRange.First()!.DeepClone() : parsedRange
                };
            if(shape is null || shape.Count==0)
            {
                shape ??= new JsonObject();
                shape["@id"] = $"{prefixPredicate}:{predicate}Shape";
                shape["sh:targetSubjectsOf"] = GetBasicIdNode(prefixPredicate, predicate);
                shape["sh:property"] = new JsonArray{ rangeProperty };
                return;
            }

            var propertyNode = shape["sh:property"];
            if(propertyNode is null)
            {
                shape["sh:property"] = new JsonArray(rangeProperty);
                return;
            }else if(propertyNode is JsonObject)
                propertyNode = new JsonArray(propertyNode.DeepClone());

            if(propertyNode is JsonArray rangeArr)
                foreach(var range in parsedRange)
                    rangeArr.Add(range);
        }

        public static (JsonObject Type, JsonObject Pattern) GetBasicAttributeConstraint((string type, string pattern) Info)
        {
            return GetBasicAttributeConstraint(Info.type, Info.pattern);
        }

        public static (JsonObject Type, JsonObject Pattern) GetBasicAttributeConstraint(string type, string pattern)
        {
            return (new JsonObject { ["sh:datatype"] = new JsonObject{["@id"] = type} }, new JsonObject{["sh:pattern"] = pattern });
        }

        public static void InsertCaseIntoNodeShape(JsonNode obj, string newKey, JsonArray xoneArray, Dictionary<(string?, string?), JsonObject> xoneIndex, string thingId,string ontologyId, bool relations)
        {
            JsonNode actualVal = obj;
            if(!relations)
            {
                Console.WriteLine("Case 1");
                (var type, var pattern) = GetBasicAttributeConstraint(FormatService.GetXsdType(obj!.AsValue()!.ToString()!));
                actualVal = new JsonArray { type, pattern };
            }else
                FormatService.ReplacePrefix(actualVal, ontologyId, "@id");

            if(!xoneIndex.TryGetValue((thingId, newKey), out var classObj) || classObj is null)
            {
                Console.WriteLine("Case 2");
                classObj = GetNewInnerBasicConstraint(thingId, newKey);
                xoneArray.Add(classObj);
                xoneIndex[(thingId, newKey)] = classObj;
            }
            var propNode = classObj["sh:node"]!["sh:property"]!;
            if(actualVal is JsonObject && propNode["sh:class"] is null && propNode["sh:or"] is null)
                propNode["sh:class"] = actualVal.DeepClone();
            else if(propNode["sh:class"] is JsonObject || actualVal is JsonArray){
                //it already has the property sh:class. 
                //We have to substitute it for a sh:or that'll be an array with sh:class properties
                
                var orVal = new JsonArray();
                if(propNode["sh:class"] is not null)
                {
                    orVal.Add(new JsonObject{["sh:class"] = propNode["sh:class"]!.DeepClone()});
                    propNode["sh:class"] = null;
                    propNode.AsObject().Remove("sh:class");
                }
                Console.WriteLine($"ActualVal {actualVal} is array {(actualVal is JsonArray ? "yes" : "no")}");
                foreach(var node in actualVal is JsonArray array ? array! : Enumerable.Repeat(actualVal, 1))
                    orVal.Add(new JsonObject{["sh:class"]=node!.DeepClone()});

                propNode["sh:or"] = orVal;
                Console.WriteLine($"Case 3: {propNode}");
            }else if(propNode["sh:or"] is JsonArray orArr)
                foreach(var node in actualVal is JsonArray arrayVal ? arrayVal! : Enumerable.Repeat(actualVal, 1))
                    orArr.Add(new JsonObject{["sh:class"]=node!.DeepClone()});
        }

        public static void BuildShapeAlgorithm(string ontologyId, JsonNode thing, JsonArray graphArray, Dictionary<string, JsonObject> graphIndex, bool relations = true)
        {
            var dict = thing.Deserialize<Dictionary<string, JsonNode>>();
            if(dict is null || !dict.Keys.Any(k => k!="@id" && k!="@type"))
                return;
            
            var thingId = FormatService.ReplacePrefix(dict["@id"].GetValue<string>(), ontologyId);  

            var objDict = dict.Where(relations 
                ? t => t.Value is JsonObject || (t.Value is JsonArray && !t.Value.AsArray().Any(v => v is JsonValue))
                : t => (t.Key!="@id" && t.Key!="@type" && t.Value is JsonValue) || (t.Value is JsonArray && !t.Value.AsArray().Any(v => v is not JsonValue)))
                .Select(t=> (t.Key, t.Value));
            
            foreach(var (key, value) in objDict)
            {
                if(value is null)
                    continue;

                var newKey = FormatService.ReplacePrefix(key, ontologyId);
                                    
                var targetId = $"{newKey}SubjectsOfShape";

                if(!graphIndex.TryGetValue(targetId, out var relShape) || relShape is null)
                {
                    relShape = GetBasicNodeShape(newKey, withXone: true);
                    graphArray.Add(relShape);
                    graphIndex[targetId] = relShape;
                }

                var xoneArray = relShape!["sh:xone"]!.AsArray();
                var xoneIndex = xoneArray
                    .Where(n => n is not null && n?["sh:node"] is not null)
                    .ToDictionary(n => 
                    (n!["sh:node"]?["sh:class"]?["@id"]!.GetValue<string>(), n!["sh:node"]?["sh:property"]?["sh:path"]?["@id"]!.GetValue<string>() ) 
                    , n=> n!.AsObject());

                foreach(var obj in value is JsonArray arr ? arr! : Enumerable.Repeat(value, 1))
                    InsertCaseIntoNodeShape(obj, newKey, xoneArray, xoneIndex, thingId, ontologyId, relations);
            }
        }

        public static bool IsLogicalContraintPredicate(string predicate)
        {
            return predicate switch
            {
                "equivalentClass" or "intersectionOf" or "subClassOf" or "unionOf" or "complementOf" or "oneOf"=> true,
                _ => false
            };
        }
    }
}