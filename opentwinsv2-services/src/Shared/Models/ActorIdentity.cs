namespace OpenTwinsV2.Shared.Models
{
    public class ActorIdentity(string actorId, string actorType) : IEquatable<ActorIdentity>
    {
        public string ActorId { get; set; } = actorId;
        public string ActorType { get; set; } = actorType;

        public override bool Equals(object? obj)
        {
            return Equals(obj as ActorIdentity);
        }

        public bool Equals(ActorIdentity? other)
        {
            return other != null &&
                    ActorId == other.ActorId &&
                    ActorType == other.ActorType;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ActorId, ActorType);
        }

        public override string ToString()
        {
            return $"{ActorType}:{ActorId}";
        }
    }
}