using Dapr.Actors;
using Dapr.Actors.Runtime;
using System.Threading.Tasks;

namespace OpenTwinsv2.Things.Models
{
    public interface ITestActor : IActor
    {       
        Task<string> SetDataAsync(MyData data);
        Task<MyData> GetDataAsync();
        Task RegisterReminder();
        Task UnregisterReminder();
        Task<IActorReminder> GetReminder();
        Task RegisterTimer();
        Task UnregisterTimer();
    }

    public class MyData
    {
        public required string PropertyA { get; set; }
        public required string PropertyB { get; set; }

        public override string ToString()
        {
            var propAValue = this.PropertyA == null ? "null" : this.PropertyA;
            var propBValue = this.PropertyB == null ? "null" : this.PropertyB;
            return $"PropertyA: {propAValue}, PropertyB: {propBValue}";
        }
    }
}