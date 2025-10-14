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

public class OntologiesAPIFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; } = null!;
    private WebApplicationFactory<Twins.TestMaker> _factory = null!;
    // public IServiceProvider Services => _factory.Services;
    public DGraphService DGraphService { get; private set; } = null!;
    public Func<IServiceScope> CreateScope { get; private set; } = null!;

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

    public async Task InitializeAsync()
    {
       _factory = new WebApplicationFactory<Twins.TestMaker>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<DGraphService>();
                });
            });

        Client = _factory.CreateClient();

        // using var scope = _factory.Services.CreateScope();
        DGraphService = _factory.Services.GetRequiredService<DGraphService>();
        

        // await _dgraphService.InitializeAsync();

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
            var response = await DGraphService.AddNQuadTripleAsync(nquads);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Something went wrong while uploading to DGraph: " + ex.Message);
        }
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine("ENTRA EN AFTERALL");
        await DGraphService.DeleteByOntologyId("ontologiaPrueba");
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

    public readonly string _ontologyId;

    public readonly string _thingId;
    public readonly string _relationName;
    public readonly string _attributeKey;
    public readonly string _instaciatedThingId;

    public OntologiesTest(OntologiesAPIFixture fixture)
    {
        _fixture = fixture;
        _ontologyId = fixture.ontologyId;
        _client = fixture.Client; // get the HttpClient from the fixture
        _dgraphService = fixture.DGraphService;
        _thingId = fixture.thingId;
        _relationName = fixture.relationName;
        _attributeKey = fixture.attributeKey;
        _instaciatedThingId = fixture.instanciatedThingId;
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
            content.Add(fileContent, "ontologyFile", fileName); // "file" = form field name

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
            //Arrange
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

        //The ontology and the thing exists but the id is already on use
        // [Fact]
        // public async Task InstanciateThingFromOntology_IdOnUse_Conflict()
        // {
        //     //Arrange
        //     string instanciatedId = _instaciatedThingId;
        //     //Act
        //     var response = await _client.PostAsync($"http://localhost:5013/ontologies/{_ontologyId}/things/{_thingId}/instanciate/{instanciatedId}", null);
        //     //Assert
        //     Assert.False(response.IsSuccessStatusCode);
        //     Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        // }

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
