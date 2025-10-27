<div align="center">
  <img src="https://github.com/ertis-research/opentwins/assets/48439828/74f974ba-3804-46de-9149-2c4fe7702e93" width="80" height="80" />
  <h3>OpenTwinsV2</h3>
  <b>Preliminary Implementation - Paper Validation</b>
</div>

</br>
</br>

> [!WARNING]
> **Archived Version for Validation Purposes**
>
> This repository contains the preliminary implementation of OpenTwins V2 that was used to conduct the **Research Questions** for validation detailed in our academic paper.
>
> This code is **static** and maintained as a snapshot to justify the results presented in the publication. It will not be updated.
>
> For the latest, actively developed version of OpenTwins V2, please see the `v2` branch in the primary **[OpenTwins repository](https://github.com/ertis-research/opentwins)**.

---

## About This Project

OpenTwins V2 is a significant architectural evolution from V1, designed to enhance the **composability** and **scalability** of digital twins (DTs). The V1 architecture is detailed in:

* Robles, J., Martín, C., & Díaz, M. (2023). OpenTwins: An open-source framework for the development of next-gen compositional digital twins. *Computers in Industry*, 152, 104007.

This preliminary version moves away from the V1 dependency on Eclipse Ditto. It instead utilizes a set of microservices developed in **C#** that leverage the **Web of Things (WoT)** standard and **Knowledge Graphs (KGs)** to define, manage, and interact with DTs.

The core of this architecture consists of three services:

* **Things Service**: Manages the WoT. Each "Thing" (representing a physical or virtual asset) is implemented as a **Dapr Actor**. This allows each Thing to have its own encapsulated state and logic.
* **Events Service**: Acts as a high-throughput ingestion point for all incoming data (e.g., from MQTT or Kafka). It is considered a direct dependency of the `Things` service.
    * **Justification**: Receiving high-frequency event streams directly within a Dapr Actor is not recommended. Actors are single-threaded and process messages sequentially. A high-volume stream would create a severe bottleneck, block the actor's mailbox, and prevent any other operations. The `Events` service acts as a scalable, stateless buffer that ingests the stream and then makes controlled calls to the appropriate Thing Actor, decoupling ingestion from state management.
* **Twins Service**: Manages the KGs and ontologies. This service stores the relationships and things that compose each CDT, represented as a KG. It communicates with the `Things` service to retrieve data and build these complex graph-based representations.

For a complete architectural breakdown, conceptual model, and the results of our validation, please refer to our publication.

---

## Prerequisites

Before proceeding, ensure you have the following tools installed and configured:

* A running **Kubernetes (K8s) cluster**.
* **Helm** (v3+)
* `kubectl` configured to your cluster.

---

## Installation

This setup must be deployed manually to your Kubernetes cluster. The following commands assume you are at the root of this repository and that all paths use `/` (Linux/macOS style). Please adjust paths if you are on Windows (`.\kubernetes\...`).

#### 1. Dapr (Service Mesh)

```bash
# Install Dapr
helm upgrade --install dapr dapr/dapr \
 --version=1.15 \
 --namespace dapr-system \
 --create-namespace \
 --set dapr_scheduler.cluster.storageSize=16Gi \
 --set global.mtls.enabled=true \
 --wait

# Install Dapr Dashboard
helm upgrade --install dapr-dashboard dapr/dapr-dashboard --namespace dapr-system --set serviceType=NodePort
```

#### 2. DGraph (Knowledge Graph Database)

```bash
# Add the DGraph Helm repo
helm repo add dgraph [https://charts.dgraph.io](https://charts.dgraph.io)

# Install DGraph using the provided configuration
helm install opentwinsv2-dgraph dgraph/dgraph -f kubernetes/dgraph/values.yaml
```

#### 3. Kafka (Message Broker)

```bash
# !! IMPORTANT: Replace "YOUR_CLUSTER_IP" with your cluster's external IP address
export CLUSTER_IP="YOUR_CLUSTER_IP"

# Install Kafka
helm install kafka oci://registry-1.docker.io/bitnamicharts/kafka -f kubernetes/kafka/values.yaml \
  --set externalAccess.controller.service.domain="$CLUSTER_IP"

# Install Kafka UI
helm install kafka-ui kafka-ui/kafka-ui -f kubernetes/kafka/values-ui.yaml
```

#### 4. Redis (Cache)

```bash
helm install opentwinsv2-redis oci://registry-1.docker.io/bitnamicharts/redis -f kubernetes/redis/values.yaml
```

#### 5. TimescaleDB (Database)

```bash
# Apply the Persistent Volume Claim (PVC)
kubectl apply -f kubernetes/timescaledb/pvc.yaml

# Apply the StatefulSet and Service
kubectl apply -f kubernetes/timescaledb/deploy-svc.yaml
```

> [!NOTE]
> After the TimescaleDB pod is running, you must manually connect to the database and execute the SQL scripts to create the required tables.
> - kubernetes/timescaledb/events-table.sql
> - kubernetes/timescaledb/data-table.sql
> - kubernetes/timescaledb/thing-descriptions-table.sql

#### 6. Mosquitto (MQTT Broker)

```bash
kubectl apply -f kubernetes/mosquitto/mosquitto-deploy.yaml
```

#### 7. Benthos (Stream Processor)

```bash
# Create ConfigMaps for the Benthos pipelines
kubectl create configmap benthos-mqtt-kafka-config --from-file=benthos.yaml=kubernetes/benthos-mqtt-kafka/benthos-config-basic.yaml
kubectl create configmap benthos-kafka-timescaledb-config --from-file=benthos.yaml=kubernetes/benthos-kafka-timescaledb/benthos-config-basic.yaml

# Deploy the Benthos instances
kubectl apply -f kubernetes/benthos-mqtt-kafka/benthos-deploy.yaml
kubectl apply -f kubernetes/benthos-kafka-timescaledb/benthos-deploy.yaml
```

#### 8. OpenTwins V2 Services (C#)

This step deploys the core C# microservices (`Things`, `Twins`, and `Events`). You can run them locally for development or deploy them to your Kubernetes cluster.

##### 8.1. Kubernetes Deployment

You have two options for deploying the services to your cluster.

**Option 1: Deploy Pre-built Images**

This is the simplest method, using images already published to Docker Hub.

```bash
# Apply the deployments and services
kubectl apply -f kubernetes/opentwinsv2-services/things.yaml
kubectl apply -f kubernetes/opentwinsv2-services/twins.yaml
kubectl apply -f kubernetes/opentwinsv2-services/events.yaml
```

**Option 2: Build and Deploy Your Own Images**

Use this option if you have made custom changes to the service code.

1. **Build and Push**: Navigate to the `opentwinsv2-services/src/` directory. From there, build and push the Docker images for each service to your container registry (replace `REGISTRYID` with your registry).
   ```bash
   # Navigate to the directory
   cd opentwinsv2-services/src/

   # Events Service
   docker build -t REGISTRYID/opentwinsv2-events:0.0.1 . -f Dockerfile.Events
   docker push REGISTRYID/opentwinsv2-events:0.0.1
    
   # Things Service
   docker build -t REGISTRYID/opentwinsv2-things:0.0.1 . -f Dockerfile.Things
   docker push REGISTRYID/opentwinsv2-things:0.0.1
    
   # Twins Service
   docker build -t REGISTRYID/opentwinsv2-twins:0.0.1 . -f Dockerfile.Twins
   docker push REGISTRYID/opentwinsv2-twins:0.0.1
   ```
   
2. **Update YAMLs**: Modify the `image`: tag in the following files to point to your new images:
    - `kubernetes/opentwinsv2-services/things.yaml`
    - `kubernetes/opentwinsv2-services/twins.yaml`
    - `kubernetes/opentwinsv2-services/events.yaml`
      
3. **Deploy**: Apply your modified configuration files.
   ```bash
   # Apply the deployments and services
   kubectl apply -f kubernetes/opentwinsv2-services/things.yaml
   kubectl apply -f kubernetes/opentwinsv2-services/twins.yaml
   kubectl apply -f kubernetes/opentwinsv2-services/events.yaml
   ```

##### 8.2. Local (Self-Hosted) Deployment

This method is ideal for development and testing.

1.  **Initialize Dapr Locally**: If you haven't already, [install the Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) and initialize the local environment.
    ```bash
    dapr init
    ```

2.  **Configure Services**: For each service (`events-service`, `things-service`, `twins-service`):

    - Navigate to the project folder (e.g., `opentwinsv2-services/src/Events/`).
    - Copy the `appsettings.Development.example.json` file to `appsettings.Development.json` and fill in the required values (e.g., connection strings).
    - Copy the `Infrastructure/DaprComponents` directory and rename the copy to `Infrastructure/DaprComponentsLocal`.
    - Edit the YAML files within `Infrastructure/DaprComponentsLocal` to point to your local services (e.g., local IPs and ports for Kafka, etc.).

3.  **Run Services**: Open **three separate terminals** and run one command in each to start all services with their Dapr sidecars.

    * **Terminal 1 (Events Service):**
        ```bash
        # Navigate to the project directory
        cd opentwinsv2-services/src/Events/
        
        # Run the service
        dapr run --app-id events-service --app-port 5012 --resources-path ./Infrastructure/DaprComponentsLocal -- dotnet run --urls=http://localhost:5012/
        ```
    * **Terminal 2 (Things Service):**
        ```bash
        # Navigate to the project directory
        cd opentwinsv2-services/src/Things/
        
        # Run the service
        dapr run --app-id things-service --app-port 5001 --resources-path ./Infrastructure/DaprComponentsLocal -- dotnet run --urls=http://localhost:5001/
        ```
    * **Terminal 3 (Twins Service):**
        ```bash
        # Navigate to the project directory
        cd opentwinsv2-services/src/Twins/
        
        # Run the service
        dapr run --app-id twins-service --app-port 5013 --resources-path ./Infrastructure/DaprComponentsLocal -- dotnet run --urls=http://localhost:5013/
        ```

## Running the Validation

Each Research Question (RQ) test suite is contained in its own directory at the root of this project.

#### Running the Tests
To run a specific RQ validation test, navigate to its directory and follow these steps.

```bash
# 1. Navigate to the specific RQ folder
cd RQ1.scalability/

# 2. Create the environment configuration file
#    Copy the example file and fill in your specific values 
#    (e.g., cluster IP, connection strings).
cp .env.example .env
#    Now edit the .env file with your details.

# 3. Create a Python virtual environment
python -m venv venv

# 4. Activate the virtual environment
#    On Linux/macOS:
source venv/bin/activate
#
#    On Windows (Command Prompt/PowerShell):
.\venv\Scripts\activate

# 5. Install the required dependencies
pip install -r requirements.txt

# 6. Run the main validation script
python main.py
```

#### Important Considerations for Accurate Latency Measurement

To ensure correct latency measurement, the following points must be taken into account:

**1. Time Synchronization (NTP)**
   
To ensure that all measured latencies are accurate, all nodes must have their clocks synchronized. This is especially critical if you are running test clients on a separate machine from the K8s cluster.

We strongly recommend configuring a reliable NTP (Network Time Protocol) client, such as [Meinberg NTP](https://www.meinbergglobal.com/english/sw/ntp.htm#ntp_stable), on all participating machines.

**2. Performance: Local (Self-Hosted) vs. Kubernetes**

You may observe significantly slower performance and timeouts (e.g., 15+ seconds) when running the services locally (in Dapr "self-hosted" mode) compared to Kubernetes deployment.

This is not a bug in the services, but a known behavior of Dapr's default service discovery mechanism.

- **Local (Self-Hosted) Mode:** Dapr defaults to mDNS for service discovery. If your local firewall, VPN, or OS configuration (common in VMs or Docker Desktop) interferes with multicast traffic, mDNS resolution can fail or time out, causing extreme delays.
- **Kubernetes Mode:** In Kubernetes, Dapr correctly uses the cluster's robust K8s DNS service. This is a centralized and highly reliable discovery method, which eliminates the mDNS bottleneck. InvokeMethodAsync calls become fast, with latency limited to standard network/sidecar overhead.

## License
This project is licensed under the **Apache 2.0 License**. See the LICENSE file for details.






