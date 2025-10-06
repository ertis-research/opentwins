// dapr run --app-id twins-service --app-port 5013 --resources-path ./Infrastructure/DaprComponentsLocal -- dotnet run --urls=http://localhost:5013/

using System.Text.Json;
using Dapr;
using Dapr.Messaging.PublishSubscribe;
using Dgraph4Net.ActiveRecords;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Twins.Handlers;
using OpenTwinsV2.Twins.Services;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddDaprPubSubClient();
builder.Services.AddDaprClient();                    // Recomendado para llamadas Dapr

builder.Services.AddScoped<IJsonNquadsConverter, JsonNquadsConverter>();
builder.Services.AddScoped<LinkEventsHandler>();
builder.Services.AddScoped<DGraphService>();
builder.Services.AddScoped<ThingsService>();
builder.Services.AddControllers().AddDapr();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCloudEvents();

app.MapControllers();
app.MapSubscribeHandler();

ClassMapping.Map();

await app.RunAsync();