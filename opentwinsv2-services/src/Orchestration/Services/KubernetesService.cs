using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using OpenTwinsV2.Orchestration.Formatters;

namespace Orchestration.Services
{
    public class KubernetesService
    {
        private readonly IKubernetes _k8s;
        private readonly IConfiguration _config;

        public KubernetesService(IConfiguration config, IKubernetes k8s)
        {
            _config = config;
            _k8s = k8s;
        }

        #region Encoding

        /// <summary>
        /// Checksif a value is valid for Kubernetes label format restrictions.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>
        /// Returns true if the value is valid for Kubernetes labels.<br/>
        /// Returns false if the value has an invalid format.
        /// </returns>
        public static bool IsValidK8sValue(string value)
        {
            return Regex.IsMatch(
                value,
                @"^[a-z0-9]([-a-z0-9_.]*[a-z0-9])?$"
            );
        }

        /// <summary>
        /// Hashes a value using SHA256.
        /// </summary>
        /// <param name="input">The value to hash.</param>
        /// <returns>Returns the hashed value starting with 'hash-'.</returns>
        public static string ToHashedK8sValue(string input)
        {
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

            // 32 hex chars = 128 bits → plenty, fits easily
            var hash = Convert.ToHexString(hashBytes)
                .ToLowerInvariant()
                .Substring(0, 32);

            return $"hash-{hash}";
        }

        /// <summary>
        /// Formats a value into a Kubernetes label format friendly one.
        /// </summary>
        /// <param name="input">The value to format.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if the value is empty or null.</exception>
        public static string ToK8sLabelValue(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be null or empty", nameof(input));

            var readable = input
                .ToLowerInvariant()
                .Replace("-", "--dash--")
                .Replace(":", "--colon--")
                .Replace("/", "--slash--")
                .Replace("@", "--at--");

            if (IsValidK8sValue(readable) && readable.Length <= 63)
                return readable;

            //if the valid value surpasses the character limit for labels and names in k8s pods, then hash it
            return ToHashedK8sValue(input);
        }

        /// <summary>
        /// Formats back a Kubernetes label friendly format value into its original value.
        /// </summary>
        /// <param name="encoded">The Kubernetes friendly format value.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown if the value is empty or null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the value is hashed.</exception>
        public static string FromK8sLabelValue(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded))
                throw new ArgumentException("Encoded value cannot be null or empty", nameof(encoded));

            // If this is a hashed value, it is intentionally NOT reversible
            if (encoded.StartsWith("hash-"))

                //TODO: Catch Exception and look into annotations

                throw new InvalidOperationException(
                    "Hashed Kubernetes values cannot be decoded. Original value must be stored elsewhere (e.g., annotations)."
                );

