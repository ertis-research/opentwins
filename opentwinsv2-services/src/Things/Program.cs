// dapr run --app-id things-service --app-port 5001 --app-protocol http --dapr-http-port 56001 --config ./daprConfig.yaml --resources-path ./Infrastructure/DaprComponentsLocal  -- dotnet run --urls=http://localhost:5001/

using System.Text.Json.Serialization.Metadata;
using OpenTwinsV2.Things.Infrastructure.Database;
using OpenTwinsV2.Things.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, new DefaultJsonTypeInfoResolver());
});

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
    options.DrainRebalancedActors = true;
    options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, new DefaultJsonTypeInfoResolver());
});

builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connString = configuration.GetSection("PostgreSQL")["connectionString"];

    if (string.IsNullOrWhiteSpace(connString)) throw new InvalidOperationException("The connection string to PostgreSQL is not configured in appsettings.json under the key 'PostgreSQL:connectionString'.");

    return new NpgsqlConnectionFactory(connString);
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

