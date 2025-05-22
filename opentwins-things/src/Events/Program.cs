// Execute with: dapr run --app-id myapp --app-port 5000 --resources-path ./Components -- dotnet run

using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Events.Services;
using Events.Handlers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprPubSubClient();
builder.Services.AddSingleton<RoutingService>();
var app = builder.Build();

var routingService = app.Services.GetRequiredService<RoutingService>();
var messagingClient = app.Services.GetRequiredService<DaprPublishSubscribeClient>();

foreach (var topic in routingService.GetAllTopics())
{
    await messagingClient.SubscribeAsync("mypubsub", topic,
        new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Drop)),
        (msg, ct) => Events.Handlers.EventHandler.Handle(msg, topic, routingService), CancellationToken.None);
}

app.MapControllers();

await app.RunAsync();