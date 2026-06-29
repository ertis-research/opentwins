using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenTwinsV2.Twins.Services;
using Twins.Models;
using VDS.RDF;
using VDS.RDF.JsonLd;

namespace Twins.Services
{
    public static class FormatService
    {

        /// <summary>
        /// Sanitizes the uri from special characters adn retuens only the last valid part.
        /// </summary>
        /// <param name="uri">The uri to be sanitized.</param>
        /// <returns>
        /// Returns the last part of the uri without special characters.<br/>
        /// Returns 'twin' if the whole uri were special characters.
        /// </returns>
        public static string SanitizeTypeAndUIDValues(string uri)
        {
            //Check which character is last
            char[] separators = ['#', '/', '&', ':'];

            // Remove trailing separator if present at the end
            while (uri.Length > 0 && separators.Contains(uri.Last()))
            {
                //Delete illegal characters at the end
                uri = uri.Substring(0, uri.Length - 1);
            }

            // //in case the whole uri were illegal characters (unlikely but possible)
            // if (uri.Length == 0)
            // {
            //     return "twin"; //for example
            // }

            // Find last separator after removing trailing char
            int indx = uri.LastIndexOfAny(separators);

            //return the substring or the whole uri in case none of the characters are present
            return (indx >= 0 && indx < uri.Length - 1) ? uri.Substring(indx + 1) : uri;
        }

        /// <summary>
        /// Separates the uri of the node into prefix and localName.
        /// </summary>
        /// <param name="nodeUri">The uri of the node.</param>
        /// <param name="parentId">The identifier of the parent.</param>
        /// <returns>
        /// Returns the prefix and sanitized localName of the uri provided.<br/>
        /// Returns a standard prefix and the localName of the uri if it didn't have a prefix.<br/>
        /// Returns the prefix and 'twin' as localName if the whole uri were special characters.
        /// </returns>
        public static (string Prefix, string LocalName) GetLocalName(string nodeUri, string parentId)
        {

            string? prefix = null;
            string? localName = null;
            //get the name right after the prefix
            var parts = nodeUri.Split(':');
            if (parts.Length >= 2)
            {
                prefix = parts[0];
                localName = parts[1];
            }

            if (prefix is null || prefix.Length == 0)
            {
                //Get the last part of the URI
                prefix = $"{parentId}";
            }
            if (localName is null || localName.Length == 0)
            {
                localName = SanitizeTypeAndUIDValues(nodeUri);
            }
            return (prefix.ToLowerInvariant(), localName);
        }

        /// <summary>
        /// Extracts from the provided node its prefix and localName.
        /// </summary>
        /// <param name="node">The node object.</param>
        /// <param name="graph">The graph object.</param>
        /// <param name="parentId">The identifier of the parent.</param>
        /// <returns>
        /// Returns the prefix and sanitized localName of the node's qname or uri.
        /// </returns>
        public static (string Prefix, string LocalName) GetLocalName(VDS.RDF.INode node, IGraph graph, string parentId)
        {
            if (node is UriNode uriNode)
            {
                string qname;
                string prefix;
                string localName;
                if (graph.NamespaceMap.ReduceToQName(uriNode.Uri.ToString(), out qname))
                {
                    (prefix, localName) = GetLocalName(qname, parentId);
                }
                else
                {
                    (prefix, localName) = GetLocalName(uriNode.Uri.ToString(), parentId);
                }
                return (prefix, localName);
            }
            return ($"{parentId}", $"{(node.NodeType.Equals(NodeType.Blank) ? "blank_" : "")}{ node.ToString()}");
        }

        /// <summary>
        /// Sanitizes Attribute values.
        /// </summary>
        /// <param name="value">The untreated value.</param>
        /// <returns>
        /// Returns the value with special characters scaped.
        /// </returns>
        public static string SanitizeAttributeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("\\", "\\\\")   // omit \
                .Replace("\"", "\\\"")   // omit "
                .Replace("\n", "\\n")    // omit \n
                .Replace("\r", "\\r")    // omit \r
                .Replace("\t", "\\t");   // omit \t
        }

