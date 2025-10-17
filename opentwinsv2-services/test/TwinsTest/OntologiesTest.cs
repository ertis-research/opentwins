namespace TwinsTest;

using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Twins;
using OpenTwinsV2.Twins.Services;
using System.Linq;
using System.Text.Json;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Dapr.Actors;
using OpenTwinsV2.Things.Actors;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Shared.Models;
using Dapr.Actors.Client;

public class OntologiesAPIFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; } = null!;
    public HttpClient ThingsClient { get; private set; } = null!;
    private WebApplicationFactory<Twins.TestMaker> _factory = null!;
    // public IServiceProvider Services => _factory.Services;
    public DGraphService DGraphService { get; private set; } = null!;
    public ThingsService ThingsService { get; private set; } = null!;
    public Func<IServiceScope> CreateScope { get; private set; } = null!;
    private Process _ThingsProcess = null!;
    private Process _TwinsProcess = null!;

    public string ontologyId = "ontologiaPrueba";

    public string thingId = null!;
    public string relationName = null!;
    public string attributeKey = null!;
    public string instanciatedThingId = "instanciatedThingId";

    private void getFirstIds(List<string> nquads)
    {
        var pattern = @"^.+<(?<predicate>[^>]+)>\s+""(?<object>[^""]+)""";
        List<string> remaining = new List<string> { "thingId", "Relation.name", "Attribute.key" };
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nquad in nquads)
        {
            if (remaining.Count == 0)
                break;
            var match = Regex.Match(nquad, pattern);

            if (!match.Success)
                continue;

            var predicate = match.Groups["predicate"].Value;
            var value = match.Groups["object"].Value;

            if (remaining.Contains(predicate, StringComparer.OrdinalIgnoreCase))
            {
                results[predicate] = value;
                remaining.Remove(predicate);
            }

        }

        //all values should be instanced, if not -> ""
        thingId = results["thingId"] ?? "";
        relationName = results["Relation.name"] ?? "";
        attributeKey = results["Attribute.key"] ?? "";

    }

    private Process StartDaprProcess(string appId, int appPort, int daprHttpPort, string configPath, string resourcesPath, string workingDir, int grpcPort)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dapr",
            Arguments = $"run --app-id {appId} --app-port {appPort} --dapr-http-port {daprHttpPort} {(configPath.Equals("") ? "" : "--config")} {configPath} --resources-path {resourcesPath} -- dotnet run --urls=http://localhost:{appPort}/",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.EnvironmentVariables["DAPR_HTTP_PORT"] = daprHttpPort.ToString();
        psi.EnvironmentVariables["DAPR_APP_ID"] = appId;
        psi.EnvironmentVariables["APP_PORT"] = appPort.ToString(); ;
        psi.EnvironmentVariables["DAPR_GRPC_PORT"] = grpcPort.ToString();
        var process = new Process
        {
            StartInfo = psi  
            
        };
        process.Start();
        return process;
    }
    
    private async Task WaitForDaprReadyAsync(int daprPort, int maxRetries = 30)
    {
        using var http = new HttpClient();
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var res = await http.GetAsync($"http://localhost:{daprPort}/v1.0/healthz");
                if (res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Dapr on port {daprPort} ready");
                    return;
                }
            }
            catch { }
            await Task.Delay(1000);
        }
        throw new Exception($"Dapr on port {daprPort} did not start in time.");
    }

    private async Task WaitForServiceReadyAsync(string url, int maxRetries = 30)
    {
        using var http = new HttpClient();
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var res = await http.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Service ready at {url}");
                    return;
                }
            }
            catch { }
            await Task.Delay(1000);
        }
        throw new Exception($"Service at {url} did not start in time.");
    }
    public async Task InitializeAsync()
    {
        var daprThingsComponentsPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "src", "Things", "Infrastructure", "DaprComponentsLocal"));
        var daprThingsConfigPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "src", "Things", "daprConfig.yaml"));
        var daprTwinsComponentsPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "src", "Twins", "Infrastructure", "DaprComponentsLocal"));
        var daprTwinsConfigPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "src", "Twins", "daprConfig.yaml"));

        // Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", "56001");

        // _ThingsProcess = new Process
        // {
        //     StartInfo = new ProcessStartInfo
        //     {
        //         FileName = "dapr",
        //         Arguments = $"run --app-id things-service --app-port 5001 --app-protocol http --dapr-http-port 56001 --config {daprThingsConfigPath} --resources-path {daprThingsComponentsPath} -- dotnet run --urls=http://localhost:5001/",
        //         WorkingDirectory = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "src", "Things")), // adjust path to service project
        //         RedirectStandardOutput = true,
        //         RedirectStandardError = true,
        //         UseShellExecute = false
        //     }
        // };
        // _ThingsProcess.Start();



        // var tcs = new TaskCompletionSource();
        // _ThingsProcess.OutputDataReceived += (sender, e) =>
        // {
        //     if (e.Data != null && e.Data.Contains("You're up and running!"))
        //         tcs.TrySetResult();
        // };
        // _ThingsProcess.BeginOutputReadLine();
        // await tcs.Task;


        // _TwinsProcess = new Process
        // {
        //     StartInfo = new ProcessStartInfo
        //     {
        //         FileName = "dapr",
        //         Arguments = $"run --app-id twins-service --app-port 5013 --dapr-http-port 56002 --resources-path {daprTwinsComponentsPath} -- dotnet run --urls=http://localhost:5013/",
        //         WorkingDirectory = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "src", "Twins")), // adjust path to service project
        //         RedirectStandardOutput = true,
        //         RedirectStandardError = true,
        //         UseShellExecute = false,
        //     }
        // };
        // _TwinsProcess.Start();

        // using var http2 = new HttpClient();
        // while (true)
        // {
        //     try
        //     {
        //         var res = await http2.GetAsync("http://localhost:5013/ontologies/hola");
        //         if (res.IsSuccessStatusCode)
        //         {
        //             Console.WriteLine("✅ Dapr ready");
        //             break;
        //         }
        //     }
        //     catch {  }
        //     await Task.Delay(1000);
        // }

        // Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", "5001");

        // _ThingsProcess = StartDaprProcess(
        //     appId: "things-service",
        //     appPort: 5001,
        //     daprHttpPort: 56001,
        //     configPath: daprThingsConfigPath,
        //     resourcesPath: daprThingsComponentsPath,
        //     workingDir: Path.Combine("..", "..", "..", "..", "..", "src", "Things"),

        //     grpcPort: 50001
        // );

        // _TwinsProcess = StartDaprProcess(
        //     appId: "twins-service",
        //     appPort: 5013,
        //     daprHttpPort: 56002, // assign a different port
        //     configPath: "",
        //     resourcesPath: daprTwinsComponentsPath,
        //     workingDir: Path.Combine("..", "..", "..", "..", "..", "src", "Twins"),

        //     grpcPort: 50002
        // );

        // await WaitForDaprReadyAsync(56001); // Things
        // await WaitForDaprReadyAsync(56002); // Twins

        await WaitForServiceReadyAsync("http://localhost:5001/health");
        await WaitForServiceReadyAsync("http://localhost:5013/ontologies/hola");

        // 6️⃣ Create HTTP clients
        ThingsClient = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
        // TwinsClient = new HttpClient { BaseAddress = new Uri("http://localhost:5013") };
        //ThingsClient = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };

        _factory = new WebApplicationFactory<Twins.TestMaker>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<DGraphService>();
                    // services.AddSingleton<ThingsService>();

                    
                });
                
            });

        Client = _factory.CreateClient();

        DGraphService = _factory.Services.GetRequiredService<DGraphService>();
        // ThingsService = _factory.Services.GetRequiredService<ThingsService>();
        // This runs ONCE for all tests in the collection
        string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "existingOntologyNQuads.txt");
        var nquads = new List<string>();
        try
        {

            nquads.Add("_:ontologiaPrueba <dgraph.type> \"Ontology\" .");
            nquads.Add("_:ontologiaPrueba <ontologyId> \"ontologiaPrueba\" .");
            nquads.AddRange(File.ReadAllLines(path));

            getFirstIds(nquads);

        }
        catch (System.IO.FileNotFoundException ex)
        {
            Console.WriteLine("File not found: " + ex.Message);
            return;
        }

        try
        {
            await DGraphService.AddNQuadTripleAsync(nquads);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Something went wrong while uploading to DGraph: " + ex.Message);
        }

        //as it imported it correctly, i will instanciate a thing for the instanciate tests
        //create payload
        var payload = new JsonObject
        {
            ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
            ["id"] = instanciatedThingId,
            ["title"] = "",
            ["hasType"] = thingId,
            ["properties"] = new JsonObject { }, //provisional
            ["actions"] = new JsonObject { },
            ["events"] = new JsonObject { }
        };

        // HERE should call the Things API to instanciate the Thing, but since it's already defined because there is no DELETE method, I skip it
        // var response = await ThingsClient.PostAsJsonAsync("/things", payload);
        // Console.WriteLine(response.IsSuccessStatusCode ? "BIEN INSTANCIADO" : "MAL INSTANCIADO");
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine("ENTRA EN AFTERALL");
        //Here the instanciated Thing should be deleted from the Things Service, but there is no method in the API for that
        await DGraphService.DeleteByOntologyId("ontologiaPrueba");
        // try
        // {
        //     if (!_ThingsProcess.HasExited)
        //     {
        //         _ThingsProcess.Kill(entireProcessTree: true); // This kills Dapr and the app
        //         _ThingsProcess.WaitForExit(5000);
        //     }
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine($"Error stopping Dapr: {ex}");
        // }
        // finally
        // {
        //     _ThingsProcess.Dispose();
        // }

        //  try
        // {
        //     if (!_TwinsProcess.HasExited)
        //     {
        //         _TwinsProcess.Kill(entireProcessTree: true); // This kills Dapr and the app
        //         _TwinsProcess.WaitForExit(5000);
        //     }
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine($"Error stopping Dapr: {ex}");
        // }
        // finally
        // {
        //     _TwinsProcess.Dispose();
        // }
        _factory?.Dispose();
    }
}

