// Execute with: dapr run --app-id myapp --app-port 5000 --resources-path ./Components -- dotnet run

using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprPubSubClient();
var app = builder.Build();

var messagingClient = app.Services.GetRequiredService<DaprPublishSubscribeClient>();

Console.WriteLine("Me subscribo");
//Create a dynamic streaming subscription
var subscription = await messagingClient.SubscribeAsync("mypubsub", "opentwinsv2-events",
    new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry)),
    HandleMessageAsync, CancellationToken.None);


// Test with: dapr publish --pubsub mypubsub --topic opentwinsv2-events --data '{ "type": "event", "value": 42 }' --publish-app-id myapp
Task<TopicResponseAction> HandleMessageAsync(TopicMessage message, CancellationToken cancellationToken = default)
{
    try
    {
        //Do something with the message
        Console.WriteLine("Ha llegado algo mi reina");
        Console.WriteLine(Encoding.UTF8.GetString(message.Data.Span));
        //Console.WriteLine(Encoding.UTF8.GetString(message.Data.Span));
        return Task.FromResult(TopicResponseAction.Success);
    }
    catch
    {
        return Task.FromResult(TopicResponseAction.Drop);
    }
}

await app.RunAsync();