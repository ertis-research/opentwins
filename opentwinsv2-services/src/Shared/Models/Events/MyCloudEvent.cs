using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public class MyCloudEvent<T>
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("specversion")]
        public string? SpecVersion { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("time")]
        public DateTime? Time { get; set; }

        [JsonPropertyName("datacontenttype")]
        public string? DataContentType { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("topic")]
        public string? Topic { get; set; }

        [JsonPropertyName("traceid")]
        public string? TraceId { get; set; }

        [JsonPropertyName("pubsubname")]
        public string? PubSubName { get; set; }

        [JsonPropertyName("traceparent")]
        public string? TraceParent { get; set; }

        [JsonPropertyName("tracestate")]
        public string? TraceState { get; set; }

        public MyCloudEvent() { }

        public MyCloudEvent(
            string id,
            string source,
            string type,
            string specVersion,
            string? subject = null,
            DateTime? time = null,
            string? dataContentType = null,
            T? data = default)
        {
            Id = id;
            Source = source;
            Type = type;
            SpecVersion = specVersion;
            Subject = subject;
            Time = time;
            DataContentType = dataContentType;
            Data = data;
        }
    }
}