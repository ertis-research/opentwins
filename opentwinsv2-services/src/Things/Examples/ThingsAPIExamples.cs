using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
public class ThingsAPIExamples : APIOperationFilter
{
    public const string CreateExample = """
    {
        "id": "urn:examples:uniqueid",
        "title": "Example Thing",
        "@type": ["CustomType"],
        "properties": {
            "customProperty": {
                "type": "number",
                "default": 1
            }
        },
        "links": [
            {
                "rel": "urn:customRelation",
                "href": "urn:example:otherid"   
            }
        ]
    } 
    """;

    public const string CreateBulkExample = """
    [
        {
            "id": "urn:examples:uniqueid",
            "title": "Example Thing",
            "@type": ["CustomType"],
            "properties": {
                "customProperty": {
                    "type": "number",
                    "default": 1
                }
            },
            "links": [
                {
                    "rel": "urn:customRelation",
                    "href": "urn:example:otherid"   
                }
            ]
        },
        {
            "id": "urn:example:otherid",
            "title": "Another Example Thing",
            "@type" : ["AnotherType", "CustomType"],
            "properties": {
                "customProperty": {
                    "type": "number",
                    "default": 1
                },
                "anotherProperty": {
                    "type": "string",
                    "default": "hello"
                }
            }
        }
    ]
    """;

    public const string DeleteBulkExample = """
    [
        {
            "id": "urn:examples:uniqueid"
        },
        {
            "@id": "urn:example:otherid"
        },
        {
            "id": "urn:example:anotherid",
            "irrelevantProperty": 1,
            "anotherProperty": "hello"
        }
    ]
    """;

    public const string CreateLinks = """
    [
        {
            "rel": "relationName",
            "href": "urn:example:otherid"
        },
        {
            "rel": "anotherRelation",
            "href": "urn:example:anotherid"
        },
        {
            "rel": "relationName",
            "href": "urn:example:anotherid"
        }
    ]
    """;

    public const string UpdateLink = """
    {
        "rel": "relationNameInParameter",
        "href": "urn:example:otherid"
    }
    """;

    public const string CreateSubscription = """
    {
        "otv2:event": "name:of:the:event",
        "otv2:source" : [
            "eventSource1",
            "eventSource2"
        ]
    }
    """;
}

