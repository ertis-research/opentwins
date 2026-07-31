using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class TwinsAPIExamples : APIOperationFilter
{
    public const string OntologyInstanciateGraph = """
    {
        "@graph": [
            {
                "@id": "urn:example:thing1",
                "@type": "ontologyId:thingInOntology"
            },
            {
                "@id": "urn:example:thing2",
                "@type": "ontologyId:thingInOntology",
                "relationInThingInOntology": [
                    {
                        "@id": "urn:example:thing1"
                    }
                ]
            }
        ]
    }
    """;

    public const string SparQLQuery = """
        PREFIX  prefix: <http://example.org/prefix#>
        PREFIX  : <http://example.org/empty/>
        SELECT  $obj
        WHERE   { :thing1  prefix:relation  $obj }
    """;

    public const string CreateTwin = """
    {
        "@graph": [
            {
                "@id": "urn:example:existingThingId"
            },
            {
                "@id": "urn:example:anotherExistingThing",
                "twinOnlyRelation": [
                    {
                        "@id": "urn:example:existingThingId"
                    }
                ]
            }
        ]
    }
    """;
}