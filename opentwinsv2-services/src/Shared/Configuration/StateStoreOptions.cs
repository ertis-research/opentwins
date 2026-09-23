namespace OpenTwinsV2.Shared.Configuration
{
    public class StateStoreOptions
    {
        public const string SectionName = "Dapr:StateStore";

        public string Name { get; set; } = "actorstatestore";
        public string TtlSeconds { get; set; } = "60";
    }
}