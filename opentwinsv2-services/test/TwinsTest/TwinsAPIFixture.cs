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

    public class TwinsAPIFixture : IAsyncLifetime
    {
        public HttpClient OntologiesClient { get; private set; } = null!;
        public HttpClient ThingsClient { get; private set; } = null!;
        public HttpClient ShapesClient {get; private set;} = null!;
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
        public int initCount = 0;

        // --------------------- Shape related ---------------------

        public string shapeId = "shapeprueba";
        public string shapeIdImported = "anothertestshapegraph";


        public async Task DisposeAsync()
        {
            Console.WriteLine("ENTRA EN AFTERALL");
            await ThingsClient.DeleteAsync($"{instanciatedThingId}");
            await DGraphService.DeleteByOntologyId(ontologyId);
            await DGraphService.DeleteShapeGraphByIdAsync(shapeId);
            _factory!.Dispose();
        }

        public async Task InitializeAsync()
        {
            await WaitForServiceReadyAsync("http://localhost:5001/health");
            await WaitForServiceReadyAsync("http://localhost:5013/health");

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

            DGraphService = _factory.Services.GetRequiredService<DGraphService>();
            ThingsService = _factory.Services.GetRequiredService<ThingsService>();

            await LoadNQuadsIntoDGraph("existingOntologyNQuads.txt", existing: true, shape: false);
            await LoadNQuadsIntoDGraph("existingShapeGraphNQuads.txt", existing: true, shape: true);
            await LinkOntologyToShapeGraph(ontologyId, shapeId); //by default, linked

            // await InstanciateThing(instanciatedThingId, thingId);
            initCount = await GetOntologyCount();
        }

        public async Task<int> GetOntologyCount()
        {
            return (await DGraphService.GetAllOntologiesIdsAsync(1,100,null)).TotalCount;
        }

        public async Task LinkOntologyToShapeGraph(string ontId, string sId)
        {
            await DGraphService.AddDefaultShapeGraphInOntology(ontId, sId);
        }

        public async Task<JsonElement> GetOntologyGraph(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", fileName);
            using var doc = JsonDocument.Parse(File.OpenRead(path));
            return doc.RootElement.Deserialize<JsonElement>();
        }

        private async Task LoadNQuadsIntoDGraph(string fileName, bool existing = false, bool shape = false)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "ExampleFiles", fileName);
            var nquads = File.ReadAllLines(path).ToList();
            if(existing){
                if(!shape)
                    (thingId, parent, child, orphan, childless, relationName, attributeKey) = GetFirstIds(nquads);
                //TODO: shape metadata
            }else
                if(!shape)
                    (thingIdOfImported, _,_,_,_,_,_) = GetFirstIds(nquads);
            Console.WriteLine($"Ahora mismo el thingId importado es: {thingIdOfImported}");
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

        public async Task DeleteOntology(string id)
        {
            await DGraphService.DeleteByOntologyId(id);
        }

        public async Task DeleteShapeGraph(string id)
        {
            await DGraphService.DeleteShapeGraphByIdAsync(id);
        }

        public async Task<bool> ExistsShapeGraph(string id)
        {
            return await DGraphService.ExistsShapeGraphByIdAsync(id);
        }

        private async Task InstanciateThing(string thingId, string typeId)
        {
            var payload = new JsonObject
            {
                ["@context"] = new JsonArray("https://www.w3.org/2019/wot/td/v1"),
                ["id"] = thingId,
                ["title"] = "",
                ["hasType"] = typeId,
                ["properties"] = new JsonObject { }, //provisional
                ["actions"] = new JsonObject { },
                ["events"] = new JsonObject { }
            };
            var response = await ThingsClient.PostAsJsonAsync("", payload);
            Console.WriteLine($"Thing with id {thingId} was {(response.IsSuccessStatusCode ? "" : "not")} successfully instanciated in Things");
        }

        private (string ThingId, string? ParentId, string? ChildId, string? Orphan, string? Childless, string RelationName, string AttributeKey) GetFirstIds(List<string> nquads)
        {
            var pattern = @"^(?<subject>\S+)\s+<(?<predicate>[^>]+)>\s+(?<object>.+?)\s*\.";

            var result = nquads
                .Where(n => n.Contains("<thingId>") || n.Contains("<Relation.name>") || n.Contains("<Attribute.key>") || n.Contains("<inheritsFrom>"))
                .Select(n => Regex.Match(n, pattern))
                .Where(m => m.Success)
                .Select(m => (
                    Subject: m.Groups["subject"].Value,
                    Predicate: m.Groups["predicate"].Value,
                    Object: m.Groups["object"].Value is string obj && obj.Length >= 2 && obj[0] == '"' && obj[^1] == '"' ? obj[1..^1] : m.Groups["object"].Value
                )).ToList();

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

        public async Task InstanciateThing(JsonElement thing)
        {
            await ThingsClient.PostAsJsonAsync("", new {value= thing});
        }

        public async Task DeleteThing(string id)
        {
            await ThingsClient.DeleteAsync(id);
        }

        public async Task DeleteGraph(JsonElement graph)
        {
            foreach(var thing in graph.GetProperty("@graph").EnumerateArray())
                await DeleteThing(thing.GetProperty("@id").GetString() ?? "");
        }

        public async Task<bool> ExistsThing(string id)
        {
            return (await ThingsClient.GetAsync(id)).IsSuccessStatusCode;
        }
    }
}