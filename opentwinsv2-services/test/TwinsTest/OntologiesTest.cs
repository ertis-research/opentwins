namespace TwinsTest;

using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Twins;
using OpenTwinsV2.Twins.Services;
using System.Linq;

public class OntologiesTest : IClassFixture<OntologiesAPIFixture>
{
    private readonly HttpClient _client;
    private readonly OntologiesAPIFixture _fixture;
    public DGraphService _dgraphService = null!;

    public OntologiesTest(OntologiesAPIFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client; // get the HttpClient from the fixture
        _dgraphService = fixture.DGraphService;
    }

    [Fact]
    public async Task  exampleTest()
    {

        bool exists = await _dgraphService.ExistsOntologyByIdAsync("ontologiaPrueba");
        Assert.Equal(true, exists);
        
    }

}

public class OntologiesAPIFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; } = null!;
    private WebApplicationFactory<Twins.TestMaker> _factory = null!;
    // public IServiceProvider Services => _factory.Services;
    public DGraphService DGraphService { get; private set; } = null!;
    public Func<IServiceScope> CreateScope { get; private set; } = null!;


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