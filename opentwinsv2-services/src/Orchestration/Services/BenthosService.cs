using k8s;
using k8s.Models;
using System.Text;
using System.Text.Json.Nodes;
using OpenTwinsV2.Orchestration.Formatters;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using OpenTwinsV2.Orchestration.Clients;
using Dapr.Client.Autogen.Grpc.v1;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using k8s.Autorest;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using k8s.ClientSets;
using Orchestration.Services;


namespace OpenTwinsV2.Orchestration.Services
{
    public class BenthosService
    {
        private readonly KubernetesService _k8s;
        private readonly IConfiguration _config;
        private readonly ThingsClient _thingsClient;
        private readonly string _namespaceName;

        public BenthosService(IConfiguration config, ThingsClient thingsClient, KubernetesService k8s)
        {
            _config = config;
            _k8s = k8s;
            _thingsClient = thingsClient ?? throw new ArgumentNullException(nameof(thingsClient));
            _namespaceName = _config["ConnectorNamespace"] ?? throw new MissingFieldException("There is no Namespace Name stored in Configuration");
            //the NamespaceInitializer IHostedService is runned before any comunication of this service, so we assume that it exists (created or alredy existed)
        }

        #region Things Client

        /// <summary>
        /// Checks the health of Things Service.
        /// </summary>
        /// <returns>
        /// Returns true if Things Service is accesible.<br/>
        /// Returns false if Thing Service cannot be reached.
        /// </returns>
        public async Task<bool> CheckThingsHealth()
        {
            return await _thingsClient.CheckHealth();
        }

        /// <summary>
        /// Creates a Thing in Things Service.
        /// </summary>
        /// <param name="thingDescription">The ThingDescription of the Thing in Json format.</param>
        /// <returns></returns>
        /// <exception cref="TimeoutException">Thrown if Things Service health check failed.</exception>
        /// <exception cref="Exception">Thrown if the response obtained from the client is not successful.</exception>
        public async Task CreateThing(JsonNode thingDescription)
        {
            if(!await CheckThingsHealth())
                throw new TimeoutException("Things Service is not available");
            var createResponse = await _thingsClient.CreateThing(thingDescription);
            if(!createResponse)
                throw new Exception($"Something went wrong in the petition to create the Thing.");
        }

        /// <summary>
        /// Creates a new Thing or mkodifies an existing one with the id and Thing Description provided.
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <param name="thingDescription">The ThingDescription of the Thing in Json format.</param>
        /// <returns></returns>
        /// <exception cref="TimeoutException">Thrown if Thingws Service health check failed.</exception>
        /// <exception cref="Exception">Thrown if the response obtained from the client is not successful.</exception>
        public async Task CreateThing(string thingId, JsonNode thingDescription)
        {
            if(!await CheckThingsHealth())
                throw new TimeoutException("Things Service is not available");
            var putResponse = await _thingsClient.CreateThing(thingId, thingDescription); 
            if(!putResponse)
                throw new Exception($"Something went wrong in the petition to create the Thing.");
        }

        /// <summary>
        /// Deletes a Thing in Things Service.
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns></returns>
        /// <exception cref="TimeoutException">Thrown if Things Service health check failed.</exception>
        /// <exception cref="Exception">Thrown if the response obtained from the client is not successful.</exception>
        public async Task DeleteThing(string thingId)
        {
            if(!await CheckThingsHealth())
                throw new TimeoutException("Things Service is not available");
            var deleteResponse = await _thingsClient.DeleteThing(thingId);
            if (!deleteResponse)
                throw new Exception("Something went wrong in the petition to delete the Thing.");
        }

        /// <summary>
        /// Gets the Thing Description of a Thing from Things Service.
        /// </summary>
        /// <param name="thingId">The identiifer of the Thing.</param>
        /// <returns>
        /// Returns the Thing Description of the Thing in Json format. 
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if Things Service health check failed.</exception>
        /// <exception cref="Exception">Thrown if the Thing Description obtained is null.</exception>
        public async Task<JsonNode> GetThing(string thingId)
        {
            if(!await CheckThingsHealth())
                throw new TimeoutException("Things Service is not available");
            var td = await _thingsClient.GetThing(thingId) ?? throw new Exception("The ThingDescription obtained was null");
            return td;
        }

