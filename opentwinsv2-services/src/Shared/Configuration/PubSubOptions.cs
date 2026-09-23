namespace OpenTwinsV2.Shared.Configuration
{
    public class PubSubOptions
    {
        public const string SectionName = "Dapr:PubSub";
        public string Name { get; set; } = "kafka-pubsub";
        public string EventsTopic { get; set; } = "opentwinsv2.events";
        public string ThingDescriptionChangesTopic { get; set; } = "thing.description.changes";
        public string ThingDescriptionDeletedTopic { get; set; } = "thing.description.deleted";
        public string ThingUpdateTopic { get; set; } = "update-things";
        public string ThingDeleteTopic { get; set; } = "delete-things";

    }
}