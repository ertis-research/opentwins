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
using Json.More;
using Api;
using System.Text;

[CollectionDefinition("Ontologies collection")]
public class OntologiesCollection : ICollectionFixture<TwinsAPIFixture>
{
    //marker for xunit
}

[Collection("Ontologies collection")]
public class OntologiesTest
{
    private readonly HttpClient _client;
    private readonly TwinsAPIFixture _fixture;
    public DGraphService _dgraphService = null!;
    public ThingsService _thingsService = null!;

    public readonly string _ontologyId;
    public readonly string _importedOntologyId;
    public readonly string _shapeId;
    public readonly string _importedShapeId;
    public readonly int _initCount;
    public readonly string _thingId;
    public readonly string? _childId;
    public readonly string? _parentId;
    public readonly string? _orphanId;
    public readonly string? _childlessId;
    public readonly string _relationName;
    public readonly string _attributeKey;
    public readonly string _instaciatedThingId;
    public readonly HttpClient _thingsClient;

    public OntologiesTest(TwinsAPIFixture fixture)
    {
        _fixture = fixture;
        _ontologyId = fixture.ontologyId;
        _importedOntologyId = fixture.ontologyIdImported;
        _shapeId = _fixture.shapeId;
        _importedShapeId = fixture.shapeIdImported;
        _client = fixture.OntologiesClient;
        _dgraphService = fixture.DGraphService;
        _thingsService = fixture.ThingsService;
        _thingId = fixture.thingId;
        _childId = fixture.child;
        _parentId = fixture.parent;
        _orphanId = fixture.orphan;
        _childlessId = fixture.childless;
        _initCount = fixture.initCount;
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

    private async Task<int> GetActualCount()
    {
        var response = await _client.GetAsync("");
        var jsonStr = await response.Content.ReadAsStringAsync();
        return GetActualCount(jsonStr);
    }

    private int GetActualCount(string jsonStr)
    {
        using var jsonDoc = JsonDocument.Parse(jsonStr);
        int count = jsonDoc.RootElement.TryGetProperty("totalCount", out var countEl) ? countEl.GetInt32() : 0;
        return count;
    }

    public class GetAllOntologiesTest : OntologiesTest
    {
        public GetAllOntologiesTest(TwinsAPIFixture fixture) : base(fixture) { }

        //It is successful
        [Fact]
        public async Task GetOntologies_Nothing_200AndPagedResult()
        {
            var responseGet = await _client.GetAsync("");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var count = GetActualCount(jsonString);

            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.Equal(_initCount, count);
        }
    }

    public class UploadOntologyTest : OntologiesTest
    {
        public UploadOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The uploaded file is null
        [Fact]
        public async Task UploadOntology_NullFile_BadRequest()
        {
            string ontologyId = "anotherontologyid";
            var initCount = await GetActualCount();
            var response = await _client.PostAsync($"{ontologyId}", null);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True(initCount >= await GetActualCount());
        }

        //The uploaded file is not of ttl type
        [Fact]
        public async Task UploadOntology_NotTTLExtension_BadRequest()
        {
            string ontologyId = "anotherontologyid";
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "emptyFile.txt");
            using var content = GetHttpContentForPostRequest(path);
            var responsePost = await _client.PostAsync($"{ontologyId}", content);
            var responseGet = await _client.GetAsync($"{ontologyId}");

            Assert.False(responsePost.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, responsePost.StatusCode);
            Assert.Equal(_initCount, await GetActualCount());
            Assert.False(responseGet.IsSuccessStatusCode);
        }

        //The uploaded file is correct but the id is already on use
        [Fact]
        public async Task UploadOntology_TTLExtensionButIdOnUse_Conflict()
        {
            string ontologyId = _ontologyId; //same id as the already existing ontology
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "importingOntology.ttl");
            using var content = GetHttpContentForPostRequest(path);
            var response = await _client.PostAsync($"{ontologyId}", content);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(_initCount, await GetActualCount());
        }

