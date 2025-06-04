using System;
using System.Runtime.Serialization;

namespace Shared.Models
{
    [DataContract]
    public class MyCloudEvent<T>
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string Source { get; set; }

        [DataMember(Order = 3)]
        public string Type { get; set; }

        [DataMember(Order = 4)]
        public string SpecVersion { get; set; }

        [DataMember(Order = 5)]
        public string? Subject { get; set; }

        [DataMember(Order = 6)]
        public DateTime? Time { get; set; }

        [DataMember(Order = 7)]
        public string? DataContentType { get; set; }

        [DataMember(Order = 8, EmitDefaultValue = false)]
        public T? Data { get; set; }

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