[CollectionDefinition("Ontologies collection")]
public class OntologiesCollection : ICollectionFixture<OntologiesAPIFixture>
{
    //marker for xunit
}

[Collection("Ontologies collection")]
public class OntologiesTest
{
    private readonly HttpClient _client;
    private readonly OntologiesAPIFixture _fixture;
    public DGraphService _dgraphService = null!;
    public ThingsService _thingsService = null!;

    public readonly string _ontologyId;

    public readonly string _thingId;
    public readonly string _relationName;
    public readonly string _attributeKey;
    public readonly string _instaciatedThingId;
    public readonly HttpClient _thingsClient;

    public OntologiesTest(OntologiesAPIFixture fixture)
    {
        _fixture = fixture;
        _ontologyId = fixture.ontologyId;
        _client = fixture.Client; // get the HttpClient from the fixture
        _dgraphService = fixture.DGraphService;
        _thingsService = fixture.ThingsService;
        _thingId = fixture.thingId;
        _relationName = fixture.relationName;
        _attributeKey = fixture.attributeKey;
        _instaciatedThingId = fixture.instanciatedThingId;
        _thingsClient = fixture.ThingsClient;
    }

    private JsonElement GetJsonRoot(string jsonString)
    {
        using var jsonDoc = JsonDocument.Parse(jsonString);
        return jsonDoc.RootElement.Clone();
    }