        /// <summary>
        /// Checks if a Thing exists in Things Service.
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <returns>
        /// Returns true if the Thing already exists.<br/>
        /// Returns false if there's no Thing with such identifier.
        /// </returns>
        public async Task<bool> ExistsThing(string thingId)
        {
            return await _thingsClient.ExitsThing(thingId);
        }

        #endregion

        #region K8s & Pods
        
        /// <summary>
        /// Checks if exists the configMap with name generated from given identifier in the namespace.
        /// </summary>
        /// <param name="id">The identifier of the Pod.</param>
        /// <returns>
        /// Returns true if the configMap exists.<br/>
        /// Returns false if the configMap does not exist.
        /// </returns>
        public async Task<bool> ExistsConfigMap(string id)
        {
            return await _k8s.ExistsConfigMap(id, _namespaceName);
        }
        

        /// <summary>
        /// Checks if a Pod or Connector (if specified) exists with a given job identifier.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="connector">OPTIONAL. Wether we are looking for a Connector or not. By defualt: false.</param>
        /// <returns>
        /// Returns true if the Pod or Connector exists.<br/>
        /// Returns false if the Pod or Connector does not exist.
        /// </returns>
        public async Task<bool> ExistsPod(string jobId, bool connector = false)
        {
            return await _k8s.ExistsPod(jobId, _namespaceName, connector);
        }
        

        /// <summary>
        /// Checks if a Connector Exists (Pod and Thing).
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>
        /// Returns true if either the Pod or the Thing exist.<br/>
        /// Returns false if neither the Pod or the Thing exist.
        /// </returns>
        public async Task<bool> ExistsConnector(string thingId, string namespaceName)
        {
            var thing = await ExistsThing(thingId);
            var pod = await _k8s.ExistsPod(BenthosConfigParser.GetJobIdFromThingId(thingId), namespaceName, connector: true);
            return thing || pod;
        }

        /// <summary>
        /// Checks if a Connector Exists (Pod and Thing).
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <returns>
        /// Returns true if either the Pod or the Thing exist.<br/>
        /// Returns false if neither the Pod or the Thing exist.
        /// </returns>
        public async Task<bool> ExistsConnector(string thingId)
        {
            return await ExistsConnector(thingId, _namespaceName);
        }

        /// <summary>
        /// Parses the Benthos configuration from plain text into a configMap.
        /// </summary>
        /// <param name="jobId">The job identifier of the configuration's Pod.</param>
        /// <param name="benthosConfig">The Benthos configuration in plain text.</param>
        /// <param name="thingId">OPTIONAL. The identifier of the Thing.</param>
        /// <returns></returns>
        
        public V1ConfigMap ParseConfigMap(string jobId, string benthosConfig, string thingId = "")
        {
            return ParseConfigMap(jobId, _namespaceName, benthosConfig, thingId);
        }

        /// <summary>
        /// Parses the Benthos configuration from plain text into a configMap.
        /// </summary>
        /// <param name="jobId">The job identifier of the configuration's Pod.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <param name="benthosConfig">The Benthos configuration in plain text.</param>
        /// <param name="thingId">OPTIONAL. The identifier of the Thing.</param>
        /// <returns></returns>
        public V1ConfigMap ParseConfigMap(string jobId,  string namespaceName, string benthosConfig, string thingId = "")
        {
            return new V1ConfigMap
            {
                Metadata = new V1ObjectMeta {Name = BenthosConfigParser.GetConfigName(!string.IsNullOrWhiteSpace(thingId) ? thingId : jobId)},
                Data = new Dictionary<string, string>
                {
                    {"benthos.yaml", benthosConfig}
                }
            };
        }


        public async Task CreateConfigMap(V1ConfigMap configMap)
        {
            await _k8s.CreateConfigMap(configMap, _namespaceName);
        }

        /// <summary>
        /// It replaces the Kubernetes ConfigMap with the provided identifier with the new one. 
        /// </summary>
        /// <param name="configId">The identifier of the Kubernetes ConfigMap.</param>
        /// <param name="newConfig">The new ConfigMap.</param>
        /// <returns></returns>
        public async Task ChangeConfigMap(string configId, V1ConfigMap newConfig)
        {
            await _k8s.ChangeConfigMap(configId, newConfig, _namespaceName);
        }

        /// <summary>
        /// Gets the Kubernetes ConfigMap with the provided identifier.
        /// </summary>
        /// <param name="configId">The identifier of the Kubernetes ConfigMap.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns the specific ConfigMap.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no ConfigMap with such identifier was found.</exception>
        public async Task<V1ConfigMap> GetConfigMap(string configId)
        {
            return await _k8s.GetConfigMap(configId, _namespaceName);
        }