        /// <summary>
        /// Extracts from a Literal Node object its datatype and value.
        /// </summary>
        /// <param name="literal">The Literal Node object.</param>
        /// <returns>
        /// Returns the datatype and sanitized value of the Literal Node.<br/>
        /// Returns string as the datatype if the node didn't have one and the sanitized value.
        /// </returns>
        public static (string datatype, string value) GetLiteralCleanData(ILiteralNode literal)
        {
            string value = SanitizeAttributeValue(literal.Value);
            string dataType = literal.DataType?.ToString() ?? "string";
            dataType = SanitizeTypeAndUIDValues(dataType) ?? "string";

            return (dataType, value);
        }

        /// <summary>
        /// Extracts the datatype and santiized value of a literal string.
        /// </summary>
        /// <param name="literalString">The untreated string of the literal.</param>
        /// <returns>
        /// Returns the datatype and sanitized value from the literal string.<br/>
        /// Returns string as datatype if the literal string didn't have one and the sanitiez value.
        /// </returns>
        public static (string datatype, string value) GetLiteralCleanData(string literalString)
        {
            if (!literalString.Contains("^^"))
                return ("string", literalString);

            var parts = literalString.Split("^^");
            var value = parts[0];
            var (_, dataType) = FormatService.GetLocalName(FormatService.SanitizeTypeAndUIDValues(parts[1]), "");
            return (dataType, value);
        }

        /// <summary>
        /// Obtains the RDF type and the valid pattern of a string value depending on its actual datatype.
        /// </summary>
        /// <param name="value">The value's string.</param>
        /// <returns>
        /// XsdType: RDF type equivalent of the corresponding type of the value. By default: xsd:string<br/>
        /// Pattern: String pattern of the corresponding type of the value. by default: ".*"
        /// </returns>
        public static (string XsdType, string Pattern) GetXsdType(string value)
        {
            var info = ("xsd:string", @".*") ;
            if (string.IsNullOrWhiteSpace(value)) return info;
            if (int.TryParse(value, out _)) return ("xsd:integer", @"^-?\d+$");
            if (double.TryParse(value, out _)) return ("xsd:float", @"^-?\d+\.\d+$");
            if (bool.TryParse(value, out _)) return ("xsd:boolean", @"^(true|false|1|0)$");
            if (DateTime.TryParse(value, out var dt)) return dt.TimeOfDay == TimeSpan.Zero ? ("xsd:date", @"^\d{4}-\d{2}-\d{2}$") : ("xsd:dateTime", @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.*$");
            return info;
        }

        public static string ReplacePrefix(string value, string newPrefix)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            int colonIndex = value.IndexOf(':');
            if (colonIndex < 0)
                return value;
            string rest = value[(colonIndex + 1)..];
            return $"{newPrefix}:{rest}";
        }

        public static void ReplacePrefix (JsonNode node, string newPrefix, string? innerPrefix = null)
        {
            if(node is JsonValue nodeVal)
                ReplacePrefix(nodeVal.ToString(), newPrefix);
            else if(!string.IsNullOrWhiteSpace(innerPrefix) && node is JsonObject nodeObj)
                nodeObj["@id"] = ReplacePrefix(nodeObj["@id"]!.GetValue<string>(), newPrefix);
            else if(node is JsonArray nodeArray)
                foreach(var innerNode in nodeArray)
                    if(innerNode is not null)
                        ReplacePrefix(innerNode, newPrefix, innerPrefix);
        }

        public static IEnumerable<(string Prefix, string Name)> GetNodesTypes(INode node, IGraph graph)
        {
            foreach (var triple in graph.GetTriplesWithSubject(node))
            {
                (_, var predicate) = GetLocalName(triple.Predicate, graph, "");
                if (predicate == "type")
                {
                    (var prefix, var type) = GetLocalName(triple.Object, graph, "");
                    yield return (prefix, type);
                }   
            }  
        }

        public static bool IsRelationBidirectional(INode subj, string prefixPredicate, string predicate,  string objPrefix, string obj, IGraph graph)
        {
            try
            {
                Uri predUri = new Uri(graph.NamespaceMap.GetNamespaceUri(prefixPredicate).AbsoluteUri + predicate);
                Uri objUri = new Uri(graph.NamespaceMap.GetNamespaceUri(objPrefix).AbsoluteUri + obj);
                IUriNode predNode = graph.GetUriNode(predUri);
                IUriNode objNode = graph.GetUriNode(objUri);

                if(predNode is null || objNode is null)
                    return false;

                Triple reversedTriple = new(objNode, predNode, subj);
                bool res = graph.ContainsTriple(reversedTriple);
                return res;
            }catch(Exception){
                return false;
            }
        }