            return encoded
                .Replace("--at--", "@")
                .Replace("--slash--", "/")
                .Replace("--colon--", ":")
                .Replace("--dash--", "-");
        }

        /// <summary>
        /// Formats the logs of a pod.
        /// </summary>
        /// <param name="jobId">The identifier of the job of the Pod.</param>
        /// <param name="namespaceName">The Kuebrnetes namespace name.</param>
        /// <returns>Returns the logs in a single string.</returns>
        private async Task<string> FormatPodLogs(string jobId, string namespaceName)
        {
            var logsStream = await _k8s.CoreV1.ReadNamespacedPodLogAsync(jobId, namespaceName);
            var logLines = new List<string>();

            using (var reader = new StreamReader(logsStream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null) logLines.Add(line);
                }
            }

            // join the logs by \n
            return string.Join(Environment.NewLine, logLines);
        }

        /// <summary>
        /// Gets the original value of a jobId or thingId of a Pod.
        /// </summary>
        /// <param name="pod">The Kubernetes Pod.</param>
        /// <param name="thingId">OPTIONAL. If instead of the job identifier we are looking for the Thing identifier.</param>
        /// <returns>Returns the original value (by default, job identifier) stored in the Pod's annotations.</returns>
        /// <exception cref="ArgumentException">Thrown if the Pod does not have job-id or thing-id (if specified) in labels or original-job-id or original-thing-id (if specified) in annotations.</exception>
        public string GetOriginalValue(V1Pod pod, bool thingId = false)
        {
            if (!pod.Metadata.Labels.ContainsKey(thingId ? "thing-id" : "job-id"))
                throw new ArgumentException($"The pod provided does not have {(thingId ? "thing-id" : "job-id")} key in labels");
            if (!pod.Metadata.Annotations.ContainsKey(thingId ? "original-thing-id" : "original-job-id"))
                throw new ArgumentException($"The pod provided does not have {(thingId ? "original-thing-id" : "original-job-id")} key in annotations");
            return pod.Metadata.Annotations[thingId ? "original-thing-id" : "original-job-id"];
        }

        #endregion

        #region Namespace

        /// <summary>
        /// Checks if a Kubernetes namespace exists.
        /// </summary>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>
        /// Returns true if the namespace exists.<br/>
        /// Returns false if the namespace does not exist.
        /// </returns>
        public async Task<bool> ExistsNamespace(string namespaceName)
        {
            try
            {
                await _k8s.CoreV1.ReadNamespaceAsync(namespaceName);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task CreateNamespace(string namespaceName)
        {
            if(await ExistsNamespace(namespaceName))
                throw new InvalidOperationException($"There is already a namespace called {namespaceName}");
            
            var nsObj = new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = namespaceName
                }
            };
            await _k8s.CoreV1.CreateNamespaceAsync(nsObj);
        }

        #endregion

        #region ConfigMap

         /// <summary>
        /// Checks if exists the configMap with name generated from given identifier in the namespace.
        /// </summary>
        /// <param name="id">The identifier of the Pod.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>
        /// Returns true if the configMap exists.<br/>
        /// Returns false if the configMap does not exist.
        /// </returns>
        public async Task<bool> ExistsConfigMap(string id, string namespaceName)
        {
            try
            {
                await _k8s.CoreV1.ReadNamespacedConfigMapAsync(BenthosConfigParser.GetConfigName(id), namespaceName);
                return true;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the Kubernetes ConfigMap with the provided identifier.
        /// </summary>
        /// <param name="configId">The identifier of the Kubernetes ConfigMap.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns the specific ConfigMap.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no ConfigMap with such identifier was found.</exception>
        public async Task<V1ConfigMap> GetConfigMap(string configId, string namespaceName)
        {
            try
            {
                var configMap = await _k8s.CoreV1.ReadNamespacedConfigMapAsync(configId, namespaceName);
                return configMap;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException();
            }
        }

        /// <summary>
        /// Creates the ConfigMap in Kubernetes.
        /// </summary>
        /// <param name="configMap">The Kubernetes ConfigMap to create.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        public async Task CreateConfigMap(V1ConfigMap configMap, string namespaceName)
        {
            await _k8s.CoreV1.CreateNamespacedConfigMapAsync(configMap, namespaceName);
        }

        /// <summary>
        /// It replaces the Kubernetes ConfigMap with the provided identifier with the new one. 
        /// </summary>
        /// <param name="configId">The identifier of the Kubernetes ConfigMap.</param>
        /// <param name="newConfig">The new ConfigMap.</param>
        /// <param name="namespaceName">The Kubenetes namespace name.</param>
        /// <returns></returns>
        public async Task ChangeConfigMap(string configId, V1ConfigMap newConfig, string namespaceName)
        {
            //the pod has a flag that watches for changes in the config and updates its volume when it detects changes.
            await _k8s.CoreV1.ReplaceNamespacedConfigMapAsync(newConfig, configId, namespaceName);
        }

        /// <summary>
        /// Deletes a ConfigMap from the specified Kubernetes Namespace.
        /// </summary>
        /// <param name="configId">The identifier of the Kubernetes ConfigMap.</param>
        /// <param name="namespaceName">The namespace name.</param>
        /// <returns></returns>
        public async Task DeleteConfigMap(string configId, string namespaceName)
        {
            var deleteOptions = new V1DeleteOptions
            {
                PropagationPolicy = "Foreground"
            };
            await _k8s.CoreV1.DeleteNamespacedConfigMapAsync(configId, namespaceName, body: deleteOptions);
        }

        #endregion

        #region Health Check 

        /// <summary>
        /// Checks that the provided Pod reached a successful state after creation.
        /// </summary>
        /// <param name="pod">The Kubernetes Pod object.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown if the pod has not reached a successful state after timeout or reaached a failed state before that.</exception>
        public async Task CheckPodStatus(V1Pod pod, string namespaceName)
        {
            await WaitForPodReadyAsync(pod.Metadata.Name, namespaceName, TimeSpan.FromSeconds(90)); //90s timeout
            var createdPod = await _k8s.CoreV1.ReadNamespacedPodAsync(pod.Metadata.Name, namespaceName);
            if(createdPod.Status.Phase != "Running")
                throw new Exception($"The pod has been created but is not Running, instead: {createdPod.Status.Phase}");
        }

        /// <summary>
        /// Checks that the Container of the provieded Pod reached a successfull state after initialization.
        /// </summary>
        /// <param name="pod">The Kubernetes Pod object.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown if the Pod's Container is stuck at Waiting status or reached Terminated status.</exception>
        public async Task CheckContainerStatus(V1Pod pod, string namespaceName)
        {
            var createdPod = await _k8s.CoreV1.ReadNamespacedPodAsync(pod.Metadata.Name, namespaceName);
            var container = createdPod.Status.ContainerStatuses?.FirstOrDefault();
            if (container?.State?.Waiting != null)
                throw new Exception($"The pod is running but the container is Waiting:\n{container.State.Waiting.Reason}: {container.State.Waiting.Message}");
            if (container?.State?.Terminated != null)
                throw new Exception($"The pod is running but the container terminated unexpectedly:\n{container.State.Terminated.ExitCode}: {container.State.Terminated.Reason}");
        }

        #endregion

        #region Pods

        /// <summary>
        /// Checks if a Pod or Connector (if specified) exists with a given job identifier.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="namespaceName">The Kubernetes namespace name</param>
        /// <param name="connector">OPTIONAL. Wether we are looking for a Connector or not. By defualt: false.</param>
        /// <returns>
        /// Returns true if the Pod or Connector exists.<br/>
        /// Returns false if the Pod or Connector does not exist.
        /// </returns>
        public async Task<bool> ExistsPod(string jobId, string namespaceName, bool connector = false)
        {
            return (await _k8s.CoreV1.ListNamespacedPodAsync(namespaceParameter: namespaceName, labelSelector: $"app=benthos-worker,{(connector ? "connector=true," : "")}job-id={ToK8sLabelValue(jobId)}")).Items.Count>0;
        }

        /// <summary>
        /// Does a full check to the provided Pod and its Container to ensure successful states.
        /// </summary>
        /// <param name="pod">The Kubernetes Pod object.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>
        /// Returns true if both the Pod and its Container reached successful states.<br/>
        /// Returns false if either the Pod or its Container didn't reach a successful state.
        /// </returns>
        public async Task<bool> IsPodHealthy(V1Pod pod, string namespaceName)
        {
            //checks and waits for Ready status with timeout
            try
            {
                if(pod.Status.Phase != "Running")
                {
                    await CheckPodStatus(pod, namespaceName);
                }
                await CheckContainerStatus(pod, namespaceName);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{pod.Metadata.Name} pod is not healthy.\nReason: {ex.Message}");
                return false;
            }

        }

        /// <summary>
        /// Gets the PodList of Benthos Pods in the provided Kubernetes namespace.
        /// </summary>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns the PodList object.</returns>
        public async Task<V1PodList> GetAllBenthosPods(string namespaceName)
        {
            var pods = await _k8s.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: $"app=benthos-worker");
            return pods;
        }

        /// <summary>
        /// Gets a Kubernetes Benthos Pod by its job identifier.
        /// </summary>
        /// <param name="jobId">The identifier of the job.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns the Pod with the provided job identifier.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if there is no Pod with such job identifier in the namespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown if there is more than one Pod with the same job identifier.</exception>
        public async Task<V1Pod> GetBenthosPod(string jobId, string namespaceName)
        {
            var pods = await _k8s.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: $"app=benthos-worker,job-id={ToK8sLabelValue(jobId)}");
            if(pods.Items.Count==0)
                throw new KeyNotFoundException($"There are no pods with the jobId {jobId}");
            if(pods.Items.Count>1)
                throw new InvalidOperationException($"There are two or more pods with the jobId {jobId}");
            return pods.Items.Single();
        }

        /// <summary>
        /// Checks if a Pod is a Connector Pod.
        /// </summary>
        /// <param name="jobId">The identifier of the job.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>
        /// Returns true if the Pod is a Connector.<br/>
        /// Returns false if the Pod is not a Connector.
        /// </returns>
        public async Task<bool> IsPodAConnector(string jobId, string namespaceName)
        {
            var pod = await GetBenthosPod(jobId, namespaceName);
            return pod.Metadata.Labels.ContainsKey("connector") && pod.Metadata.Labels["connector"]=="true" && pod.Metadata.Labels.ContainsKey("thingId");
        }

        /// <summary>
        /// Waits for the pod to be running after it's been created.
        /// </summary>
        /// <param name="podName">The name of the pod, commonly the identifier of the job (safe format).</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <param name="timeout">The maximum amount of time to await for the pod's creation before failing.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown if the pod is created with Failed Status.</exception>
        /// <exception cref="TimeoutException">Thrown if the pod hasn't reached a Ready Status before the specified timeout runs out.</exception>
        private async Task WaitForPodReadyAsync(string podName, string namespaceName, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                var pod = await _k8s.CoreV1.ReadNamespacedPodAsync(podName, namespaceName);

                if (pod.Status.Conditions?.Any(c =>
                        c.Type == "Ready" && c.Status == "True") == true)
                {
                    return;
                }

                if (pod.Status.Phase == "Failed")
                {
                    throw new Exception("Pod failed to start");
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            throw new TimeoutException("Pod did not become ready in time");
        }

        /// <summary>
        /// Creates a Benthos Pod.
        /// </summary>
        /// <param name="jobId">The identifier of the job.</>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <param name="thingId">OPTIONAL. The identifier of the Connector Thing.</param>
        /// <param name="deleteConfigInFailure">OPTIONAL. If the configMap should be deleted in case of failure creating the pod. By default: true</param>
        /// <returns></returns>
        public async Task CreateBenthosPod(string jobId, string namespaceName, string thingId = "", bool deleteConfigOnFailure = true)
        {
            // if(!string.IsNullOrWhiteSpace(thingId))
            //     await CreateThing(thingId);
            var safeJobId = ToK8sLabelValue(jobId);
            V1Pod pod;
            try
            {
                var image = _config["BenthosWorker:Image"] ?? "jeffail/benthos:latest";
                var pullPolicy = _config["BenthosWorker:PullPolicy"] ?? "IfNotPresent";
                var secretName = _config["BenthosWorker:PullSecret"]; // Might be null
                var labels = new Dictionary<string, string> { { "app", "benthos-worker" }, { "job-id", safeJobId }};
                var safeThingId = !string.IsNullOrWhiteSpace(thingId) ? ToK8sLabelValue( thingId) : "";

                var annotations = new Dictionary<string, string> {{"original-job-id", jobId}};
                if (!string.IsNullOrWhiteSpace(thingId))
                {
                    annotations.Add("original-thing-id", thingId);
                    labels.Add("thing-id", safeThingId);
                    labels.Add("connector", "true");
                }


                pod = new V1Pod
                {
                    Metadata = new V1ObjectMeta 
                    {
                        Name = safeJobId,
                        Labels = labels,
                        NamespaceProperty = namespaceName,
                        Annotations = annotations
                    },
                    Spec = new V1PodSpec
                    {
                        RestartPolicy = "Never",
                        ImagePullSecrets = !string.IsNullOrEmpty(secretName) 
                            ? new List<V1LocalObjectReference> { new V1LocalObjectReference{Name = secretName} }
                            : null,
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name= "benthos",
                                Image = image,
                                ImagePullPolicy = pullPolicy,
                                Args = new List<string> {"-c", $"/{BenthosConfigParser.GetConfigName(!string.IsNullOrWhiteSpace(thingId) ? thingId : jobId)}/benthos.yaml", "-w"}, //-w flag makes it so that the pod watches for changes in the configMap and reloads itself when it happens
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new V1VolumeMount
                                    {
                                        Name = "config-volume",
                                        MountPath = $"/{BenthosConfigParser.GetConfigName(!string.IsNullOrWhiteSpace(thingId) ? thingId : jobId)}"
                                    }
                                }
                            }
                        },
                        Volumes = new List<V1Volume>
                        {
                            new V1Volume
                            {
                                Name = "config-volume",
                                ConfigMap = new V1ConfigMapVolumeSource
                                {
                                    Name = BenthosConfigParser.GetConfigName(!string.IsNullOrWhiteSpace(thingId) ? thingId : jobId)
                                }
                            }
                        }
                    }
                };

                if(pod is null)
                    throw new Exception("New pod is null");
            }
            catch (Exception)
            {
                try
                {
                    if(deleteConfigOnFailure)
                        await DeleteConfigMap(BenthosConfigParser.GetConfigName(!string.IsNullOrWhiteSpace(thingId) ? thingId : jobId), namespaceName);

                }catch{}
                throw;
            }

            try
            {
                await _k8s.CoreV1.CreateNamespacedPodAsync(pod, namespaceName);
            }catch(Exception)
            {
                try
                {
                    if(deleteConfigOnFailure)
                        await DeleteConfigMap(BenthosConfigParser.GetConfigName(!string.IsNullOrWhiteSpace(thingId) ? thingId : jobId), namespaceName);
                }catch{}
                throw;
            }

            //Check the status after it's been created, the config may not be valid and can be stuck at error status
            try
            {
                await CheckPodStatus(pod, namespaceName);
            }
            catch (Exception ex)
            {
                var logs = FormatPodLogs(safeJobId, namespaceName);
                await DeleteBenthosJob(jobId, namespaceName, deleteConfig: deleteConfigOnFailure);
                throw new Exception($"Original: {ex.Message}\nLogs right before failure and deletion: {logs}");
            }

            try
            {
                await CheckContainerStatus(pod, namespaceName);
            }
            catch (Exception)
            {
                await DeleteBenthosJob(jobId, namespaceName, deleteConfig: deleteConfigOnFailure);
                throw;
            }
        }

        /// <summary>
        /// Modifies a Benthos Pod using the provided configMap.
        /// </summary>
        /// <param name="id">The identifier of the Pod.</param>
        /// <param name="configMap">The new Kubernetes configMap of the Pod.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns></returns>
        public async Task ModifyBenthosPod(string id, V1ConfigMap configMap, string namespaceName)
        {
            //it only modifies the configMap, not the pod
            var configId = BenthosConfigParser.GetConfigName(id);
            var priorConfig = await GetConfigMap(configId, namespaceName);

            try
            {
                await ChangeConfigMap(configId, configMap, namespaceName);
            }
            catch (Exception)
            {
                await ChangeConfigMap(configId, priorConfig, namespaceName);
                throw;
            }
        }

        /// <summary>
        /// Deletes a Benthos Pod or Connector (if specified).
        /// </summary>
        /// <param name="jobId">The identifier of the job.</param>
        /// <param name="namespaceName">The Kubernetes namespace name</param>
        /// <param name="deleteConfig">OPTIONAL. If the configMap should be deleted as well. By default: true</param>
        /// <param name="thingId">OPTIONAL. The identifier of the Connector Thing.</param>
        /// <param name="connector">OPTIONAL. If the Pod to delete is a Connector. By default: false.</param>
        /// <returns></returns>
        public async Task DeleteBenthosJob(string jobId, string namespaceName, bool deleteConfig = true, bool connector = false)
        {
            
            var safeJobId = connector ? ToK8sLabelValue(BenthosConfigParser.GetJobIdFromThingId(jobId)) : ToK8sLabelValue(jobId);
            try
            {
                await _k8s.CoreV1.DeleteNamespacedPodAsync(safeJobId, namespaceName);
                if(deleteConfig && await ExistsConfigMap(jobId, namespaceName))
                    await DeleteConfigMap(BenthosConfigParser.GetConfigName(jobId), namespaceName);
            }catch(Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// It restarts a Pod by recreating it with the same ConfigMap.
        /// </summary>
        /// <param name="pod">The Kubernetes Pod object to restart.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <param name="connector">OPTIONAL. If the pod to restart is a Connector. By default: false.</param>
        /// <returns></returns>
        public async Task RestartBenthosPod(V1Pod pod, string namespaceName, bool connector = false)
        {
            //Restart the pod

            //1 - Look what configMap it has in its volumes

            //check if it's a connector -> obtain thing id
            //otherwise, only the jobId
            var thingId = "";
            if(connector)
                thingId = GetOriginalValue(pod, connector);
            var jobId = connector ? GetOriginalValue(pod, false) : thingId;            

            //2 - Delete the Pod, but not the configMap (mark false flag)
            await DeleteBenthosJob(jobId, namespaceName, deleteConfig: false);

            //3 - Create the pod again with same data
            try
            {
                await CreateBenthosPod(jobId, namespaceName, thingId: thingId, deleteConfigOnFailure: false);
            }catch(Exception ex)
            {
                Console.WriteLine($"WARNING. The restart of the pod failed, There's residual configMap and Connector Thing remaining.\nError: {ex.Message}");
            }
        }

        #endregion

        #region Connector Pods

        /// <summary>
        /// gets a Connector Benthos Pod by its Thing identifier.
        /// </summary>
        /// <param name="thingId">The identifier of the Connector Thing.</param>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns the Connector Pod.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no Pods with such Thing identifier was found.</exception>
        /// <exception cref="InvalidOperationException">Thrown if there is more than one Pod with the same Thing identifier.</exception>
        public async Task<V1Pod> GetConnectorPodByJobId(string thingId, string namespaceName)
        {
            var pods = await _k8s.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: $"app=benthos-worker,connector=true,thing-id={ToK8sLabelValue(thingId)}");
            if(pods.Items.Count==0)
                throw new KeyNotFoundException($"There are no pods with the thingId {thingId}");
            if(pods.Items.Count>1)
                throw new InvalidOperationException($"There are two or more pods with the thingId {thingId}");
            return pods.Items.Single();
        }

        /// <summary>
        /// Gets all Connector Benthos Pods.
        /// </summary>
        /// <param name="namespaceName">The Kubernetes namespace name.</param>
        /// <returns>Returns the list of Connector Pods.</returns>
        public async Task<(int, List<V1Pod>)>GetAllConnectorPods(string namespaceName, int offset, int pageSize, string filter)
        {
            var pods = await _k8s.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: $"app=benthos-worker,connector=true");
            
            var filteredPods = pods.Items
                .Where(pod => pod.Metadata.Annotations.ContainsKey("original-thing-id") && pod.Metadata.Annotations["original-thing-id"].Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            return (filteredPods.Count, filteredPods.Skip(offset).Take(pageSize).ToList());
        }

        #endregion
    }
}