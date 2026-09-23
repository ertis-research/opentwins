namespace OpenTwinsV2.Shared.Constants
{
    public class PubSub
    {
        public const string Name = "kafka-pubsub";
        public const string EventsTopic = "opentwinsv2.events";
        public const string ThingDescriptionChangesTopic = "thing.description.changes";
        public const string ThingDescriptionDeletedTopic = "thing.description.deleted";
        public const string ThingUpdateTopic = "update-things";
        public const string ThingDeleteTopic = "delete-things";

    }
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