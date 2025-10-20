using Dapr.Actors;
using Dapr.Actors.Runtime;

namespace OpenTwinsV2.Shared.Models
{
    public interface IThingActor : IActor
    {
        Task<string> SetThingDescriptionAsync(string data);
        Task<string?> GetThingDescriptionAsync();
        Task<string> GetCurrentStateAsync();
        Task<bool> DeleteThingAsync();
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
        Task<string> AddLinkAsync(string v);
        Task RemoveLinkAsync(string href, string relName);
        Task<string> UpdateLinkAsync(string targetId, string relName, string newLink);
        Task<string> AddSubscriptionAsync(string v);
        Task<string> RemoveSubscriptionAsync(string eventName);
    }

}