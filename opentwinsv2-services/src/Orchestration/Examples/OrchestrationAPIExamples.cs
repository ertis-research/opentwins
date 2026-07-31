public class OrchestrationAPIExamples : APIOperationFilter
{
    public const string CreateConnection = """
    {
        "@context": [
            "https://www.w3.org/2022/wot/td/v1.1"
        ],
        "id": "urn:connections:kafka:example",
        "title": "Kafka Broker Configuration",
        "properties": {
            "topics": {
                "observable": false,
                "type": "array",
                "readOnly": true,
                "const": [
                    "opentwinsv2.events"
                ],
                "forms": []
            },
            "addresses": {
                "observable": false,
                "type": "array",
                "readOnly": true,
                "const": [
                    "kafka-address1:9092",
                    "kafka-address2:9092",
                    "kafka-address3:9092"
                ],
                "forms": []
            },
            "consumer_group": {
                "observable": false,
                "type": "string",
                "readOnly": true,
                "const": "consumers-group",
                "forms": []
            },
            "target_version": {
                "observable": false,
                "type": "string",
                "readOnly": true,
                "const": "",
                "forms": []
            },
            "checkpoint_limit": {
                "observable": false,
                "type": "integer",
                "readOnly": true,
                "const": 1024,
                "forms": []
            },
            "auto_replay_nacks": {
                "observable": false,
                "type": "boolean",
                "readOnly": true,
                "const": true,
                "forms": []
            }
        }
    }
    """;
}