        /// <summary>
        /// Gets the full ConfigMap for Kuebernetes Pod from the provided ThingDescription.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="thingDescription">The ThingDescription of the Connector Thing.</param>
        /// <returns>Returns the full ConfigMap for the Connector Thing.</returns>
        public async Task<V1ConfigMap> GetConnectorConfigFromThingDescription(string thingId, JsonNode thingDescription)
        {
            var input = BenthosConfigParser.GetInputFromThingDescription(thingDescription);
            var configMap = ParseThingDescriptionIntoBenthosConfig(thingDescription, thingId, input);
            var yaml = await GetKafkaOutput("./Sources/kafka.yaml");
            configMap = await MergeOutputYamlIntoConfig(yaml, configMap);
            return configMap;
        }

        /// <summary>
        /// Creates a Benthos Pod.
        /// </summary>
        /// <param name="jobId">The identifier of the job.</param>
        /// <param name="thingId">OPTIONAL. The identifier of the Connector Thing.</param>
        /// <returns></returns>
        public async Task CreateBenthosPod(string jobId, string thingId = "")
        {
            await _k8s.CreateBenthosPod(jobId, _namespaceName, thingId);
        }

        /// <summary>
        /// Modifies a Benthos Pod using the provided configMap.
        /// </summary>
        /// <param name="id">The identifier of the Pod.</param>
        /// <param name="configMap">The new Kubernetes configMap of the Pod.</param>
        /// <returns></returns>
        public async Task ModifyBenthosPod(string id, V1ConfigMap configMap)
        {
            await _k8s.ModifyBenthosPod(id, configMap, _namespaceName);
        }

        /// <summary>
        /// It restarts a Pod by recreating it with the same ConfigMap.
        /// </summary>
        /// <param name="jobId">The identifier of the job.</param>
        /// <param name="connector">OPTIONAL. if the pod is a Connector. By default: false.</param>
        /// <returns></returns>
        public async Task RestartPod(string jobId, bool connector = false)
        {
            var pod = await _k8s.GetBenthosPod(jobId, _namespaceName);
            if(connector && !await IsPodAConnector(jobId))
                throw new Exception("The pod in question is not a Connector.");
            await _k8s.RestartBenthosPod(pod, _namespaceName, connector);
        }

        public async Task<bool> IsPodAConnector(string jobId)
        {
            if(!await ExistsPod(jobId))
                throw new KeyNotFoundException("The pod does not exist");
            return await _k8s.IsPodAConnector(jobId, _namespaceName);
        }

        /// <summary>
        /// Creates a Connector (Pod + Thing).
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        public async Task CreateConnector(string thingId, JsonNode thingDescription, string namespaceName)
        {
            //create or modify the Thing
            await CreateThing(thingDescription);
            V1ConfigMap? configMap = null;
            
            var jobId = BenthosConfigParser.GetJobIdFromThingId(thingId);
            try
            {
                try
                {
                    configMap = await GetConnectorConfigFromThingDescription(thingId, thingDescription);
                    await _k8s.CreateConfigMap(configMap, namespaceName);
                }
                catch (ArgumentNullException)
                {
                    throw;
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Something went wrong while obtaining the configMap: {ex.Message}");
                }
                try
                {
                    await _k8s.CreateBenthosPod(jobId, namespaceName, thingId);
                }catch(Exception ex)
                {
                    throw new Exception($"Something went wrong while creating the Connector: {ex.Message}");
                }
            }
            catch (Exception)
            {
                //the k8s leftovers are managed inside pod method.
                await DeleteThing(thingId);
                throw;
            }
        }

        /// <summary>
        /// Creates a Connector (Pod + Thing).
        /// </summary>
        /// <param name="thingId">The identifier of the Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        public async Task CreateConnector(string thingId, JsonNode thingDescription)
        {
            await CreateConnector(thingId, thingDescription, _namespaceName);
        }

        /// <summary>
        /// Modifies a Connector (Pod + Thing)
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="thingDescription">The ThingDescription of the Connector Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        public async Task ModifyConnector(string thingId, JsonNode thingDescription, string namespaceName)
        {
            var priorThing = await GetThing(thingId);
            var configMap = await GetConnectorConfigFromThingDescription(thingId, thingDescription);
            try{
                await CreateThing(thingId, thingDescription);
                await _k8s.ModifyBenthosPod(thingId, configMap, namespaceName);
            }
            catch (Exception)
            {
                await CreateThing(thingId, priorThing);
                throw;
            }
        }

