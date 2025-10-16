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
    private readonly ILogger<SubscriptionManager> _logger;

    // Track topics already subscribed to
    private readonly HashSet<string> _subscribedTopics = [];

    public SubscriptionManager(DaprPublishSubscribeClient messagingClient, RoutingService routingService, EventProcessor eventProcessor, ILogger<SubscriptionManager> logger)
    {
        _messagingClient = messagingClient;
        _routingService = routingService;
        _eventProcessor = eventProcessor;
        _logger = logger;
    }

    public async Task InitializeSubscriptionsAsync()
    {
        await RefreshSubscriptionsAsync();
    }

    public async Task SubscribeToTopicIfNeeded(string topic)
    {
        _logger.LogDebug("Checking if subscription is needed for topic '{Topic}'", topic);

        lock (_lock)
        {
            if (_subscribedTopics.Contains(topic))
            {
                _logger.LogDebug("Already subscribed to topic '{Topic}', skipping.", topic);
                return;
            }
            _subscribedTopics.Add(topic);
        }

        _logger.LogInformation("Subscribing to topic '{Topic}'", topic);

        await _messagingClient.SubscribeAsync(
            _pubsubName,
            topic,
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Drop)),
            async (msg, ct) =>
            {
                _logger.LogTrace("Received message on topic '{Topic}'. Enqueuing for processing.", topic);
                await _eventProcessor.EnqueueEvent(msg, topic);
                _logger.LogTrace("Message from topic '{Topic}' successfully enqueued.", topic);
                return TopicResponseAction.Success; // ya no haces procesamiento aquí
            },
            CancellationToken.None);
    }

    // Call this method whenever routes are updated
    public async Task RefreshSubscriptionsAsync()
    {
        _logger.LogDebug("Refreshing topic subscriptions...");
        var topics = _routingService.GetAllTopics();
        _logger.LogDebug("Found {Count} topics to subscribe to.", topics.Count());

        foreach (var topic in topics)
        {
            await SubscribeToTopicIfNeeded(topic);
        }
    }
}