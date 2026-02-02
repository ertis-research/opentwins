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
            _defaultNs = config["ConnectorNamespace"] ?? throw new MissingFieldException("There is no Namespace Name stored in Configuration");
            // _k8s = k8s;
            _scopeFactory = scopeFactory;
        }

        private async Task EnsureNamespaceExistsAsync(KubernetesService k8s, string namespaceName)
        {
            if(!await k8s.ExistsNamespace(namespaceName))
                await k8s.CreateNamespace(namespaceName);
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
                        bool connector = await k8s.IsPodAConnector(pod.Metadata.Name, _defaultNs);
                        await k8s.RestartBenthosPod(pod, _defaultNs, connector);
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}