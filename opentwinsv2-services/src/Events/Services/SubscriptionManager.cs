using Dapr.Client;
using Events.Services;
using Dapr.Messaging.PublishSubscribe;

public class SubscriptionManager
{
    private readonly DaprPublishSubscribeClient _messagingClient;
    private readonly RoutingService _routingService;
    private readonly string _pubsubName = "mypubsub";
    private readonly object _lock = new();

    // Track topics already subscribed to
    private readonly HashSet<string> _subscribedTopics = [];

    public SubscriptionManager(DaprPublishSubscribeClient messagingClient, RoutingService routingService)
    {
        _messagingClient = messagingClient;
        _routingService = routingService;
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
                (msg, ct) => Events.Handlers.EventHandler.Handle(msg, topic, _routingService), CancellationToken.None);
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