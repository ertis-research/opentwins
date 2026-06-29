namespace OpenTwinsV2.Shared.Constants
{
    public static class PubSub
    {
        public const string Name = "kafka-pubsub";
        public const string EventsTopic = "opentwinsv2.events";
        public const string ThingDescriptionChangesTopic = "thing.description.changes";
        public const string ThingDescriptionDeletedTopic = "thing.description.deleted";
        public const string ThingUpdateTopic = "update-things";
        public const string ThingDeleteTopic = "delete-things";

    }
}