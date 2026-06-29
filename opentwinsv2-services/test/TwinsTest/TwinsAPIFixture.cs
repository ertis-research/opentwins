namespace TwinsTest
{

    using System.Text.RegularExpressions;
    using Microsoft.AspNetCore.Mvc.Testing;
    using OpenTwinsV2.Twins.Services;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;
    using System.Text.Json.Nodes;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Net.Http.Headers;
    using Json.More;
    using OpenTwinsV2.Shared.Models;
    using OpenTwinsV2.Twins.Builders;
    using VDS.RDF;
    using Dapr.Actors;

    public class TwinsAPIFixture : IAsyncLifetime
    {
        public HttpClient OntologiesClient { get; private set; } = null!;
        public HttpClient ThingsClient { get; private set; } = null!;
        public HttpClient ShapesClient {get; private set;} = null!;
        public HttpClient TwinsClient {get; private set;} = null!;
        private WebApplicationFactory<Twins.TestMaker> _factory = null!;
        public DGraphService DGraphService { get; private set; } = null!;
        public ThingsService ThingsService { get; private set; } = null!;

        // --------------------- Ontology related ---------------------
        public string ontologyId = "ontologiaprueba";
        public string ontologyIdImported = "anothertestontology";
        public string? thingIdOfImported = null!;
        public string thingId = null!;
        public string relationName = null!;
        public string attributeKey = null!;
        public string? parent = null!;
        public string? child = null!;
        public string? childless = null!;
        public string? orphan = null!;
        public string instanciatedThingId = "instanciatedThingId";
        public int ontologyInitCount = 0;

        // --------------------- Shape related ---------------------

        public string shapeId = "shapeprueba";
        public string shapeIdImported = "anothertestshapegraph";
        public int shapeInitCount = 0;
        public string nodeShapeId = null!;

        // --------------------- Twin related ---------------------

        public string twinId = "urn:twinprueba";
        private string twinUid = null!;
        public string twinIdImported = "urn:anothertesttwin";
        public string shapeIdValidates = "validatingexistingshapegraph";
        public string shapeIdNotValidates = "unvalidatingexistingshapegraph";
        public string thingIdInTwin = null!;
        public string thingIdInTwinImported = null!;

        #region Before All

        public async Task InitializeAsync() 
        {
            await WaitForServiceReadyAsync("http://localhost:5001/health");
            await WaitForServiceReadyAsync("http://localhost:5013/health");

            Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "52208");

            _factory = new WebApplicationFactory<Twins.TestMaker>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddSingleton<DGraphService>();
                        services.AddSingleton<ThingsService>();
                    });
            });

            ThingsClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost:5001/things/") });
            ShapesClient = _factory.CreateClient(new WebApplicationFactoryClientOptions{ BaseAddress = new Uri("http://localhost:5013/shapes/") });
            OntologiesClient = _factory.CreateClient(new WebApplicationFactoryClientOptions{ BaseAddress = new Uri("http://localhost:5013/ontologies/")});
            TwinsClient = _factory.CreateClient(new WebApplicationFactoryClientOptions{ BaseAddress = new Uri("http://localhost:5013/twins/")});

            DGraphService = _factory.Services.GetRequiredService<DGraphService>();
            ThingsService = _factory.Services.GetRequiredService<ThingsService>();

            await LoadNQuadsIntoDGraph("existingOntologyNQuads.txt", existing: true, shape: false);
            await LoadNQuadsIntoDGraph("existingShapeGraphNQuads.txt", existing: true, shape: true);
            await LinkOntologyToShapeGraph(ontologyId, shapeId); //by default, linked
            await InitializeSampleThingsAndTwin(twinId, "existingOntologyGraph.json");
            await LoadNQuadsIntoDGraph("existingTwinShapeGraphNotValidatesNQuads.txt", existing: false, shape: true);
            await LoadNQuadsIntoDGraph("existingTwinShapeGraphValidatesNQuads.txt", existing: false, shape: true);

            ontologyInitCount = await GetOntologyCount();
            shapeInitCount = await GetShapeCount();
        }

        #endregion

        #region After All

        public async Task DisposeAsync()
        {
            Console.WriteLine("ENTRA EN AFTERALL");
            // await ThingsClient.DeleteAsync($"{instanciatedThingId}");
            await DGraphService.DeleteByOntologyId(ontologyId);
            await DGraphService.DeleteByOntologyId(ontologyIdImported);
            await DGraphService.DeleteShapeGraphByIdAsync(shapeId);
            await DGraphService.DeleteShapeGraphByIdAsync(shapeIdValidates);
            await DGraphService.DeleteShapeGraphByIdAsync(shapeIdNotValidates);
            try
            {
                await DeleteTwinAndItsThings(twinId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            var graph = (await GetJsonGraph("existingOntologyGraph.json")).GetProperty("@graph").EnumerateArray().Select(t => t.GetProperty("id").GetString());
            foreach(var id in graph)
            {
                try
                {
                    await DeleteThing(id!);
                }catch(KeyNotFoundException){}
                try
                {
                    await DeleteThingInDGraph(id!);
                }catch(KeyNotFoundException){}
            }
            
            _factory!.Dispose();
        }

        #endregion

        #region Get From Files

        public async Task InitializeSampleThingsAndTwin(string twinId, string fileName)
        {
            var payload1 = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = twinId,
                ["title"] = "",
                ["properties"] = new JsonObject { },
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { }
            };
            await ThingsService.CreateThingAsync(twinId, payload1);
            // await ThingsClient.PostAsJsonAsync("", payload1);
            var response = await DGraphService.AddThingAsync(ThingBuilder.BuildTwin(twinId));
            //load the sample ontology graph from file
            var file = await GetJsonGraph(fileName);
            var graph = file.GetProperty("@graph").EnumerateArray();

            twinUid = response.Uids.FirstOrDefault().Value;

            foreach(var thing in graph)
                await InstanciateThing(thing, twinUid, defaultTwin: twinId==this.twinId);
                
        }

        public async Task<JsonElement> GetJsonGraph(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", fileName);
            using var doc = JsonDocument.Parse(File.OpenRead(path));
            return doc.RootElement.Deserialize<JsonElement>();
        }

        #endregion

        #region Delete

        public async Task DeleteTwinAndItsThings(string twinId)
        {
            var things = await DGraphService.GetThingsInTwinAsync(twinId);
            foreach(var thing in things)
            {
                var thingId = thing.GetProperty("thingId").GetString()!;
                if (!string.IsNullOrEmpty(thingId))
                {
                    try
                    {
                        await ThingsService.DeleteThingAsync(thingId);
                    }catch(KeyNotFoundException){}
                    
                    try{
                        await DGraphService.RemoveThingFromTwinAsync(thingId, twinId);
                    }catch (Exception){}
                    try
                    {
                        await DeleteThingInDGraph(thingId);
                    }catch(KeyNotFoundException){}
                }
            }
            try
            {
                await ThingsService.DeleteThingAsync(twinId);
            }catch(KeyNotFoundException){}
            
            try
            {
                await DGraphService.DeleteThingAsync(twinId);
            }catch(KeyNotFoundException){}
        }

        public async Task DeleteOntology(string id)
        {
            await DGraphService.DeleteByOntologyId(id);
        }

        public async Task DeleteShapeGraph(string id)
        {
            await DGraphService.DeleteShapeGraphByIdAsync(id);
        }

        public async Task DeleteThing(string id)
        {
            await ThingsService.DeleteThingAsync(id);
        }

        public async Task DeleteThingInDGraph(string id)
        {
            await DGraphService.DeleteThingAsync(id);
        }

        public async Task DeleteGraph(JsonElement graph)
        {
            foreach(var thing in graph.GetProperty("@graph").EnumerateArray())
                await DeleteThing(thing.GetProperty("@id").GetString() ?? "");
        }

        #endregion

        #region Counts

        public static int GetActualCount(string jsonStr)
        {
            using var jsonDoc = JsonDocument.Parse(jsonStr);
            int count = jsonDoc.RootElement.TryGetProperty("totalCount", out var countEl) ? countEl.GetInt32() : 0;
            return count;
        }

        public async Task<int> GetOntologyCount()
        {
            return (await DGraphService.GetAllOntologiesIdsAsync(1,100,null)).TotalCount;
        }

        public async Task<int> GetShapeCount()
        {
            return (await DGraphService.GetAllShapeGraphsAsync(1,100,null)).TotalCount;
        }

        public async Task<int> GetTwinCount()
        {
            return (await DGraphService.GetAllTwinsAsync(1,100,null)).TotalCount;
        }

        public async Task<int> GetThingCountInTwin(string id)
        {
            return (await DGraphService.GetThingsInTwinAsync(id)).Count;
        }

        #endregion

        #region Exists

        public async Task<bool> ExistsShapeGraph(string id)
        {
            return await DGraphService.ExistsShapeGraphByIdAsync(id);
        }

        public async Task<bool> ExistsOntology(string id)
        {
            return await DGraphService.ExistsOntologyByIdAsync(id);
        }

        public async Task<bool> ExistsTwin(string id)
        {
            return await DGraphService.ExistsTwinAsync(id);
        }

        public async Task<bool> ExistsThing(string id)
        {
            try{
                await ThingsService.GetThingAsync(id);
                return true;
            }
            catch (ActorMethodInvocationException ex)
            {
                if(ex.Message.Contains("KeyNotFoundException"))
                    return false;
                else if(ex.Message.Contains("InvalidOperationException"))
                    return true;
                else
                    throw;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        #endregion

        #region Auxiliary

        public static JsonElement GetJsonRoot(string jsonString)
        {
            using var jsonDoc = JsonDocument.Parse(jsonString);
            return jsonDoc.RootElement.Clone();
        }

         private List<(string Subject, string Predicate, string Object)> FormatNQuadsList(List<string> nquads)
        {
            var pattern = @"^(?<subject>\S+)\s+<(?<predicate>[^>]+)>\s+(?<object>.+?)\s*\.";

            return nquads
                .Where(n => n.Contains("<thingId>") || n.Contains("<Relation.name>") || n.Contains("<Attribute.key>") || n.Contains("<inheritsFrom>") || n.Contains("nodeShapeId"))
                .Select(n => Regex.Match(n, pattern))
                .Where(m => m.Success)
                .Select(m => (
                    Subject: m.Groups["subject"].Value,
                    Predicate: m.Groups["predicate"].Value,
                    Object: m.Groups["object"].Value is string obj && obj.Length >= 2 && obj[0] == '"' && obj[^1] == '"' ? obj[1..^1] : m.Groups["object"].Value
                )).ToList();
        }

        private (string ThingId, string? ParentId, string? ChildId, string? Orphan, string? Childless, string RelationName, string AttributeKey) GetFirstIdsInOntologyNQuads(List<string> nquads)
        {
            var result = FormatNQuadsList(nquads);

            var inheritNQuads = result.Where(r => r.Predicate == "inheritsFrom").ToList();
            var thingIdNQuads = result.Where(r => r.Predicate == "thingId").ToList();
            (string Subject, string Predicate, string Object)? inheritNQuad = inheritNQuads.FirstOrDefault();
            var childUid = inheritNQuad?.Subject;
            var parentUid = inheritNQuad?.Object;

            var childId = childUid is null ? null : thingIdNQuads.First(r =>r.Subject == childUid).Object;
            var parentId = parentUid is null ? null : thingIdNQuads.First(r =>r.Subject == parentUid).Object;
            var thing = parentId ?? thingIdNQuads.First().Object;


            var orphan = inheritNQuads.Count == 0 ? thing : thingIdNQuads.First(r => !inheritNQuads.Any(inh => inh.Subject == r.Subject)).Object;
            var childless = inheritNQuads.Count == 0 ? thing : thingIdNQuads.First(r => !inheritNQuads.Any(inh => inh.Object == r.Subject)).Object;
            var relationNameStr = result.First(r => r.Predicate=="Relation.name").Object;
            var attributeKeyStr= result.First(r => r.Predicate=="Attribute.key").Object;
            Console.WriteLine($"Parent: {parentId}, Child: {childId}, orphan: {orphan}, childless: {childless}, relation: {relationNameStr}, attribute: {attributeKeyStr}");
            return (thing, parentId, childId, orphan, childless, relationNameStr, attributeKeyStr);
        }

        private string GetFirstIdsInShapeGraphNQuads(List<string> nquads)
        {
            var result = FormatNQuadsList(nquads);

            var nodeShapeIds = result.Where(n => n.Predicate == "nodeShapeId").ToList();
            if(nodeShapeIds.Count == 0)
                return "";
            
            return nodeShapeIds.First().Object;
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

        private static string GetMimeType(string filePath)
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

        #endregion

        #region  Instanciate

        private async Task LoadNQuadsIntoDGraph(string fileName, bool existing = false, bool shape = false)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", fileName);
            var nquads = File.ReadAllLines(path).ToList();
            if(existing){
                if(!shape)
                    (thingId, parent, child, orphan, childless, relationName, attributeKey) = GetFirstIdsInOntologyNQuads(nquads);
                else{
                    nodeShapeId = GetFirstIdsInShapeGraphNQuads(nquads);
                }
            }else
                if(!shape)
                    (thingIdOfImported, _,_,_,_,_,_) = GetFirstIdsInOntologyNQuads(nquads);
            Console.WriteLine($"The thing imported is: {thingIdOfImported}");
            try
            {
                await DGraphService.AddNQuadTripleAsync(nquads);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong while uploading to DGraph: " + ex.Message);
            }
        }

        public async Task ImportSampleOntology()
        {
            await LoadNQuadsIntoDGraph("importingOntologyNQuads.txt", existing: false, shape: false);
        }

        public async Task ImportShampleShapeGraph()
        {
            await LoadNQuadsIntoDGraph("importingShapeGraphNQuads.txt", existing:false, shape:true);
        }

        public async Task ImportEmptyTwin(string twinId)
        {
            var payload1 = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = twinId,
                ["title"] = "",
                ["properties"] = new JsonObject { },
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { }
            };
            await ThingsService.CreateThingAsync(twinId, payload1);
            await DGraphService.AddThingAsync(ThingBuilder.BuildTwin(twinId));
        }

        private async Task InstanciateThing(JsonElement thing, string? twinUid = null, bool defaultTwin = false)
        {
            var thingNode = thing.AsNode()!.AsObject();
            var thingId = thingNode["@id"]!.GetValue<string>();
            if(defaultTwin)
                thingIdInTwin ??= thingId;
            var type = thingNode["@type"]!.GetValue<string>();
            await DGraphService.CreateInstanciatedThingAsync(type, thingId, ontologyId: type[..type.IndexOf(':')], twinUid: twinUid);
            await ThingsService.CreateThingAsync(thingId, thingNode);
        }

        #endregion

        #region Default Shape Graphs

        public async Task LinkOntologyToShapeGraph(string ontId, string sId)
        {
            await DGraphService.AddDefaultShapeGraphInOntology(ontId, sId);
        }

        #endregion

        #region Thing Specific

        public async Task<JsonElement> GetThingsState(string id)
        {
            try
            {
                return await ThingsService.GetThingState(id);
            }catch(KeyNotFoundException){return new();}
        }

        public async Task<string> AddSampleThingIntoDefaultTwin(bool addToTwin=true)
        {
            var graph = await GetJsonGraph("importingTwinGraph.json");
            var thing = graph.GetProperty("@graph").EnumerateArray().First();

            thingIdInTwinImported = thing.GetProperty("id").GetString()!;

            await InstanciateThing(thing, twinUid: addToTwin ? this.twinUid : null, defaultTwin: false);
            return thingIdInTwinImported;
        }

        public async Task<string> AddSampleThingUnrelatedToTwin()
        {
            return await AddSampleThingIntoDefaultTwin(addToTwin:false);
        }

        public async Task DeleteSampleThingFromDefaultTwin()
        {
            await DGraphService.RemoveThingFromTwinAsync(thingIdInTwinImported, twinId);
            await DeleteThingInDGraph(thingIdInTwinImported);
            await DeleteThing(thingIdInTwinImported);
            thingIdInTwinImported = null!;
        }

        #endregion

        public static HttpContent GetHttpContentForPostRequest(string path, string fieldName)
        {
            var fileStream = File.OpenRead(path);
            var content = new MultipartFormDataContent();

            // Add the file content
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeType(path)); //appropriate MIME type
            string fileName = Path.GetFileName(path);
            content.Add(fileContent, fieldName, fileName);
            return content;
        }
    }
}