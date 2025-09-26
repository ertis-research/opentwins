// dapr run --app-id twins-service --app-port 5013 -- dotnet run --urls=http://localhost:5013/

using Dgraph4Net.ActiveRecords;
using OpenTwinsV2.Twins.Services;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddDaprPubSubClient();
builder.Services.AddDaprClient();                    // Recomendado para llamadas Dapr

builder.Services.AddScoped<IJsonNquadsConverter, JsonNquadsConverter>();

builder.Services.AddScoped<DGraphService>();
builder.Services.AddScoped<ThingsService>();
builder.Services.AddControllers().AddDapr();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

ClassMapping.Map();

await app.RunAsync();