        /// <summary>
        /// Modifies a Connector (Pod + Thing)
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="thingDescription">The ThingDescription of the Connector Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        public async Task ModifyConnector(string thingId, JsonNode thingDescription)
        {
            await ModifyConnector(thingId, thingDescription, _namespaceName);
        }

        /// <summary>
        /// Deletes a ConfigMap from the specified Kubernetes Namespace.
        /// </summary>
        /// <param name="configId">The identifier of the Kubernetes ConfigMap.</param>
        /// <returns></returns>
        public async Task DeleteConfigMap(string configId)
        {
            await _k8s.DeleteConfigMap(configId, _namespaceName);
        }

        /// <summary>
        /// Deletes a Benthos Pod or Connector (if specified).
        /// </summary>
        /// <param name="jobId">The identifier of the job.</param>
        /// <param name="thingId">OPTIONAL. The identifier of the Connector Thing.</param>
        /// <returns></returns>
        
        public async Task DeleteBenthosJob(string jobId)
        {
            await _k8s.DeleteBenthosJob(jobId, _namespaceName);
        }

        /// <summary>
        /// Deletes a Connector (Pod + Thing).
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">Thrown if no Thing with such identifier could be found.</exception>
        /// <exception cref="Exception">Thrown if something went wrong while the deletion of either the Thing or the Pod.</exception>
        public async Task DeleteConnector(string thingId, string namespaceName)
        {
            var thing = await GetThing(thingId) ?? throw new KeyNotFoundException("There is no Thing with such identifier");

            try
            {
                await DeleteThing(thingId);
            }catch(Exception ex)
            {
                throw new Exception($"Something went wrong wile deleting the Connector Thing: {ex.Message}");
            }

            // var jobId = BenthosConfigParser.GetJobIdFromThingId(thingId);
            try
            {
                await _k8s.DeleteBenthosJob(thingId, namespaceName, connector:true);
            }catch(Exception ex)
            {
                await CreateThing(thing);
                throw new Exception($"Something went wrong wile deleting in Kubernetes: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a Connector (Pod + Thing).
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">Thrown if no Thing with such identifier could be found.</exception>
        /// <exception cref="Exception">Thrown if something went wrong while the deletion of either the Thing or the Pod.</exception>
        public async Task DeleteConnector(string thingId)
        {
            await DeleteConnector(thingId, _namespaceName);
        }

        /// <summary>
        /// It restarts a Connector Pod.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <returns></returns>
        public async Task RestartConnector(string thingId)
        {
            await RestartPod(BenthosConfigParser.GetJobIdFromThingId(thingId), connector: true);
        }

        /// <summary>
        /// Parses a Thing Description Json into a benthos configMap.
        /// </summary>
        /// <param name="thingDescription">The Thing Description Json.</param>
        /// <param name="id">The identifier of the Pod.</param>
        /// <returns>Returns the Parsed ConfigMap.</returns>
        public V1ConfigMap ParseThingDescriptionIntoBenthosConfig(JsonNode thingDescription, string id, string input)
        {
            return ParseThingDescriptionIntoBenthosConfig(thingDescription, _namespaceName, id, input);
        }

        /// <summary>
        /// Parses a Thing Description Json into a benthos configMap.
        /// </summary>
        /// <param name="thingDescription">The Thing Description Json.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <param name="id">The identifier of the Pod.</param>
        /// <returns>Returns the Parsed ConfigMap.</returns>
        public V1ConfigMap ParseThingDescriptionIntoBenthosConfig(JsonNode thingDescription, string namespaceName, string id, string input)
        {
            return BenthosConfigParser.ParseBenthosConfig(thingDescription, namespaceName, KubernetesService.ToK8sLabelValue(id), input);
        }

        /// <summary>
        /// Obtains the default Kafka output for Benthos Configuration.  
        /// </summary>
        /// <param name="path">The file's path.</param>
        /// <returns>
        /// Returns the Kafka output configuration in plain text stored.
        /// </returns>
        public async Task<string> GetKafkaOutput(string path)
        {
            return await System.IO.File.ReadAllTextAsync(path);
        }

        /// <summary>
        /// Merged Kafka output configuration into an existing configMap.
        /// </summary>
        /// <param name="outputYaml">Kafka output configuration in plain text.</param>
        /// <param name="configMap">The original configMap.</param>
        /// <returns>Returns the modified configMap.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the Kafka plain text does not contain an output configuration.</exception>
        
        public async Task<V1ConfigMap> MergeOutputYamlIntoConfig(string outputYaml, V1ConfigMap configMap)
        {
            return MergeOutputYamlIntoConfig(outputYaml, configMap, _namespaceName);
        }

        /// <summary>
        /// Merged Kafka output configuration into an existing configMap.
        /// </summary>
        /// <param name="outputYaml">Kafka output configuration in plain text.</param>
        /// <param name="configMap">The original configMap.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns the modified configMap.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the Kafka plain text does not contain an output configuration.</exception>
        public V1ConfigMap MergeOutputYamlIntoConfig(string outputYaml, V1ConfigMap configMap, string namespaceName)
        {
            if (!configMap.Data.TryGetValue("benthos.yaml", out var baseYaml))
            throw new InvalidOperationException("ConfigMap does not contain benthos.yaml");

            // Parse base Benthos YAML
            var baseStream = new YamlStream();
            baseStream.Load(new StringReader(baseYaml));

            var baseRoot = (YamlMappingNode)baseStream.Documents[0].RootNode;

            // Parse output YAML
            var outputStream = new YamlStream();
            outputStream.Load(new StringReader(outputYaml));

            var outputRoot = (YamlMappingNode)outputStream.Documents[0].RootNode;

            if (!outputRoot.Children.TryGetValue("output", out var outputNode))
                throw new InvalidOperationException("Output YAML must contain a top-level 'output' node");

            // Replace or insert output node
            baseRoot.Children[new YamlScalarNode("output")] = outputNode;

            // Serialize merged YAML
            var writer = new StringWriter();
            baseStream.Save(writer, assignAnchors: false);

            configMap.Data["benthos.yaml"] = writer.ToString();

            return configMap;
        }

        /// <summary>
        /// Gets all stored Connectors.
        /// </summary>
        /// <returns>
        /// Returns an Array with all Connector's Thing Desszcription and job identifiers.
        /// </returns>
        public async Task<JsonArray> GetConnectors()
        {
            return await GetConnectors(_namespaceName);
        }

        /// <summary>
        /// Gets all stored Connectors.
        /// </summary>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>
        /// Returns an Array with all Connector's Thing Desszcription and job identifiers.
        /// </returns>
        public async Task<JsonArray> GetConnectors(string namespaceName)
        {
            JsonArray connectors = new JsonArray();

            var pods = await _k8s.GetAllConnectorPods(_namespaceName);

            //now that we have the pods, and can obtain the jobId, get the ThingDescription
            foreach(V1Pod pod in pods.Items){
                // task: get the thingId and the jobId
                try
                {
                    var jobId = _k8s.GetOriginalValue(pod);
                    var td = await GetThing(_k8s.GetOriginalValue(pod, thingId: true));
                    connectors.Add(new JsonObject { ["jobId"] = jobId, ["thingDescription"] = td});
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return connectors;
        }

        /// <summary>
        /// Gets an specific Connector by its Thing identifier.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <returns>Returns both the JsonNode of the Thing Description and the job identifier of the Connector.</returns>
        /// <exception cref="InvalidOperationException">Thrown if there are more tha one Pod with the provided Thing identifier (illegal).</exception>
        /// <exception cref="Exception">Thrown if there is any other issue encountered while obtaining either the job identifier or the Thing Description.</exception>
        
        public async Task<(JsonNode?, string?)> GetConnectorByThingId(string thingId)
        {
            return await GetConnectorByThingId(thingId, _namespaceName);
        }

        /// <summary>
        /// Gets an specific Connector by its Thing identifier.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns both the JsonNode of the Thing Description and the job identifier of the Connector.</returns>
        /// <exception cref="InvalidOperationException">Thrown if there are more tha one Pod with the provided Thing identifier (illegal).</exception>
        /// <exception cref="Exception">Thrown if there is any other issue encountered while obtaining either the job identifier or the Thing Description.</exception>
        public async Task<(JsonNode?, string?)> GetConnectorByThingId(string thingId, string namespaceName)
        {
            var pod = await _k8s.GetConnectorPodByJobId(thingId, _namespaceName);
            var jobId = _k8s.GetOriginalValue(pod);
            var td = await GetThing(thingId);
            return (td, jobId);
        }

        #endregion
    }
}