    public class UploadOntologyTest : OntologiesTest
    {
        public UploadOntologyTest(OntologiesAPIFixture fixture) : base(fixture) { }

        private string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".ttl" => "text/turtle",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".csv" => "text/csv",
                ".xml" => "application/xml",
                ".html" => "text/html",
                _ => "application/octet-stream", // fallback
            };
        }

        private HttpContent GetHttpContentForPostRequest(string path)
        {
            var fileStream = File.OpenRead(path);
            var content = new MultipartFormDataContent();

            // Add the file content
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(path)); //appropriate MIME type
            string fileName = Path.GetFileName(path);
            content.Add(fileContent, "ontologyFile", fileName);

            return content;
        }

        private async void deleteImportedOntology(string ontologyId)
        {
            await _dgraphService.DeleteByOntologyId(ontologyId);
        }

        //The uploaded file is null
        [Fact]
        public async Task UploadOntology_NullFile_BadRequest()
        {
            string ontologyId = "anotherOntologyId";
            var response = await _client.PostAsync($"http://localhost:5013/ontologies/{ontologyId}", null);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        //The uploaded file is not of ttl type
        [Fact]
        public async Task UploadOntology_NotTTLExtension_BadRequest()
        {
            string ontologyId = "anotherOntologyId";
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "emptyFile.txt");
            using var content = GetHttpContentForPostRequest(path);
            var response = await _client.PostAsync($"http://localhost:5013/ontologies/{ontologyId}", content);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        //The uploaded file is correct but the id is already on use
        [Fact]
        public async Task UploadOntology_TTLExtensionButIdOnUse_Conflict()
        {
            string ontologyId = _ontologyId; //same id as the already existing ontology
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "importingOntology.ttl");
            using var content = GetHttpContentForPostRequest(path);
            var response = await _client.PostAsync($"http://localhost:5013/ontologies/{ontologyId}", content);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        //The uploaded file is correct and the id is unused
        [Fact]
        public async Task UploadOntology_TTLExtensionAndIdUnused_ImportedOntology()
        {
            string ontologyId = "anotherOntologyId"; //same id as the already existing ontology
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "importingOntology.ttl");
            using var content = GetHttpContentForPostRequest(path);
            var responsePost = await _client.PostAsync($"http://localhost:5013/ontologies/{ontologyId}", content);
            var responseGet = await _client.GetAsync($"http://localhost:5013/ontologies/{ontologyId}");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);

            Assert.True(responsePost.IsSuccessStatusCode);
            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.True(jsonRoot.GetArrayLength() > 0);

            deleteImportedOntology(ontologyId);
        }
    }

    public class GetThingsOntologyTest : OntologiesTest
    {
        public GetThingsOntologyTest(OntologiesAPIFixture fixture) : base(fixture) { }

        //The ontology exists
        [Fact]
        public async Task GetThingsOntology_OntologyExists_JSON()
        {
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}");
            var jsonString = await response.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);

            // Assert.
            Assert.True(response.IsSuccessStatusCode);
            Assert.True(jsonRoot.GetArrayLength() > 0);
        }

        //The ontology does not exist
        [Fact]
        public async Task GetThingsOntology_OntologyDoesntExist_EmptyJSON()
        {
            //Arrange
            string otherId = "inexistentOntologyId";

            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{otherId}");
            var jsonString = await response.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);

            //Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(0, jsonRoot.GetArrayLength());
        }
    }

    public class GetThingInOntologyTest : OntologiesTest
    {
        public GetThingInOntologyTest(OntologiesAPIFixture fixture) : base(fixture) { }

        //The ontology and the thing exist
        [Fact]
        public async Task GetThingInOntology_OntologyAndThingExists_JSON()
        {
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/things/{_thingId}");
            var jsonString = await response.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);

            // Assert
            Assert.True(response.IsSuccessStatusCode); //Recieves a 2xx code
            Assert.NotEqual(JsonValueKind.Undefined, jsonRoot.ValueKind); //recieves something
            Assert.NotEqual(JsonValueKind.Null, jsonRoot.ValueKind); //recieves something
            Assert.NotEqual(JsonValueKind.Array, jsonRoot.ValueKind); //recieves only one thing
        }

        //The ontology exists but the thing doesn't
        [Fact]
        public async Task GetThingInOntology_ThingDoesNotExist_404()
        {
            //Arrange
            string inexistentThingId = "INEXISTENTTHING";
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/things/{inexistentThingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The ontology doesn't exist
        [Fact]
        public async Task GetThingInOntology_OntologDoesNotExist_404()
        {
            //Arrange
            string inexistentOntologyId = "INEXISTENTONTOLOGY";
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{inexistentOntologyId}/things/{_thingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class GetRelationInOntologyTest : OntologiesTest
    {
        public GetRelationInOntologyTest(OntologiesAPIFixture fixture) : base(fixture) { }

        //The ontology and the relation exist
        [Fact]
        public async Task GetRelationInOntology_OntologyAndRelationExist_JSON()
        {
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/relations/{_relationName}");
            var jsonString = await response.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);
            // Assert
            Assert.True(response.IsSuccessStatusCode); //Recieves a 2xx code
            Assert.NotEqual(JsonValueKind.Undefined, jsonRoot.ValueKind); //recieves something
            Assert.NotEqual(JsonValueKind.Null, jsonRoot.ValueKind); //recieves something
            Assert.Equal(JsonValueKind.Array, jsonRoot.ValueKind); //recieves an array
            Assert.True(jsonRoot.GetArrayLength() >= 1); //there is at least one pair with such relation
        }

        //The ontology exists but no relation has said name
        [Fact]
        public async Task GetRelationInOntology_RelationDoesNotExist_404()
        {
            //Arrange
            string inexistentRelation = "INEXISTENTRELATIONNAME";
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/relations/{inexistentRelation}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The ontology doesn't exist
        [Fact]
        public async Task GetRelationInOntology_OntologyDoesNotExist_404()
        {
            //Arrange
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/relations/{inexistentOntologyId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class GetAttributeInOntologyTest : OntologiesTest
    {
        public GetAttributeInOntologyTest(OntologiesAPIFixture fixture) : base(fixture) { }

        //The ontology and the attribute exist
        [Fact]
        public async Task GetAttributeInOntology_OntologyAndAttributeExist_JSON()
        {
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/attributes/{_attributeKey}");
            var jsonString = await response.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);
            // Assert
            Assert.True(response.IsSuccessStatusCode); //Recieves a 2xx code
            Assert.NotEqual(JsonValueKind.Undefined, jsonRoot.ValueKind); //recieves something
            Assert.NotEqual(JsonValueKind.Null, jsonRoot.ValueKind); //recieves something
            Assert.Equal(JsonValueKind.Array, jsonRoot.ValueKind); //recieves an array
            Assert.True(jsonRoot.GetArrayLength() >= 1); //there is at least one pair with such relation
        }

        //The ontology exists but no attribute has said name
        [Fact]
        public async Task GetAttributeInOntology_AttributeDoesNotExist_JSON()
        {
            string inexistentAttribute = "INEXISTENTATTRIBUTEKEY";
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/relations/{inexistentAttribute}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The ontology doesn't exist
        [Fact]
        public async Task GetAttributeInOntology_OntologyDoesNotExist_JSON()
        {
            //Arrange
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"http://localhost:5013/ontologies/{_ontologyId}/relations/{inexistentOntologyId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class DeleteOntologyTest : OntologiesTest
    {
        public DeleteOntologyTest(OntologiesAPIFixture fixture) : base(fixture) { }

        //The ontology exists
        [Fact]
        public async Task DeleteOntology_OntologyExists_200AndNoOntology()
        {
            //Import another ontology so it doesn't affect the other methods
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "importingOntologyNQuads.txt");
            string ontologyId = "anotherTestOntology";
            var nquads = new List<string>();
            try
            {

                nquads.Add($"_:{ontologyId} <dgraph.type> \"Ontology\" .");
                nquads.Add($"_:{ontologyId} <ontologyId> \"{ontologyId}\" .");
                nquads.AddRange(File.ReadAllLines(path));
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Console.WriteLine("File not found: " + ex.Message);
                return;
            }

            try
            {
                var response = await _dgraphService.AddNQuadTripleAsync(nquads);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong while uploading to DGraph: " + ex.Message);
            }

            //Act
            var responseDelete = await _client.DeleteAsync($"http://localhost:5013/ontologies/{ontologyId}");
            await Task.Delay(500);
            var responseGet = await _client.GetAsync($"http://localhost:5013/ontologies/{ontologyId}");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);

            //Assert
            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.Equal(0, jsonRoot.GetArrayLength());
        }

        //The ontology doesn't exist
        [Fact]
        public async Task DeleteOntology_OntologyDoesNotExist_404()
        {
            //Arrange
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.DeleteAsync($"http://localhost:5013/ontologies/{inexistentOntologyId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class InstanciateThingFromOntologyTest : OntologiesTest
    {
        public InstanciateThingFromOntologyTest(OntologiesAPIFixture fixture) : base(fixture) { }

        //The ontology and the thing exists and the id is unused
        [Fact]
        public async Task InstanciateThingFromOntology_IdNotOnUse_200AndThing()
        {
            //Arrange
            string uninstanciatedId = "newInstanciatedId"+Guid.NewGuid();
            //Act
            var responsePost = await _client.PostAsync($"http://localhost:5013/ontologies/{_ontologyId}/things/{_thingId}/instanciate/{uninstanciatedId}", null);
            await Task.Delay(500);
            var responseGet = await _thingsClient.GetAsync($"/things/{uninstanciatedId}");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);
            //Assert
            Assert.True(responsePost.IsSuccessStatusCode);
            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.Equal(JsonValueKind.Object, jsonRoot.ValueKind);

            //TODO delete thing from Things*****
        }

        //The ontology and the thing exists but the id is already on use
        [Fact]
        public async Task InstanciateThingFromOntology_IdOnUse_Conflict()
        {
            //Arrange
            string instanciatedId = _instaciatedThingId;
            //Act
            var response = await _client.PostAsync($"http://localhost:5013/ontologies/{_ontologyId}/things/{_thingId}/instanciate/{instanciatedId}", null);
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        //The ontology exists but the thing doesn't
        [Fact]
        public async Task InstanciateThingFromOntology_ThingDoesNotExist_404()
        {
            //Arrange
            string inexistentThingId = "INEXISTENTTHINGID";
            string instanciateId = "instanciateId";
            //Act
            var response = await _client.PostAsync($"http://localhost:5013/ontologies/{_ontologyId}/things/{inexistentThingId}/instanciate/{instanciateId}", null);
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The ontology doesn't exist
        [Fact]
        public async Task InstanciateThingFromOntology_OntologyDoesNotExist_404()
        {
            //Arrange
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            string instanciateId = "instanciateId";
            //Act
            var response = await _client.PostAsync($"http://localhost:5013/ontologies/{inexistentOntologyId}/things/{_thingId}/instanciate/{instanciateId}", null);
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

}