        private static void ReplaceNulls(JsonNode? node, string replacement = "")
        {
            if (node is JsonObject obj)
            {
                // We must call .ToList() to snapshot the keys, 
                // because we cannot modify a collection while iterating over it.
                var keys = obj.Select(kvp => kvp.Key).ToList();
                
                foreach (var key in keys)
                {
                    if (obj[key] == null)
                    {
                        // It's a JSON null! Replace it.
                        obj[key] = replacement;
                    }
                    else
                    {
                        // It's an object or array, go deeper
                        ReplaceNulls(obj[key], replacement);
                    }
                }
            }
            else if (node is JsonArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] == null)
                    {
                        arr[i] = replacement;
                    }
                    else
                    {
                        ReplaceNulls(arr[i], replacement);
                    }
                }
            }
        }

        /// <summary>
        /// Obtains the RDF Graph equivalent of the Json provided.
        /// </summary>
        /// <param name="json">The Json object.</param>
        /// <param name="id">The identifier of the object.</param>
        /// <param name="ld">OPTIONAl. Whether the Json provided is in JsonLD format. By default is false.</param>
        /// <param name="twin"></param>
        /// <returns>Returns the RDF Graph of the Json.</returns>
        public static Graph GetRDFGraphFromJson(JsonObject json, string id, bool ld = false, bool twin = false)
        {
            var idSanitized = SanitizeTypeAndUIDValues(id);
            var store = new TripleStore();
            var jsonLd = ld ? json : ExportService.GetJsonLDFromRegularJson(json, id, twin:true);
            ReplaceNulls(jsonLd, "");
            var jsonString = JsonSerializer.Serialize(jsonLd);

            //extract the namespaces from the Json
            json.TryGetPropertyValue(ld ? "@context" :"namespace", out var nsJson);
            var ns = nsJson ?? new JsonObject();

            string? blankUri = ns is JsonObject nsObj ? nsObj[""]?.GetValue<string>() : ns.AsArray().FirstOrDefault(n => n!.AsObject()["prefix"]!.GetValue<string>() == "")?["uri"]?.GetValue<string>();
            
            if(blankUri is not null)
                jsonString = Regex.Replace(jsonString, "(?<=(:|,|\\[|\\{)\\s*)\":", $"\"{blankUri}");

            var parser = new VDS.RDF.Parsing.JsonLdParser();
            using var reader = new StringReader(jsonString);
            parser.Load(store, reader);

            var mergedGraph = new Graph();
            //load namespaces into graph for eventual prefix parsing to uri
            ExportService.LoadNamespaceIntoGraph(ns, mergedGraph, idSanitized);

            foreach (var g in store.Graphs)
                mergedGraph.Merge(g, true); // true = keep namespace mappings
            
            
            return mergedGraph;

        }

        /// <summary>
        /// Obtains the TTL File equivalent of the Json provided.
        /// </summary>
        /// <param name="id">The identifier of the object.</param>
        /// <param name="json">The Json object.</param>
        /// <param name="ld">OPTIONAL. Whether the Json provided has JsonLD format. By default is false.</param>
        /// <param name="twin">OPTIONAL. Whether the Json belongs to a Twin or not. By default is false.</param>
        /// <returns></returns>
        public static MemoryStream GetTTLFileFromRegularJson(string id, JsonObject json, bool ld = false, bool twin = false)
        {
            var mergedGraph = GetRDFGraphFromJson(json, id, ld, twin);

            var ttlWriter = new VDS.RDF.Writing.CompressingTurtleWriter();
            using var sw = new StringWriter();
            ttlWriter.Save(mergedGraph, sw);
            string ttlString = sw.ToString();
            Console.WriteLine(ttlString);

            //convert the string to bytes
            var ttlBytes = System.Text.Encoding.UTF8.GetBytes(ttlString);
            var stream = new MemoryStream(ttlBytes);

            return stream;
        }

    }
}