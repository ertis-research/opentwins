// Execute with: dapr run --app-id events-service --app-port 5012 --resources-path ./Components -- dotnet run --urls=http://localhost:5012/

using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Events.Services;
using Events.Handlers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprPubSubClient();
builder.Services.AddSingleton<RoutingService>();
builder.Services.AddSingleton<SubscriptionManager>();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var subscriptionManager = app.Services.GetRequiredService<SubscriptionManager>();
//var messagingClient = app.Services.GetRequiredService<DaprPublishSubscribeClient>();
await subscriptionManager.InitializeSubscriptionsAsync();

/*
foreach (var topic in routingService.GetAllTopics())
{
    await messagingClient.SubscribeAsync("mypubsub", topic,
        new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Drop)),
        (msg, ct) => Events.Handlers.EventHandler.Handle(msg, topic, routingService), CancellationToken.None);
}*/

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.MapGet("/orders", () =>
{
    Console.WriteLine("Order received : ");
    return;
});

await app.RunAsync();