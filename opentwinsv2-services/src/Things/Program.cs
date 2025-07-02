// dapr run --app-id things-service --app-port 5001 --app-protocol http --dapr-http-port 56001 --config ./daprConfig.yaml --resources-path ./DaprComponentsLocal  -- dotnet run --urls=http://localhost:5001/

using OpenTwinsv2.Things.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddActors(options =>
{
    // Register actor types and configure actor settings
    //options.Actors.RegisterActor<TestActor>();
    options.Actors.RegisterActor<ThingActor>();
    options.ReentrancyConfig = new Dapr.Actors.ActorReentrancyConfig()
    {
        Enabled = true,
        MaxStackDepth = 16,
    };

    options.ActorIdleTimeout = TimeSpan.FromSeconds(30);           // idleTimeout
    options.ActorScanInterval = TimeSpan.FromSeconds(10);          // scanInterval
    options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(30);    // drainOngoingCallTimeout
    options.DrainRebalancedActors = true;                           // drainRebalancedActors
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // By default, ASP.Net Core uses port 5000 for HTTP. The HTTP
    // redirection will interfere with the Dapr runtime. You can
    // move this out of the else block if you use port 5001 in this
    // example, and developer tooling (such as the VSCode extension).
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok("Actor service is running"));

app.MapActorsHandlers();

app.MapControllers();

app.Run();

