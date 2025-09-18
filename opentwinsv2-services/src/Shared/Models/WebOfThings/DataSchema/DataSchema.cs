using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using OpenTwinsV2.Shared.Converters;

namespace OpenTwinsV2.Shared.Models
{
    [DataContract]
    [KnownType(typeof(ArraySchema))]
    [KnownType(typeof(BooleanSchema))]
    [KnownType(typeof(IntegerSchema))]
    [KnownType(typeof(NullSchema))]
    [KnownType(typeof(ObjectSchema))]
    [KnownType(typeof(StringSchema))]
    [KnownType(typeof(NumberSchema))]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(ArraySchema), typeDiscriminator: "array")]
    [JsonDerivedType(typeof(BooleanSchema), typeDiscriminator: "boolean")]
    [JsonDerivedType(typeof(IntegerSchema), typeDiscriminator: "integer")]
    [JsonDerivedType(typeof(NullSchema), typeDiscriminator: "null")]
    [JsonDerivedType(typeof(ObjectSchema), typeDiscriminator: "object")]
    [JsonDerivedType(typeof(StringSchema), typeDiscriminator: "string")]
    [JsonDerivedType(typeof(NumberSchema), typeDiscriminator: "number")]
    public abstract class DataSchema
    {
        [JsonPropertyName("@type")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string>? TypeAnnotation { get; set; } // Puede ser string o array de string

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("titles")]
        public Dictionary<string, string>? Titles { get; set; } // Multi-language titles

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("descriptions")]
        public Dictionary<string, string>? Descriptions { get; set; } // Multi-language descriptions

        [JsonPropertyName("const")]
        public object? Const { get; set; }

        [JsonPropertyName("default")]
        public object? Default { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("oneOf")]
        public List<DataSchema>? OneOf { get; set; }

        [JsonPropertyName("enum")]
        public List<object>? Enum { get; set; }

        [JsonPropertyName("readOnly")]
        public bool ReadOnly { get; set; } = false;

        [JsonPropertyName("writeOnly")]
        public bool WriteOnly { get; set; } = false;

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonIgnore]
        public abstract string? Type { get; }
    }
}