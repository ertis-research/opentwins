---
sidebar_position: 1
---

# Installation Guide

:::warning

The FMI simulation service is currently being tested, so please be patient, as soon as it is properly tested, the public image will be available on Docker Hub.

:::


This guide explains how to install the component using two methods:
1. **Helm** (Work in Progress)
2. **Manual Installation** using Kubernetes manifests (Deployment and Service)

## Prerequisites
This guide asumes that you have OpenTwins already installed.

Before you begin, ensure you have the following:
- Access to a Kubernetes cluster
- `kubectl` installed and configured
- [Helm](https://helm.sh/docs/intro/install/) (for Helm installation)

---

## Method 1: Helm Installation (WIP)
:::warning

This method is currently a Work in Progress (WIP) and may not be fully functional yet. We recomend using manual installation.

:::

1. **Add the Helm repository** (once available):
    ```bash
    helm repo add ertis https://ertis-research.github.io/Helm-charts/
    ```

2. **Update Helm repositories**:
    ```bash
    helm repo update
    ```

3. **Install the component**:
    ```bash
    helm install <release-name> <chart-name> --namespace <namespace>
    ```

For additional configuration options, refer to the [Helm documentation](https://helm.sh/docs/).

---

## Method 2: Manual Installation
You can manually deploy the component by creating a Kubernetes Deployment resource and a Service.


### Step 1: Deploy the Kubernetes Deployment
Create a YAML file for the Deployment (e.g., `deployment.yaml`):

```yaml

apiVersion: apps/v1
kind: Deployment
metadata:
  labels:
    name: opentwins-fmi-api
  name: opentwins-fmi-api
spec:
  replicas: 1
  selector:
    matchLabels:
      name: pod-opentwins-fmi-api
  template:
    metadata:
      labels:
        name: pod-opentwins-fmi-api
      name: opentwins-fmi-api
    spec:
      serviceAccountName: ot-agents
      automountServiceAccountToken: true
      containers:
        - image: ertis/opentwins-fmi-simulator-api-v2:latest
          name: opentwins-fmi-api
          env:
          - name: KUBE_NAMESPACE
            value: 
          - name: INSIDE_CLUSTER
            value: 
          - name: INFLUXDB_HOST
            value: 
          - name: INFLUXDB_TOKEN
            value: 
          - name: INFLUXDB_DB
            value: 
          - name: MINIO_TOKEN
            value: 
          - name: MINIO_URL
            value: 
          - name: MINIO_A_KEY
            value: 
          - name: MINIO_S_KEY
            value: 
          - name: POSTGRE_HOST
            value: 
          - name: POSTGRE_PORT
            value: 
          - name: POSTGRE_DB
            value: 
          - name: POSTGRE_USER
            value: 
          - name: POSTGRE_PASSWORD
            value: 
          - name: BROKER_TYPE
            value: 
          - name: BROKER_IP
            value: 
          - name: BROKER_PORT
            value: 
          - name: BROKER_TOPIC
            value: 
          - name: BROKER_USERNAME
            value: 
          - name: BROKER_PASSWORD
            value: 
          ports:
            - containerPort: 8001
          imagePullPolicy: Always
```

Apply the Deployment using the following command:

```bash
kubectl apply -f deployment.yaml -n <namespace>
```

### Step 2: Create a Kubernetes Service
Next, create a YAML file for the Service (e.g., `service.yaml`):

```yaml
apiVersion: v1
kind: Service
metadata:
  name: opentwins-fmi-api
spec:
  selector:
    name: pod-opentwins-fmi-api
  type: NodePort
  ports:
  - protocol: "TCP"
    port: 8000
    nodePort: <PORT>
    targetPort: 8000

```

Apply the Service configuration using the following command:

```bash
kubectl apply -f service.yaml -n <namespace>
```

### Step 3: Verify the installation

After deploying both the deployment and the service, veryfy that everything is running correctly:

```bash
kubectl get deployments -n <namespace>
kubectl get services -n <namespace>
```

You should see your Deployment and Service listed, and the component should be ready for use.