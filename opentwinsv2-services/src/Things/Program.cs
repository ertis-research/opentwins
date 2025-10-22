// dapr run --app-id things-service --app-port 5001 --app-protocol http --dapr-http-port 56001 --config ./daprConfig.yaml --resources-path ./Infrastructure/DaprComponentsLocal  -- dotnet run --urls=http://localhost:5001/

using System.Text.Json.Serialization.Metadata;
using OpenTwinsV2.Things.Actors;
using OpenTwinsV2.Things.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});
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

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok("Actor service is running"));

app.MapActorsHandlers();

app.MapControllers();

app.Run();

