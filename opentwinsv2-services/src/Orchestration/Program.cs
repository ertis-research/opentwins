//dapr run --app-id orchestration-service --app-port 5117 --dapr-http-port 3501 -- dotnet run

using Dapr;
using OpenTwinsV2.Orchestration.Services;
using OpenTwinsV2.Orchestration.Formatters;
using OpenTwinsV2.Orchestration.Clients;
using OpenTwinsV2.Orchestration.Error;
using k8s;
using Dapr.Client;
using Orchestration.Services;
// using Dapr.Messaging.PublishSubscribe;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddDaprClient();
builder.Services.AddControllers(options => options.InputFormatters.Insert(0, new TextPlainInputFormatter())).AddDapr();
builder.Services.AddSingleton(sp =>
{
    var client = DaprClient.CreateInvokeHttpClient("things-service");
    return new ThingsClient(client);
});
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
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