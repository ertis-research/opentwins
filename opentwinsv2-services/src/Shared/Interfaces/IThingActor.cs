using Dapr;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using OpenTwinsv2.Things.Models;
using Shared.Models;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenTwinsv2.Things.Interfaces
{
    public interface IThingActor : IActor
    {
        Task<string> SetThingDescriptionAsync(string data);
        Task<string> GetThingDescriptionAsync();
        Task<Dictionary<string, PropertyState>> GetCurrentStateAsync();
        Task OnEventReceived(MyCloudEvent<string> eventRecv);
        Task InvokeAction(string actionName, string parameters);
        Task SendEvent(MyCloudEvent<string> evnt);
        /*
        ¿Que debe hacer el actor?
        - Estar subcrito a eventos
        - Enviar eventos a Apache Kafka
        */
        //Task<Dictionary<string, PropertyAffordance>> GetPropertiesAsync();
        //Task<bool> SetPropertyAsync(string propertyName, object value);
        //Task InvokeActionAsync(string actionName, object parameters);
        Task RegisterReminder();
        Task UnregisterReminder();
        Task<IActorReminder> GetReminder();
        Task RegisterTimer();
        Task UnregisterTimer();

    }

}