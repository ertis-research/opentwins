//dapr run --app-id orchestration-service --app-port 5117 --dapr-http-port 3501 -- dotnet run

using Dapr;
using OpenTwinsV2.Orchestration.Services;
using OpenTwinsV2.Orchestration.Formatters;
using OpenTwinsV2.Orchestration.Clients;
using OpenTwinsV2.Orchestration.Error;
using k8s;
using Dapr.Client;
using Orchestration.Services;
using Yarp.ReverseProxy.Configuration;
// using Dapr.Messaging.PublishSubscribe;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDaprClient();
builder.Services.AddControllers(options => options.InputFormatters.Insert(0, new TextPlainInputFormatter())).AddDapr();

var daprServices = builder.Configuration.GetSection("Dapr").Get<Dictionary<string, string>>() ?? [];
builder.Services.AddSingleton(sp =>
{
    var client = DaprClient.CreateInvokeHttpClient(daprServices.GetValueOrDefault("Things", "things-service"));
    return new ThingsClient(client);
});

// Configure reverse proxy for Dapr services
var routes = new List<RouteConfig>();
foreach (var service in daprServices)
{
    routes.Add(new RouteConfig
    {
        RouteId = $"{service.Key}-route",
        ClusterId = "dapr-sidecar",
        Match = new RouteMatch { Path = $"/{service.Key}/{{**catch-all}}" },
        Transforms =
        [
            new Dictionary<string, string>
            {
                { "PathPattern", $"/v1.0/invoke/{service.Value}/method/{{**catch-all}}" }
            }
        ]
    });
}
var daprPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3501";
var clusters = new List<ClusterConfig>
{
    new() {
        ClusterId = "dapr-sidecar",
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "dapr-local", new DestinationConfig { Address = $"http://localhost:{daprPort}/" } }
        }
    }
};
builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);
// End of reverse proxy configuration

builder.Services.AddSingleton<IKubernetes>(sp =>
{
    var config = KubernetesClientConfiguration.IsInCluster()
        ? KubernetesClientConfiguration.InClusterConfig()
        : KubernetesClientConfiguration.BuildConfigFromConfigFile();

    return new Kubernetes(config);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHostedService<NamespaceInitializer>();
builder.Services.AddScoped<BenthosService>();
builder.Services.AddScoped<KubernetesService>();
builder.Services.AddSwaggerGen(options =>
{
    options.DocumentFilter<UnifiedSwaggerFilter>();
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    options.OperationFilter<OrchestrationAPIExamples>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Orchestration V1");
});
app.UseDeveloperExceptionPage();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseAuthorization();
app.UseCloudEvents();
app.MapControllers();
app.MapSubscribeHandler();
app.MapReverseProxy();

app.UseHttpsRedirection();

// ClassMapping.Map();

await app.RunAsync();