namespace TwinsTest;

using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Google.Api;
using Json.More;
using Xunit;

[Collection("Twins Tests")]
public abstract class ShapesTest : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly TwinsAPIFixture _fixture;
    private readonly string _shapeId;
    private readonly string _importedShapeId;
    private string _nodeShapeId;
    private string _twinId;
    private readonly int _initCount;
    
    public ShapesTest(TwinsAPIFixture fixture)
    {
        _client = fixture.ShapesClient;
        _fixture = fixture;
        _shapeId = fixture.shapeId;
        _importedShapeId = fixture.shapeIdImported;
        _initCount = _fixture.shapeInitCount;
        _nodeShapeId = fixture.nodeShapeId;
        _twinId = fixture.twinId;
    }

    public async Task DisposeAsync()
    {
        await CleanupAfterTestAsync();
    }

    protected virtual Task CleanupAfterTestAsync() => Task.CompletedTask;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public class GetAllShapesTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //It is successful
        [Fact]
        public async Task GetAllShapes_Nothing_200AndPagedResult()
        {
            var response = await _client.GetAsync("");
            var jsonString = await response.Content.ReadAsStringAsync();
            var count = TwinsAPIFixture.GetActualCount(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(_initCount, count);
        }
    }

    public class UploadShapeGraphTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Content is null
        [Fact]
        public async Task UploadShapeGraph_NullFile_BadRequest()
        {
            string shapeId = _importedShapeId;
            var response = await _client.PostAsync(shapeId, null);
            var count = await _fixture.GetShapeCount();

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(_initCount, count);
        }

        //The file is not of TTL extension
        [Fact]
        public async Task UploadShapeGraph_NotTTLExtension_BadRequest()
        {
            string shapeId = _importedShapeId;
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "emptyFile.txt");
            var response = await _client.PostAsync(shapeId, TwinsAPIFixture.GetHttpContentForPostRequest(path, "shapeFile"));
            var count = await _fixture.GetShapeCount();

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(_initCount, count);
        }

        //The file is of TTL extension but the id is on use
        [Fact]
        public async Task UploadShapeGraph_IdOnUse_Conflict()
        {
            string shapeId = _shapeId;
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "importingShapeGraph.ttl");
            var response = await _client.PostAsync(shapeId, TwinsAPIFixture.GetHttpContentForPostRequest(path, "shapeFile"));
            var count = await _fixture.GetShapeCount();

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(_initCount, count);
        }

        //The file is of TTL extension and the id is unused
        [Fact]
        public async Task UploadShapeGraph_IdNotOnUse_ImportedShapeGraph()
        {
            string shapeId = _importedShapeId;
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "importingShapeGraph.ttl");
            var response = await _client.PostAsync(shapeId, TwinsAPIFixture.GetHttpContentForPostRequest(path, "shapeFile"));
            var count = await _fixture.GetShapeCount();

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(_initCount+1, count);
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsShapeGraph(_importedShapeId))
                await _fixture.DeleteShapeGraph(_importedShapeId);
        }
    }

    public class GetShapeGraphByIdTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Shape Graph does not exist
        [Fact]
        public async Task GetShapeGraphById_ShapeGraphDoesNotExist_NotFound()
        {
            var shapeId = "INEXISTENTSHAPEID";
            var response = await _client.GetAsync(shapeId);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Shape Graph exists
        [Fact]
        public async Task GetShapeGraphById_ShapeGraphExists_JSON()
        {
            var shapeId = _shapeId;
            var response = await _client.GetAsync(shapeId);
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(root.EnumerateArray());
            Assert.Single(root.EnumerateArray());
        }
    }

    public class DeleteShapeGraphByIdTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Shape Graph does not exist
        [Fact]
        public async Task DeleteShapeGraphById_ShapeGraphDoesNotExist_NotFound()
        {
            var shapeId = "INEXISTENTSHAPEID";
            var response = await _client.DeleteAsync(shapeId);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Shape Graph exists
        [Fact]
        public async Task DeleteShapeGraphById_ShapeGrapHExists_ShapeGraphDeleted()
        {
            var shapeId = _importedShapeId;
            await _fixture.ImportShampleShapeGraph();
            var initCount = await _fixture.GetShapeCount();
            
            var response = await _client.DeleteAsync(shapeId);
            var count = await _fixture.GetShapeCount();

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(initCount-1, count);
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsShapeGraph(_importedShapeId))
                await _fixture.DeleteShapeGraph(_importedShapeId);
        }
    }

    public class GetNodeShapeInShapeGraphTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Shape Graph does not exist
        [Fact]
        public async Task GetNodeShapeInShapeGraph_ShapeGraphDoesNotExist_NotFound()
        {
            var shapeId = "INEXISTENTSHAPEID";
            var nodeShapeId = "INEXISTENTNODESHAPEID";
            var response = await _client.GetAsync($"{shapeId}/shapes/{nodeShapeId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Node Shape does not exist
        [Fact]
        public async Task GetNodeShapeInShapeGraph_NodeShapeDoesNotExist_NotFound()
        {
            var shapeId = _shapeId;
            var nodeShapeId = "INEXISTENTNODESHAPEID";
            var response = await _client.GetAsync($"{shapeId}/shapes/{nodeShapeId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Both the Shape Graph and the Node Shape exist
        [Fact]
        public async Task GetNodeShapeInShapeGraph_NodeShapeExists_JSON()
        {
            var shapeId = _shapeId;
            var nodeShapeId = _nodeShapeId;
            var response = await _client.GetAsync($"{shapeId}/shapes/{nodeShapeId}");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.NotEmpty(root.EnumerateObject());
        }

    }

    public class ExportShapeGraphInJsonTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Shape Graph does not exist
        [Fact]
        public async Task ExportShapeGraphInJson_ShapeGraphDoesNotExist_NotFound()
        {
            var shapeId = "INEXISTENTSHAPEID";
            var response = await _client.GetAsync($"{shapeId}/Json");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Shape Graph exists
        [Fact]
        public async Task ExportShapeGraphInJson_ShapeGraphExists_JSON()
        {
            var shapeId = _shapeId;
            var response = await _client.GetAsync($"{shapeId}/export/Json");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.NotEmpty(root.EnumerateObject());
        }
    }

    public class ExportShapeGraphInJsonLdTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Shape Graph does not exist
        [Fact]
        public async Task ExportShapeGraphInJsonLd_ShapeGraphDoesNotExist_NotFound()
        {
            var shapeId = "INEXISTENTSHAPEID";
            var response = await _client.GetAsync($"{shapeId}/export/JsonLd");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Shape Graph exists
        [Fact]
        public async Task ExportShapeGraphInJsonLd_ShapeGraphExists_JsonLD()
        {
            var shapeId = _shapeId;
            var response = await _client.GetAsync($"{shapeId}/export/JsonLd");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.NotEmpty(root.EnumerateObject());
            Assert.True(root.TryGetProperty("@context", out _));
            Assert.True(root.TryGetProperty("@graph", out var arr));
            Assert.True(arr.AsNode() is JsonArray);
        }
    }

    public class ExportShapeGraphInTTLFileTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Shape Graph does not exist
        [Fact]
        public async Task ExportShapeGraphInTTLFile_ShapeGraphDoesNotExist_NotFound()
        {
            var shapeId = "INEXISTENTSHAPEID";
            var response = await _client.GetAsync($"{shapeId}/export/TTL");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Shape Graph exists
        [Fact]
        public async Task ExportShapeGraphInTTLFile_ShapeGraphExists_TTLFile()
        {
            var shapeId = _shapeId;
            var response = await _client.GetAsync($"{shapeId}/export/TTL");
            var byteArray = await response.Content.ReadAsByteArrayAsync();
            
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(byteArray);
            Assert.NotEmpty(byteArray);
        }
    }

    public class ValidateTwinWithShapeGraphTest(TwinsAPIFixture fixture) : ShapesTest(fixture)
    {
        //The Shape Graph does not exist
        [Fact]
        public async Task ValidateTwinWithShapeGraph_ShapeGraphDoesNotExist_NotFound()
        {
            var shapeId = "INEXISTENTSHAPEID";
            var twinId = "INEXISTENTTWINID";
            var response = await _client.GetAsync($"{shapeId}/validate/{twinId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Twin does not exist
        [Fact]
        public async Task ValidateTwinWithShapeGraph_TwinDoesNotExist_NotFound()
        {
            var shapeId = _shapeId;
            var twinId = "INEXISTENTTWINID";

            var response = await _client.GetAsync($"{shapeId}/validate/{twinId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Both the Shape Graph and the Twin exist
        [Fact]
        public async Task ValidateTwinWithShapeGraph_BothExists_ValdiationReport()
        {
            var shapeId = _shapeId;
            var twinId = _twinId;

            var response = await _client.GetAsync($"{shapeId}/validate/{twinId}");
            var jsonString = await response.Content.ReadAsStringAsync();
            
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
        }
    }
}
