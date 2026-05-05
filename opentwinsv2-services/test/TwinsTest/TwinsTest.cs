using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Api;
using Json.More;
using Lucene.Net.QueryParsers.Flexible.Standard.Processors;
using Microsoft.VisualBasic;
using OpenTwinsV2.Shared.Models;
using Xunit;

namespace TwinsTest;

[CollectionDefinition("Twins Tests")]
public class TwinsCollection : ICollectionFixture<TwinsAPIFixture>
{
    //marker for xunit
}

[Collection("Twins Tests")]

public abstract class TwinsTest : IAsyncLifetime
{
    protected virtual Task CleanupAfterTestAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        await CleanupAfterTestAsync();
    }
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    private readonly HttpClient _client;
    private readonly TwinsAPIFixture _fixture;
    private readonly string _twinId;
    private readonly string _importedTwinId;
    private string _importedThingId = null!;
    private readonly string _shapeIdNotValidates;
    private readonly string _shapeIdValidates;
    private readonly string _thingId;

    public TwinsTest(TwinsAPIFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.TwinsClient;
        _twinId = fixture.twinId;
        _importedTwinId = fixture.twinIdImported;
        _shapeIdNotValidates = fixture.shapeIdNotValidates;
        _shapeIdValidates = fixture.shapeIdValidates;
        _thingId = fixture.thingIdInTwin;
        // _importedThingId = fixture.thingIdInTwinImported;
    }

    public class GetAllTwinsTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //It is successful
        [Fact]
        public async Task GetAllTwinsTest_Nothing_200AndPagedResult()
        {
            var initCount = await _fixture.GetTwinCount();
            var responseGet = await _client.GetAsync("");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var count = TwinsAPIFixture.GetActualCount(jsonString);

            Assert.True(responseGet.IsSuccessStatusCode);
            Assert.Equal(initCount, count);
        }
    }

    public class CreateTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The id is already on use
        [Fact]
        public async Task CreateTwinTest_IdOnUse_Conflict()
        {
            var initCount = await _fixture.GetTwinCount();
            var twinId = _twinId;
            var response = await _client.PostAsync($"./{twinId}", new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(initCount, await _fixture.GetTwinCount());
        }

        //The optional Shape Graph does not exist
        [Fact]
        public async Task CreateTwinTest_ShapeGraphDoesNotExist_NotFound()
        {
            string twinId = _importedTwinId;
            var initCount = await _fixture.GetTwinCount();
            var shapeId = "INEXISTENTSHAPEID";
            
            var response = await _client.PostAsync($"./{twinId}?shapeId={shapeId}", new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(initCount, await _fixture.GetTwinCount());
        }
        
        //The id is not on use and the Shape Graph does not validate the graph
        [Fact]
        public async Task CreateTwinTest_ShapeGraphDoesNotValidate_BadRequest()
        {
            string twinId = _importedTwinId;
            var initCount = await _fixture.GetTwinCount();
            var shapeId = _shapeIdNotValidates;
            var graph = await _fixture.GetJsonGraph("importingTwinGraph.json");

            var response = await _client.PostAsync($"./{twinId}?shapeId={shapeId}", new StringContent(graph.ToJsonString(), Encoding.UTF8, "application/json"));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(initCount, await _fixture.GetTwinCount());
        }

        //The id is not on use and the graph is empty
        [Fact]
        public async Task CreateTwinTest_GraphIsEmpty_200AndEmptyTwin()
        {
            string twinId = _importedTwinId;
            var initCount = await _fixture.GetTwinCount();
            var graph = new JsonObject
            {
                ["@graph"] = new JsonArray()
            };
            
            var responsePost = await _client.PostAsync($"./{twinId}", new StringContent(graph.ToJsonString(), Encoding.UTF8, "application/json"));
            var responseGet = await _client.GetAsync($"./{twinId}/things");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(responsePost.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, responsePost.StatusCode);
            Assert.Equal(initCount+1, await _fixture.GetTwinCount());
            Assert.True(root.AsNode() is JsonArray);
            Assert.Empty(root.EnumerateArray());
        }

        //The id is not on use and the graph is of bad format
        [Fact]
        public async Task CreateTwinTest_GraphIsOfBadFormat_InternalServerError()
        {
            string twinId = _importedTwinId;
            var initCount = await _fixture.GetTwinCount();
            var graph = new JsonObject();

            var response = await _client.PostAsync($"./{twinId}", new StringContent(graph.ToJsonString(), Encoding.UTF8, "application/json"));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(initCount, await _fixture.GetTwinCount());
        }

        //The id is not on use and the graph is valid
        [Fact]
        public async Task CreateTwinTest_GraphIsValid_200AndTwinCreated()
        {
            string twinId = _importedTwinId;
            var initCount = await _fixture.GetTwinCount();
            var shapeId = _shapeIdValidates;
            var graph = await _fixture.GetJsonGraph("importingTwinGraph.json");

            var responsePost = await _client.PostAsync($"./{twinId}?shapeId={shapeId}", new StringContent(graph.ToJsonString(), Encoding.UTF8, "application/json"));
            var responseGet = await _client.GetAsync($"./{twinId}/things");
            var jsonString = await responseGet.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(responsePost.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, responsePost.StatusCode);
            Assert.Equal(initCount+1, await _fixture.GetTwinCount());
            Assert.True(root.AsNode() is JsonArray);
            Assert.NotEmpty(root.EnumerateArray());
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsTwin(_importedTwinId)){
                await _fixture.DeleteTwinAndItsThings(_importedTwinId);
            
            }
            // if(await _fixture.ExistsTwin(_safeImportedTwinId)){
            //     await _fixture.DeleteTwinAndItsThings(_safeImportedTwinId);
            
            // }
        }
    }

    public class GetTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task GetTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXTISTENTTWINID";

            var response = await _client.GetAsync(twinId);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Twin exists but has no Things
        [Fact]
        public async Task GetTwin_TwinHasnoThings_NotFound()
        {
            string twinId = _importedTwinId;
            await _fixture.ImportEmptyTwin(_importedTwinId);
            
            var response = await _client.GetAsync($"./{twinId}");

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Twin exists and has Things
        [Fact]
        public async Task GetTwin_TwinHasThings_200AndTwin()
        {
            string twinId = _twinId;

            var response = await _client.GetAsync($"./{twinId}");
            var content = await response.Content.ReadAsStringAsync();

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(content);
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsTwin(_importedTwinId))
                await _fixture.DeleteTwinAndItsThings(_importedTwinId);
        }
    }

    public class DeleteTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task DeleteTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";
            var initCount = await _fixture.GetTwinCount();

            var response = await _client.DeleteAsync(twinId);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(initCount, await _fixture.GetTwinCount());
        }

        //The Twin exists
        [Fact]
        public async Task DeleteTwin_TwinExists_TwinDeleted()
        {
            string twinId = _importedTwinId;
            // var thingIds = (await _fixture.GetJsonGraph("importingTwinGraph.json")).GetProperty("@graph").EnumerateArray().Select(n => n.GetProperty("id").GetString()).ToList();
            await _fixture.InitializeSampleThingsAndTwin(twinId, "importingTwinGraph.json");
            var initCount = await _fixture.GetTwinCount();

            var responseDelete = await _client.DeleteAsync($"./{twinId}");
            var responseGet = await _client.GetAsync($"./{twinId}");

            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NoContent, responseDelete.StatusCode);
            Assert.False(responseGet.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, responseGet.StatusCode);
            Assert.Equal(initCount-1, await _fixture.GetTwinCount());
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsTwin(_importedTwinId))
                await _fixture.DeleteTwinAndItsThings(_importedTwinId);

            var thingIds = (await _fixture.GetJsonGraph("importingTwinGraph.json")).GetProperty("@graph").EnumerateArray().Select(n => n.GetProperty("id").GetString()).ToList();
            foreach(var id in thingIds)
            {
                try
                {
                    await _fixture.DeleteThing(id!);
                }catch(KeyNotFoundException){}
                try
                {
                    await _fixture.DeleteThingInDGraph(id!);
                }catch(KeyNotFoundException){}
            }
                
        }
    }

    public class GetThingsOfTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task GetThingsOfTwin_TwinDoesNotExist_EmptyList()
        {
            string twinId = "INEXISTENTTWINID";

            var response = await _client.GetAsync($"{twinId}/things");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(root.AsNode() is JsonArray);
            Assert.Empty(root.EnumerateArray());
        }

        //The Twin has no Things
        [Fact]
        public async Task GetThingsOfTwin_TwinHasNoThings_EmptyList()
        {
            string twinId = _importedTwinId;
            await _fixture.ImportEmptyTwin(twinId);

            var response = await _client.GetAsync($"./{twinId}/things");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(root.AsNode() is JsonArray);
            Assert.Empty(root.EnumerateArray());
        }

        //The Twin has Things
        [Fact]
        public async Task GetThingsOfTwin_TwinHasThings_ListOfThings()
        {
            string twinId = _twinId;

            var response = await _client.GetAsync($"./{twinId}/things");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(root.AsNode() is JsonArray);
            Assert.NotEmpty(root.EnumerateArray());
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsTwin(_importedTwinId))
                await _fixture.DeleteTwinAndItsThings(_importedTwinId);
        }
    }

    public class GetThingOfTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task GetThingOfTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";
            string thingId = "INEXISTENTTHINGID";
            
            var response = await _client.GetAsync($"./{twinId}/things/{thingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Thing does not exist
        [Fact]
        public async Task GetThingOfTwin_ThingDoesNotExist_NotFound()
        {
            string twinId = _twinId;
            string thingId = "INEXISTENTTHINGID";

            var response = await _client.GetAsync($"./{twinId}/things/{thingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Thing exists within the Twin
        [Fact]
        public async Task GetThingOfTwin_BothExist_ThingDescription()
        {
            string twinId = _twinId;
            string thingId = _thingId;

            var response = await _client.GetAsync($"./{twinId}/things/{thingId}");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);
            ThingDescription? td = JsonSerializer.Deserialize<ThingDescription>(root);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(td);
            Assert.Equal(thingId, td.Id);
        }
    }

    public class DeleteThingOfTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task DeleteThingOfTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";
            string thingId = "INEXISTENTTHINGID";

            var response = await _client.DeleteAsync($"./{twinId}/things/{thingId}");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Thing does not exist
        [Fact]
        public async Task DeleteThingOfTwin_ThingDoesNotExist_NotFound()
        {
            string twinId = _twinId;
            string thingId = "INEXISTENTTHINGID";
            var responseGet1 = await _client.GetAsync($"./{twinId}/things");
            var jsonString1 = await responseGet1.Content.ReadAsStringAsync();
            var root1 = TwinsAPIFixture.GetJsonRoot(jsonString1);
            var initCount = root1.EnumerateArray().Count();

            var responseDelete = await _client.DeleteAsync($"./{twinId}/things/{thingId}");
            var responseGet2 = await _client.GetAsync($"./{twinId}/things");
            var jsonString2 = await responseGet2.Content.ReadAsStringAsync();
            var root2 = TwinsAPIFixture.GetJsonRoot(jsonString2);
            var count = root2.EnumerateArray().Count();

            Assert.False(responseDelete.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, responseDelete.StatusCode);
            Assert.Equal(initCount, count);
        }

        //The Thing exists within the Twin
        [Fact]
        public async Task DeleteThingOfTwin_ThingExists_200AndThingDeletedFromTwin()
        {
            var twinId = _importedTwinId;
            await _fixture.InitializeSampleThingsAndTwin(twinId, "importingTwinGraph.json");
            var graph = (await _fixture.GetJsonGraph("importingTwinGraph.json")).GetProperty("@graph").EnumerateArray();
            var thingId = graph.First().GetProperty("id").GetString()!;

            var initCount = await _fixture.GetThingCountInTwin(twinId);

            var responseDelete = await _client.DeleteAsync($"./{twinId}/things/{thingId}");
            var count = await _fixture.GetThingCountInTwin(twinId);

            Assert.True(responseDelete.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, responseDelete.StatusCode);
            Assert.Equal(initCount-1, count);
            try
            {
                await _fixture.DeleteThing(thingId);
            }catch(KeyNotFoundException){}
            try
            {
                await _fixture.DeleteThingInDGraph(thingId);
            }catch(KeyNotFoundException){}
            
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsTwin(_importedTwinId))
                await _fixture.DeleteTwinAndItsThings(_importedTwinId);
        }
    } 

    public class GetNodeOfThingOfTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task GetThingOfTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";
            string thingId = "INEXISTENTTHINGID";
            
            var response = await _client.GetAsync($"./{twinId}/things/{thingId}/node");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Thing does not exist
        [Fact]
        public async Task GetThingOfTwin_ThingDoesNotExist_NotFound()
        {
            string twinId = _twinId;
            string thingId = "INEXISTENTTHINGID";

            var response = await _client.GetAsync($"./{twinId}/things/{thingId}/node");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Thing exists within the Twin
        [Fact]
        public async Task GetThingOfTwin_BothExist_ThingDescription()
        {
            string twinId = _twinId;
            string thingId = _thingId;

            var response = await _client.GetAsync($"./{twinId}/things/{thingId}/node");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(root.TryGetProperty("thingId", out var thingIdInResponse));
            Assert.Equal(thingId, thingIdInResponse.GetString());
        }
    }

    public class GetStatesOfThingOfTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task GetStatesOfThingInTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";
            string thingId = "INEXISTENTTHINGID";

            var response = await _client.GetAsync($"./{twinId}/things/{thingId}/state");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Thing does not exist
        [Fact]
        public async Task GetStatesOfThingInTwin_ThingDoesNotExist_NotFound()
        {
            string twinId = _twinId;
            string thingId = "INEXISTENTTHINGID";

            var response = await _client.GetAsync($"./{twinId}/things/{thingId}/state");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Thing exists within the Twin
        [Fact]
        public async Task GetStatesOfThingInTwins_BothExist_State()
        {
            string twinId = _twinId;
            string thingId = _thingId;

            var response = await _client.GetAsync($"./{twinId}/things/{thingId}/state");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);
            var stateInThing = await _fixture.GetThingsState(thingId);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(stateInThing.EnumerateObject(), root.EnumerateObject());
        }
    }

    public class AddThingInTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task AddThingInTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";
            string thingId = "INEXISTENTTHINGID";

            var response = await _client.PutAsync($"./{twinId}/things/{thingId}", new StringContent(""));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //There's at least one thingId that doesn't exist
        [Fact]
        public async Task AddThingInTwin_TheOnlyThingIdDoesNotExist_NotFound()
        {
            string twinId = _twinId;
            string thingIds = $"INEXISTENTTHINGID";
            var initCount = await _fixture.GetThingCountInTwin(twinId);

            var response = await _client.PutAsync($"./{twinId}/things/{thingIds}", new StringContent(""));
            var count = await _fixture.GetThingCountInTwin(twinId);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(initCount, count);
        }

        //A Thing already belongs to the Twin
        [Fact]
        public async Task AddThingInTwin_ThingAlreadyInTwin_OkAndNoThingAdded()
        {
            string twinId = _twinId;
            string thingIds = _thingId;
            var initCount = await _fixture.GetThingCountInTwin(twinId);

            var response = await _client.PutAsync($"./{twinId}/things/{thingIds}", new StringContent(""));
            var count = await _fixture.GetThingCountInTwin(twinId);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(initCount, count);
        }

        //the list of ids is empty
        [Fact]
        public async Task AddThingInTwin_EmptyIdList_MethodNotAllowed()
        {
            string twinId = _twinId;
            string thingIds = "";
            var initCount = await _fixture.GetThingCountInTwin(twinId);

            var response = await _client.PutAsync($"./{twinId}/things/{thingIds}", new StringContent(""));
            var count = await _fixture.GetThingCountInTwin(twinId);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            Assert.Equal(initCount, count);
        }

        //All ids exist
        [Fact]
        public async Task AddThingInTwin_ThingsExistAndAreNotAlreadyInTwin_OkAndThingsAdded()
        {
            _importedThingId = await _fixture.AddSampleThingUnrelatedToTwin();
            string twinId = _twinId;
            StringBuilder sb = new();
            string thingIds = sb.Append(_thingId).Append(',').Append(_importedThingId).ToString();
            var initCount = await _fixture.GetThingCountInTwin(twinId);

            var response = await _client.PutAsync($"./{twinId}/things/{thingIds}", new StringContent(""));
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);
            var count = await _fixture.GetThingCountInTwin(twinId);
            var allOk = root.EnumerateArray().Count(s => s.GetProperty("status").GetInt32() == 200) == root.EnumerateArray().Count();

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.MultiStatus, response.StatusCode);
            Assert.NotEmpty(root.EnumerateArray());
            Assert.True(allOk);
        }

        protected override async Task CleanupAfterTestAsync()
        {
            if(await _fixture.ExistsThing(_importedThingId))
                await _fixture.DeleteSampleThingFromDefaultTwin();
        }
    }

    public class ExportTwinInJsonTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task ExportTwinInJson_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";

            var response = await _client.GetAsync($"./{twinId}/export/Json");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Twin exists
        [Fact]
        public async Task ExportTwinInJson_TwinExists_OkAndJson()
        {
            string twinId = _twinId;

            var response = await _client.GetAsync($"./{twinId}/export/Json");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(root.EnumerateObject());
        }
    }

    public class ExportTwinInJsonLDTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task ExportTwinInJsonLD_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";

            var response = await _client.GetAsync($"./{twinId}/export/JsonLd");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Twin exists
        [Fact]
        public async Task ExportTwinInJsonLD_TwinExists_OkAndJsonLD()
        {
            string twinId = _twinId;

            var response = await _client.GetAsync($"./{twinId}/export/JsonLd");
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(root.TryGetProperty("@context", out _));
            Assert.True(root.TryGetProperty("@graph", out var graphEl));
            Assert.True(graphEl.AsNode() is JsonArray);
        }
    }

    public class ExportTwinInTTLTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task ExportTwinInTTL_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";

            var response = await _client.GetAsync($"./{twinId}/export/TTL");

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //The Twin exists
        [Fact]
        public async Task ExportTwinInJsonLD_TwinExists_OkAndJsonLD()
        {
            string twinId = _twinId;

            var response = await _client.GetAsync($"./{twinId}/export/JsonLd");
            var byteArray = await response.Content.ReadAsByteArrayAsync();

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(byteArray);
            Assert.NotEmpty(byteArray);
        }
    }

    public class RunQueryOnTwinTest(TwinsAPIFixture fixture) : TwinsTest(fixture)
    {
        //The Twin does not exist
        [Fact]
        public async Task RunQueryOnTwin_TwinDoesNotExist_NotFound()
        {
            string twinId = "INEXISTENTTWINID";
            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", "SELECT *"}
            };

            var response = await _client.PostAsync($"./{twinId}/query", new FormUrlEncodedContent(formValues));

            // Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        //Invalid query
        [Fact]
        public async Task RunQueryOnTwin_InvalidQuery_BadRequest()
        {
            string twinId = _twinId;
            string query = "invalid query in some invalid format";
            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", query}
            };

            var response = await _client.PostAsync($"./{twinId}/query", new FormUrlEncodedContent(formValues));
        
            // Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        //Valid query but is not readonly
        [Fact]
        public async Task RunQueryOnTwin_IllegalQuery_BadRequest()
        {
            string twinId = _twinId;
            string illegalQuery = $"DELETE *";

            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", illegalQuery}
            };

            var response = await _client.PostAsync($"./{twinId}/query", new FormUrlEncodedContent(formValues));

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        //The query is successful but does not return anything
        [Fact]
        public async Task RunQueryOnTwin_ValidQueryButWontReturnAnything_OkAndEmptyReport()
        {
            string twinId = _twinId;
            string query = $"PREFIX foaf: <http://xmlns.com/foaf/0.1/> SELECT * WHERE {{?thing a foaf:INEXISTENTTYPE}}";

            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", query}
            };

            var response = await _client.PostAsync($"./{twinId}/query", new FormUrlEncodedContent(formValues));
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(root.EnumerateArray());
        }

        //The query is successful and returns something
        [Fact]
        public async Task RunQueryOnTwin_ValidQueryReturnsSomething_OkAndJson()
        {
            string twinId = _twinId;
            string query = $"SELECT * WHERE {{?s ?p ?o}}";

            var formValues = new Dictionary<string, string>
            {
                {"stringQuery", query}
            };

            var response = await _client.PostAsync($"./{_twinId}/query", new FormUrlEncodedContent(formValues));
            var jsonString = await response.Content.ReadAsStringAsync();
            var root = TwinsAPIFixture.GetJsonRoot(jsonString);

            Assert.True(response.IsSuccessStatusCode);
            Assert.False(string.IsNullOrWhiteSpace(jsonString));
            Assert.True(root.EnumerateArray().Any());
        }
    }
} 