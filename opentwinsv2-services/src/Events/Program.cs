// Execute with: dapr run --app-id events-service --app-port 5012 --resources-path ./Infrastructure/DaprComponentsLocal --config ./daprConfig.yaml -- dotnet run --urls=http://localhost:5012/

using Dapr.Messaging.PublishSubscribe.Extensions;
using Events.Handlers;
using Events.Persistence;
using Events.Services;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetSection("PostgreSQL")["connectionString"];
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Level:u5}] [{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

// === Servicios Dapr y de aplicación ===
builder.Services.AddDaprClient();                    // Recomendado para llamadas Dapr
builder.Services.AddDaprPubSubClient();              // Cliente Pub/Sub

builder.Services.AddSingleton<PersistenceBuffer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PersistenceBuffer>());
builder.Services.AddSingleton<FastEventConfig>();
builder.Services.AddSingleton<RoutingRepository>();
builder.Services.AddSingleton<RoutingService>();     // Enrutador de actores
builder.Services.AddSingleton<ActorEventRouter>();
builder.Services.AddSingleton<EventProcessor>();     // Cola + procesamiento paralelo
builder.Services.AddSingleton<SubscriptionManager>(); // Gestor de suscripciones
builder.Services.AddHostedService<SubscriptionInitializer>(); // Inicializador al arranque

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//var subscriptionManager = app.Services.GetRequiredService<SubscriptionManager>();
//var messagingClient = app.Services.GetRequiredService<DaprPublishSubscribeClient>();
//await subscriptionManager.InitializeSubscriptionsAsync();

// === Middlewares ===
app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();

app.UseCloudEvents();           // Necesario para Pub/Sub con Dapr
app.MapControllers();
app.MapSubscribeHandler();      // Mapea endpoint para suscripciones de Dapr

await app.RunAsync();