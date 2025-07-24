using Microsoft.AspNetCore.Mvc;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Twins.Services;

namespace OpenTwinsV2.Twins.Controllers
{
    [ApiController]
    [Route("ontologies")]
    public class OntologiesController : ControllerBase
    {
        private readonly DGraphService _dgraphService;
        private const string ActorType = Actors.ThingActor;

        public OntologiesController(DGraphService dgraphService)
        {
            _dgraphService = dgraphService;
        }
    }
}