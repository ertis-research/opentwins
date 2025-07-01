namespace Events.Handlers
{
    public class SubscriptionInitializer : IHostedService
    {
        private readonly SubscriptionManager _subscriptionManager;

        public SubscriptionInitializer(SubscriptionManager subscriptionManager)
        {
            _subscriptionManager = subscriptionManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _subscriptionManager.InitializeSubscriptionsAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}