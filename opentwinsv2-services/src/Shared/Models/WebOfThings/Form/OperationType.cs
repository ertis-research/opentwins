using System.Runtime.Serialization;

namespace OpenTwinsV2.Shared.Models
{
    public enum OperationType
    {
        [EnumMember(Value = "readproperty")]
        ReadProperty,

        [EnumMember(Value = "writeproperty")]
        WriteProperty,

        [EnumMember(Value = "observeproperty")]
        ObserveProperty,

        [EnumMember(Value = "unobserveproperty")]
        UnobserveProperty,

        [EnumMember(Value = "invokeaction")]
        InvokeAction,

        [EnumMember(Value = "queryaction")]
        QueryAction,

        [EnumMember(Value = "cancelaction")]
        CancelAction,

        [EnumMember(Value = "subscribeevent")]
        SubscribeEvent,

        [EnumMember(Value = "unsubscribeevent")]
        UnsubscribeEvent,

        [EnumMember(Value = "readallproperties")]
        ReadAllProperties,

        [EnumMember(Value = "writeallproperties")]
        WriteAllProperties,

        [EnumMember(Value = "readmultipleproperties")]
        ReadMultipleProperties,

        [EnumMember(Value = "writemultipleproperties")]
        WriteMultipleProperties,

        [EnumMember(Value = "observeallproperties")]
        ObserveAllProperties,

        [EnumMember(Value = "unobserveallproperties")]
        UnobserveAllProperties,

        [EnumMember(Value = "subscribeallevents")]
        SubscribeAllEvents,

        [EnumMember(Value = "unsubscribeallevents")]
        UnsubscribeAllEvents,

        [EnumMember(Value = "queryallactions")]
        QueryAllActions
    }
}