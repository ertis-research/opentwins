---
sidebar_position: 4
---

# Simulations

## FMI simulations

:::warning

The FMI simulation service is currently being tested. Please be patient, as soon as it is properly tested, the public image will be available on Docker Hub. If you want to test or use the service, you can find it in the [GitHub repository](https://github.com/ertis-research/opentwins-fmi-2.0).

:::

### Prerequisites

Before you begin, ensure you have the following:
- Access to a Kubernetes cluster
- OpenTwins with the components of the essential functionality (monitoring) already installed by Helm or manually
- `kubectl` installed and configured

### Deploy

You can manually deploy the component by creating a Kubernetes deployment resource and service.

Create a YAML file for the deployment with this content:

```yaml title="deployment.yaml"
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

Next, create a YAML file for the service with this content:

```yaml title="service.yaml"
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

Apply the deployment and service using the following commands:

```bash
kubectl apply -f deployment.yaml -n opentwins
kubectl apply -f service.yaml -n opentwins
```

To **verify that everything is working correctly**, use the following command to check if the new components are running and ready to use.

```bash
kubectl get all -n opentwins
```

### Connect

## Custom simulations

### Prerequisites

### Deploy

### Connect