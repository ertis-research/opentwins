using Dapr.Client;
using Events.Services;
using Dapr.Messaging.PublishSubscribe;
using Events.Handlers;

public class SubscriptionManager
{
    private readonly DaprPublishSubscribeClient _messagingClient;
    private readonly RoutingService _routingService;
    private readonly EventProcessor _eventProcessor;
    private readonly string _pubsubName = "mypubsub";
    private readonly object _lock = new();

    // Track topics already subscribed to
    private readonly HashSet<string> _subscribedTopics = [];

    public SubscriptionManager(DaprPublishSubscribeClient messagingClient, RoutingService routingService, EventProcessor eventProcessor)
    {
        _messagingClient = messagingClient;
        _routingService = routingService;
        _eventProcessor = eventProcessor;
    }

    public async Task InitializeSubscriptionsAsync()
    {
        await RefreshSubscriptionsAsync();
    }

    public async Task SubscribeToTopicIfNeeded(string topic)
    {
        lock (_lock)
        {
            if (_subscribedTopics.Contains(topic))
                return;

            _subscribedTopics.Add(topic);
        }
        Console.WriteLine("Leyendo topic " + topic);

        await _messagingClient.SubscribeAsync(
            _pubsubName,
            topic,
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Drop)),
            async (msg, ct) =>
            {
                await _eventProcessor.EnqueueEvent(msg, topic);
                return TopicResponseAction.Success; // ya no haces procesamiento aquí
            },
            CancellationToken.None);
    }

    // Call this method whenever routes are updated
    public async Task RefreshSubscriptionsAsync()
    {
        var topics = _routingService.GetAllTopics();

        foreach (var topic in topics)
        {
            await SubscribeToTopicIfNeeded(topic);
        }
    }
}