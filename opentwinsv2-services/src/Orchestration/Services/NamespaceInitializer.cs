using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Orchestration.Services;

namespace OpenTwinsV2.Orchestration.Services
{
    public class NamespaceInitializer : IHostedService
    {
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _defaultNs;

        public NamespaceInitializer(IConfiguration config, IServiceScopeFactory scopeFactory)
        {
            _config = config;
            _defaultNs = config["ConnectionNamespace"] ?? throw new MissingFieldException("There is no Namespace Name stored in Configuration");
            // _k8s = k8s;
            _scopeFactory = scopeFactory;
        }

        private async Task EnsureNamespaceExistsAsync(KubernetesService k8s, string namespaceName)
        {
            if(!await k8s.ExistsNamespace(namespaceName))
                await k8s.CreateNamespace(namespaceName);
            
            var secretName = _config["BenthosWorker:PullSecret"] ?? "k8s--orchestration--secret";
            var user = _config["BenthosWorker:MqttUsername"] ?? "your_user";
            var pwd = _config["BenthosWorker:MqttPassword"] ?? "your_password";
            var secret = new V1Secret
            {
                Metadata = new V1ObjectMeta { Name = secretName },
                Type = "Opaque", // Standard, simple secret
                StringData = new Dictionary<string, string>
                {
                    { "username", user },
                    { "password", pwd }
                }
            };
            await k8s.CreateSecretInNamespace(secretName, _defaultNs, secret, overrideSecret: true);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (IServiceScope scope = _scopeFactory.CreateScope())
            {
                var k8s = scope.ServiceProvider.GetRequiredService<KubernetesService>();
                await EnsureNamespaceExistsAsync(k8s, _defaultNs);
                //First, obtain the pods
                var podsList = (await k8s.GetAllBenthosPods(_defaultNs)).Items;
                foreach(var pod in podsList)
                {
                    if(!await k8s.IsPodHealthy(pod, _defaultNs))
                    {
                        string originalJobId = k8s.GetOriginalValue(pod);
                        bool connection = await k8s.IsPodAConnection(originalJobId, _defaultNs);
                        await k8s.RestartBenthosPod(pod, _defaultNs, connection);
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}