        //The uploaded file is correct and the id is unused
        [Fact]
        public async Task UploadOntology_TTLExtensionAndIdUnused_ImportedOntology()
        {
            string ontologyId = "anotherontologyid"; //same id as the already existing ontology
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", "importingOntology.ttl");
            using var content = GetHttpContentForPostRequest(path);
            var responsePost = await _client.PostAsync($"{ontologyId}", content);
            var responseGet = await _client.GetAsync($"{ontologyId}");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var jsonRoot = GetJsonRoot(jsonString);

            Assert.True(responsePost.IsSuccessStatusCode);
            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.Equal(_initCount+1, await GetActualCount());

            await _fixture.DeleteOntology(ontologyId);
        }
    }

    public class GetThingsOntologyTest : OntologiesTest
    {
        public GetThingsOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The ontology exists
        [Fact]
        public async Task GetThingsOntology_OntologyExists_JSON()
        {
            //Act
            var response = await _client.GetAsync($"{_ontologyId}");
            var jsonString = await response.Content.ReadAsStringAsync();

            Assert.True(response.IsSuccessStatusCode);
        }

        //The ontology does not exist
        [Fact]
        public async Task GetThingsOntology_OntologyDoesntExist_404NotFound()
        {
            //Arrange
            string otherId = "inexistentOntologyId";

            //Act
            var response = await _client.GetAsync($"{otherId}");

            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class GetThingInOntologyTest : OntologiesTest
    {
        public GetThingInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The ontology and the thing exist
        [Fact]
        public async Task GetThingInOntology_OntologyAndThingExists_JSON()
        {
            //Act
            var response = await _client.GetAsync($"{_ontologyId}/things/{_thingId}");
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
            var response = await _client.GetAsync($"{_ontologyId}/things/{inexistentThingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The ontology doesn't exist
        [Fact]
        public async Task GetThingInOntology_OntologDoesNotExist_404()
        {
            //Arrange
            string inexistentOntologyId = "INEXISTENTONTOLOGY";
            var response = await _client.GetAsync($"{inexistentOntologyId}/things/{_thingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class GetRelationInOntologyTest : OntologiesTest
    {
        public GetRelationInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The ontology and the relation exist
        [Fact]
        public async Task GetRelationInOntology_OntologyAndRelationExist_JSON()
        {
            //Act
            var response = await _client.GetAsync($"{_ontologyId}/relations/{_relationName}");
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
            var response = await _client.GetAsync($"{_ontologyId}/relations/{inexistentRelation}");
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
            var response = await _client.GetAsync($"{_ontologyId}/relations/{inexistentOntologyId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class GetAttributeInOntologyTest : OntologiesTest
    {
        public GetAttributeInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The ontology and the attribute exist
        [Fact]
        public async Task GetAttributeInOntology_OntologyAndAttributeExist_JSON()
        {
            //Act
            var response = await _client.GetAsync($"{_ontologyId}/attributes/{_attributeKey}");
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
            var response = await _client.GetAsync($"{_ontologyId}/relations/{inexistentAttribute}");
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
            var response = await _client.GetAsync($"{_ontologyId}/relations/{inexistentOntologyId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class DeleteOntologyTest : OntologiesTest
    {
        public DeleteOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The ontology exists
        [Fact]
        public async Task DeleteOntology_OntologyExists_200AndNoOntology()
        {
            //Import another ontology so it doesn't affect the other methods

            string ontologyId = _importedOntologyId;
            await _fixture.ImportSampleOntology();
            var initCount = await GetActualCount();

            //Act
            var responseDelete = await _client.DeleteAsync($"{ontologyId}");
            await Task.Delay(500);
            var responseGet = await _client.GetAsync($"{ontologyId}");

            //Assert
            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.False(responseGet.IsSuccessStatusCode);
            Assert.Equal(initCount-1, await GetActualCount());

            await _fixture.DeleteOntology(_importedOntologyId);
        }

        //The ontology doesn't exist
        [Fact]
        public async Task DeleteOntology_OntologyDoesNotExist_404()
        {
            //Arrange
            var initCount = await _fixture.GetOntologyCount();
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.DeleteAsync($"{inexistentOntologyId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(initCount, await _fixture.GetOntologyCount());
        }
    }

    public class DeleteThingFromOntologyTest : OntologiesTest
    {
        public DeleteThingFromOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The ontology and the Thing exists
        [Fact]
        public async Task DeleteThingFromOntology_OntologyAndThingExist_200AndNoThing()
        {
            string ontologyId = _importedOntologyId;
            await _fixture.ImportSampleOntology();
            string thingId = _fixture.thingIdOfImported ?? "";
            Console.WriteLine($"THINGID :{thingId}");
            //Act
            var responseDelete = await _client.DeleteAsync($"{ontologyId}/things/{thingId}");
            var responseGet = await _client.GetAsync($"{ontologyId}/things/{thingId}");
            //Assert
            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.False(responseGet.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, responseGet.StatusCode);

            await _fixture.DeleteOntology(ontologyId);
        }

        //The Ontology exists but the Thing does not
        [Fact]
        public async Task DeleteThingFromOntology_ThingDoesNotExist_404()
        {
            string inexistentThingId = "INEXISTENTTHINGID";
            //Act
            var response = await _client.DeleteAsync($"{_ontologyId}/things/{inexistentThingId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology does not exist
        [Fact]
        public async Task DeleteThingFromOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.DeleteAsync($"{inexistentOntologyId}/things/{_thingId}");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class InstanciateGraphFromOntologyTest : OntologiesTest
    {
        public InstanciateGraphFromOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology doesn't exist
        [Fact]
        public async Task InstanciateGraphFromOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.PutAsync($"{inexistentOntologyId}/instanciate", new StringContent((new JsonObject()).ToJsonString(), Encoding.UTF8, "application/json"));
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists but the graph is empty
        [Fact]
        public async Task InstanciateGraphFromOntology_EmptyGraph_400()
        {
            var response = await _client.PutAsync($"{_ontologyId}/instanciate", new StringContent((new JsonObject()).ToJsonString(), Encoding.UTF8, "application/json"));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        //The Ontology exists but the graph has ids that conflict with existing ones in Things
        [Fact]
        public async Task InstanciateGraphFromOntology_ConflictingIds_409()
        {
            var response = await _client.PutAsync($"{_ontologyId}/instanciate", new StringContent(new JsonObject
                {
                    ["@graph"] = new JsonArray
                    {
                        new JsonObject{["@id"]= "duplicatedId"},
                        new JsonObject{["@id"]= "duplicatedId"}
                    }
                }.ToJsonString(), Encoding.UTF8, "application/json"));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        //TODO: The Ontology exists and the graph is valid
        // [Fact]
        // public async Task InstanciateGraphFromOntology_ValidGraph_200AndInstanciatedThings()
        // {
        //     var graph = await _fixture.GetOntologyGraph("existingOntologyGraph.json");
        //     var thingIds = graph.GetProperty("@graph").EnumerateArray().Select(thing => thing.GetProperty("id").GetString() ?? "");
            
        //     var response = await _client.PutAsync($"{_ontologyId}/instanciate", new StringContent(graph.ToJsonString(), Encoding.UTF8, "application/json"));
        //     bool allInstanciated = true;

        //     await Task.Delay(30000);
        //     foreach(var id in thingIds){
        //         var responseGet = await _thingsClient.GetAsync($"{id}");
        //         allInstanciated = allInstanciated && responseGet.IsSuccessStatusCode;
        //         Console.WriteLine($"{id} --> {responseGet.StatusCode}");
        //     }

        //     Assert.True(response.IsSuccessStatusCode);
        //     Assert.True(allInstanciated);

        //     foreach(var id in thingIds)
        //         await _thingsClient.DeleteAsync(id);
        // }
    }

    public class GetChildrenOfThingInOntologyTest : OntologiesTest
    {
        public GetChildrenOfThingInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology does not exist
        [Fact]
        public async Task GetChildrenOfThingInOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"{inexistentOntologyId}/things/{_thingId}/children");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists but the Thing doesn't
        [Fact]
        public async Task GetChildrenOfThingInOntology_ThingDoesNotExist_404()
        {
            string inexistentThingId = "INEXISTENTTHING";
            //Act
            var response = await _client.GetAsync($"{_ontologyId}/things/{inexistentThingId}/children");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Both exists and the Thing doesn't have any children
        [Fact]
        public async Task GetChildrenOfThingInOntology_ThingHasNoChildren_204()
        {
            var response = await _client.GetAsync($"{_ontologyId}/things/{_childlessId}/children");
            // Console.WriteLine($"Content recieved: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        //Both exists and the Thing has children
        [Fact]
        public async Task GetChildrenOfThingInOntology_ThingHasChildren_ArrayWithIds()
        {
            var response = await _client.GetAsync($"{_ontologyId}/things/{_parentId}/children");
            var jsonString = await response.Content.ReadAsStringAsync();
            var jsonDoc = GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.True(jsonDoc.EnumerateArray().Any());
        }
    }

    public class GetParentOfThingInOntologyTest : OntologiesTest
    {
        public GetParentOfThingInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology does not exist
        [Fact]
        public async Task GetParentOfThingInOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"{inexistentOntologyId}/things/{_thingId}/parent");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists but the Thing doesn't
        [Fact]
        public async Task GetParentOfThingInOntology_ThingDoesNotExist_404()
        {
            string inexistentThingId = "INEXISTENTTHING";
            //Act
            var response = await _client.GetAsync($"{_ontologyId}/things/{inexistentThingId}/parent");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Both exists and the Thing doesn't have a parent
        [Fact]
        public async Task GetParentOfThingInOntology_ThingHasNoParent_204()
        {
            var response = await _client.GetAsync($"{_ontologyId}/things/{_orphanId}/parent");

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        //Both exists and the Thing have a parent
        [Fact]
        public async Task GetParentOfThingInOntology_ThingHasParent_ParentJson()
        {
            var response = await _client.GetAsync($"{_ontologyId}/things/{_childId}/parent");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.True(root.EnumerateObject().Any());
        }
    }

    public class GetDependenciesOfThingInOntologyTest : OntologiesTest
    {
        public GetDependenciesOfThingInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology des not exist
        [Fact]
        public async Task GetDependenciesOfThingInOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"{inexistentOntologyId}/things/{_thingId}/relations/dependencies");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists but the Thing doesn't
        [Fact]
        public async Task GetDependenciesOfThingInOntology_ThingDoesNotExist_404()
        {
            string inexistentThingId = "INEXISTENTTHING";
            //Act
            var response = await _client.GetAsync($"{_ontologyId}/things/{inexistentThingId}/relations/dependencies");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Both exists and the Thing exist
        [Fact]
        public async Task GetDependenciesOfThingInOntology_BothExists_ThingDependency()
        {
            var response = await _client.GetAsync($"{_ontologyId}/things/{_thingId}/relations/dependencies");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.True(root.TryGetProperty("dependencies", out var dependencies));
            Assert.True(dependencies.AsNode() is JsonArray);
        }
    }

    public class GetDefaultShapeGraphsOfOntologyTest : OntologiesTest
    {
        public GetDefaultShapeGraphsOfOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology does not exist
        [Fact]
        public async Task GetDefaultShapeGraphsOfOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"{inexistentOntologyId}/defaultShapeGraphs");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology has default Shape Graphs and they exist
        [Fact]
        public async Task GetDefaultShapeGraphsOfOntology_OntologyHasShapeGraphs_ArrayWithExistentShapeGraphs()
        {
            var response = await _client.GetAsync($"{_ontologyId}/defaultShapeGraphs");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);
            var idArr = root.EnumerateArray();

            Assert.True(response.IsSuccessStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.NotEmpty(idArr);
            foreach(var id in idArr.Select(el => el.GetString()))
                Assert.True(await _fixture.ExistsShapeGraph(id ?? ""));
        } 
    }

    public class DeleteDefaultShapeGraphInOntologyTest : OntologiesTest
    {
        public DeleteDefaultShapeGraphInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }
    
        //The Ontology does not exist
        [Fact]
        public async Task DeleteDefaultShapeGraphInOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.DeleteAsync($"{inexistentOntologyId}/defaultShapeGraphs/something");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists but the Shape Graph doesn't
        [Fact]
        public async Task DeleteDefaultShapeGraphInOntology_ShapeGraphDoesNotExist_404()
        {
            string inexistentShapeId = "INEXISTENTSHAPEID";

            var response = await _client.DeleteAsync($"{_ontologyId}/defaultShapeGraphs/{inexistentShapeId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists but the Shape Graph is not in its defaults
        [Fact]
        public async Task DeleteDefaultShapeGraphInOntology_ShapeGraphIsNotDefault_400()
        {
            string ontologyId = _importedOntologyId;
            await _fixture.ImportSampleOntology();

            var response = await _client.DeleteAsync($"{ontologyId}/defaultShapeGraphs/{_shapeId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            await _fixture.DeleteOntology(ontologyId);
        }

        //The Ontology exists and has in its defaults the Shape Graph
        [Fact]
        public async Task DeleteDefaultShapeGraphInOntology_ShapeGraphIsDefault_204AndShapeNotDefault()
        {
            var responseDelete = await _client.DeleteAsync($"{_ontologyId}/defaultShapeGraphs/{_shapeId}");
            var responseGet = await _client.GetAsync($"{_ontologyId}/defaultShapeGraphs");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);
            var idArr = root.EnumerateArray().Select(id => id.GetString() ?? "");


            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.DoesNotContain(_shapeId, idArr);

            await _fixture.LinkOntologyToShapeGraph(_ontologyId, _shapeId);
        }
    }

    public class AddDefaultShapeGraphInOntologyTest : OntologiesTest
    {
        public AddDefaultShapeGraphInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }
    
        //The Ontology does not exist
        [Fact]
        public async Task AddDefaultShapeGraphsInOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.PutAsync($"{inexistentOntologyId}/defaultShapeGraphs/something", null);
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists buit the Shape Graph doesn't
        [Fact]
        public async Task AddDefaultShapeGraphsInOntology_ShapeGraphDoesNotExist_404()
        {
            string inexistentShapeId = "INEXISTENTSHAPEID";

            var response = await _client.PutAsync($"{_ontologyId}/defaultShapeGraphs/{inexistentShapeId}", null);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Both exists but the Shape Graph is already a default
        [Fact]
        public async Task AddDefaultShapeGraphsInOntology_ShapeGraphAlreadyDefault_409()
        {
            var response = await _client.PutAsync($"{_ontologyId}/defaultShapeGraphs/{_shapeId}", null);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        //Both exists and the Shape Graph is not in its defaults
        [Fact]
        public async Task AddDefaultShapeGraphsInOntology_ShapeGraphIsNotDefault_200AndIdInDefaults()
        {
            var shapeId = _importedShapeId;
            await _fixture.ImportShampleShapeGraph();

            var responsePut = await _client.PutAsync($"{_ontologyId}/defaultShapeGraphs/{shapeId}", null);
            var responseGet = await _client.GetAsync($"{_ontologyId}/defaultShapeGraphs");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);
            var arrEl = root.EnumerateArray().Select(id => id.GetString() ?? "");

            Assert.True(responsePut.IsSuccessStatusCode);
            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.Contains(shapeId, arrEl);

            await _fixture.DeleteShapeGraph(shapeId);
        }
    }

    public class DeleteAllDefaultShapeGraphsInOntologyTest : OntologiesTest
    {
        public DeleteAllDefaultShapeGraphsInOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }
    
        //The Ontology does not exist
        [Fact]
        public async Task DeleteAllDefaultShapeGraphsInOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.DeleteAsync($"{inexistentOntologyId}/defaultShapeGraphs");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology has no defaults
        [Fact]
        public async Task DeleteAllDefaultShapeGraphsInOntology_OntologyHasNoDefaults_SameArray()
        {
            var ontologyId = _importedOntologyId;
            await _fixture.ImportSampleOntology();

            var responseGet1 = await _client.GetAsync($"{ontologyId}/defaultShapeGraphs");
            var jsonStringGet1 = await responseGet1.Content.ReadAsStringAsync();
            var rootGet1 = GetJsonRoot(jsonStringGet1);
            var arrInit = rootGet1.EnumerateArray();
            
            var responseDelete = await _client.DeleteAsync($"{ontologyId}/defaultShapeGraphs/");

            var responseGet2 = await _client.GetAsync($"{ontologyId}/defaultShapeGraphs");
            var jsonStringGet2 = await responseGet2.Content.ReadAsStringAsync();
            var rootGet2 = GetJsonRoot(jsonStringGet2);
            var arr = rootGet2.EnumerateArray();

            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.Empty(arr);
            Assert.Equal(arrInit.Count(), arr.Count());

            await _fixture.DeleteOntology(ontologyId);
        }

        //The Ontology has defaults
        [Fact]
        public async Task DeleteAllDefaultShapeGraphsInOntology_OntologyHasDefaults_EmptyArray()
        {
            var shapeId = _importedShapeId;
            await _fixture.ImportSampleOntology();
            await _fixture.LinkOntologyToShapeGraph(_ontologyId, shapeId);

            var responseGet1 = await _client.GetAsync($"{_ontologyId}/defaultShapeGraphs");
            var jsonStringGet1 = await responseGet1.Content.ReadAsStringAsync();
            var rootGet1 = GetJsonRoot(jsonStringGet1);
            var arrInit = rootGet1.EnumerateArray();
            
            var responseDelete = await _client.DeleteAsync($"{_ontologyId}/defaultShapeGraphs/");
            
            var responseGet2 = await _client.GetAsync($"{_ontologyId}/defaultShapeGraphs");
            var jsonStringGet2 = await responseGet2.Content.ReadAsStringAsync();
            var rootGet2 = GetJsonRoot(jsonStringGet2);
            var arr = rootGet2.EnumerateArray();

            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.Empty(arr);
            Assert.True(arrInit.Count() > arr.Count());

            await _fixture.DeleteShapeGraph(shapeId);
            await _fixture.LinkOntologyToShapeGraph(_ontologyId, _shapeId);
        }

    }

    public class ExportOntologyInJsonTest : OntologiesTest
    {
        public ExportOntologyInJsonTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology does not exist
        [Fact]
        public async Task ExportOntologyInJson_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"{inexistentOntologyId}/export/Json");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists
        [Fact]
        public async Task ExportOntologyInJson_OntologyExists_Json()
        {
            var response = await _client.GetAsync($"{_ontologyId}/export/Json");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.True(root.EnumerateObject().Any());
        }
    }

    public class ExportOntologyInJsonLDTest : OntologiesTest
    {
        public ExportOntologyInJsonLDTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology does not exist
        [Fact]
        public async Task ExportOntologyInJsonLD_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"{inexistentOntologyId}/export/JsonLd");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists
        [Fact]
        public async Task ExportOntologyInJsonLD_OntologyExists_JsonLD()
        {
            var response = await _client.GetAsync($"{_ontologyId}/export/JsonLd");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.True(root.TryGetProperty("@context", out _));
            Assert.True(root.TryGetProperty("@graph", out var graphEl));
            Assert.True(graphEl.AsNode() is JsonArray);
        }
    }

    public class ExportOntologyInTTLTest : OntologiesTest
    {
        public ExportOntologyInTTLTest(TwinsAPIFixture fixture) : base(fixture) { }

        //The Ontology does not exist
        [Fact]
        public async Task ExportOntologyInTTL_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            //Act
            var response = await _client.GetAsync($"{inexistentOntologyId}/export/TTL");
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Ontology exists
        [Fact]
        public async Task ExportOntologyInTTL_OntologyExists_TTLFile()
        {
            var response = await _client.GetAsync($"{_ontologyId}/export/TTL");
            var byteArray = await response.Content.ReadAsByteArrayAsync();

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(byteArray is not null);
            Assert.True(byteArray.Length != 0);
        }
    }

    public class RunSparQLQueryOnOntologyTest : OntologiesTest
    {
        public RunSparQLQueryOnOntologyTest(TwinsAPIFixture fixture) : base(fixture) { }
    
        //The Ontology does not exist
        [Fact]
        public async Task RunSparQLQueryOnOntology_OntologyDoesNotExist_404()
        {
            string inexistentOntologyId = "INEXISTENTONTOLOGYID";
            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", "SELECT *"}
            };
            //Act
            var response = await _client.PostAsync($"{inexistentOntologyId}/query", new FormUrlEncodedContent(formValues));
            //Assert
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Invalid query
        [Fact]
        public async Task RunSparQLQueryOnOntology_InvalidQuery_400()
        {
            string query = "invalid query in some invalid format";
            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", query}
            };

            var response = await _client.PostAsync($"{_ontologyId}/query", new FormUrlEncodedContent(formValues));
        
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        //Valid query but is not readonly
        [Fact]
        public async Task RunSparQLQueryOnOntology_IllegalQuery_400()
        {
            string illegalQuery = $"DELETE *";

            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", illegalQuery}
            };

            var response = await _client.PostAsync($"{_ontologyId}/query", new FormUrlEncodedContent(formValues));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        //The query is successful but does not return anything
        [Fact]
        public async Task RunSparQLQueryOnOntology_ValidQueryButWontReturnAnything_204()
        {
            string query = $"PREFIX foaf: <http://xmlns.com/foaf/0.1/> SELECT * WHERE {{?thing a foaf:INEXISTENTTYPE}}";

            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", query}
            };

            var response = await _client.PostAsync($"{_ontologyId}/query", new FormUrlEncodedContent(formValues));

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        //The query is successful and returns something
        [Fact]
        public async Task RunSparQLQQueryOnOntology_ValidQueryReturnsSomething_Json()
        {
            string query = $"SELECT * WHERE {{?s ?p ?o}}";

            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", query}
            };

            var response = await _client.PostAsync($"{_ontologyId}/query", new FormUrlEncodedContent(formValues));
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.True(root.EnumerateArray().Any());
        }
    }
}
