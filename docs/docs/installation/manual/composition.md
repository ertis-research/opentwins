---
sidebar_position: 2
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Composition (recommended) 

## Prerequisites

Before you begin, ensure you have the following:
- Container manager: Currently tested on [Docker](https://www.docker.com/) and [ContainerD](https://containerd.io/).
- Access to a [Kubernetes](https://kubernetes.io/releases/download/) (recommended) or [K3s](https://k3s.io/) cluster.
- `kubectl` installed and configured.

## Deploy

To provide OpenTwins with digital twin composition capabilities it is necessary to add two components. You can check [architecture](../../overview/architecture.md#compositional-support) to know what is the functionality of each one and how it connects with the rest of the elements.

### Extended API for Eclipse Ditto

This component, developed in NodeJS, has its code available in [its repository](https://github.com/ertis-research/extended-api-for-eclipse-ditto). It has been containerized with Docker and published in [DockerHub](https://hub.docker.com/r/ertis/ditto-extended-api).

To install it in a Kubernetes cluster we will use a deployment and a service, but it will be necessary to previously modify the environment variables containing the Eclipse Ditto IP and its credentials. In addition, to enable the query of all policies, it must also be configured with the IP of the MongoDB instance used by Eclipse Ditto.

These are the environment variables of the component:

| Name | Type | Description |
|----------|----------|----------|
| **HOST** | IP |  Host where the API will be deployed (default: localhost) |
| **PORT**    | int | Port to serve as endpoint for the API (default: 8080) |
| **MONGO_URI_POLICIES** | URI   | MongoDB URI to extract the policies. It must follow the format: _mongodb://IP_MONGODB:PORT_MONGODB/policies_  |
| **DITTO_URI_THINGS** | URI | Eclipse Ditto **nginx** service URI to provide functionality and apply constraints for composition. It must follow the format: _http://IP_DITTO:PORT_DITTO_ |
| **DITTO_USERNAME_API** | text | Eclipse Ditto API user |
| **DITTO_PASSWORD_API** | text | Eclipse Ditto API password |
| **DITTO_USERNAME_DEVOPS** | text | Eclipse Ditto Devops user (user who can create connections) |
| **DITTO_PASSWORD_DEVOPS** | text | Eclipse Ditto Devops password |
| **ALL_LOGS** | boolean | If enabled, the component logs will show the results of all requests it sends to Eclipse Ditto. It is only useful if you need to debug (default: false) |

It is **necessary to set up all the ones that start with DITTO**, although it is highly recommended to set up the MONGO_URI_POLICIES one as well.

With this information, we will create a YAML file for a deployment and a YAML file for a service. All values, including those for credentials, correspond to the default values of Eclipse Ditto for the version indicated in the [essential](./essential.md#eclipse-ditto-v33) part. If you have changed the default values, you may need to modify these. 

```yaml title="deployment.yaml"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: opentwins-ditto-extended-api
spec:
  selector:
    matchLabels:
      app.kubernetes.io/name: opentwins-ditto-extended-api
  replicas: 1
  template:
    metadata:
      labels:
        app.kubernetes.io/name: opentwins-ditto-extended-api
    spec:
      containers:
      - name: ditto-extended-api
        image: ertis/ditto-extended-api:latest
        imagePullPolicy: Always
        ports:
        - containerPort: 8080
          protocol: TCP
        env: 
        - name: HOST 
          value: "localhost" 
        - name: PORT 
          value: "8080" 
        - name: MONGO_URI_POLICIES 
          value: "mongodb://mongodb:27017/policies" 
        - name: DITTO_URI_THINGS 
          value: "http://ditto-nginx:30525" 
        - name: DITTO_USERNAME_API 
          value: "ditto" 
        - name: DITTO_PASSWORD_API 
          value: "ditto" 
        - name: DITTO_USERNAME_DEVOPS 
          value: "devops" 
        - name: DITTO_PASSWORD_DEVOPS 
          value: "foobar"
        - name: ALL_LOGS 
          value: false
```

```yaml title="service.yaml"
apiVersion: v1
kind: Service
metadata:
  name: opentwins-ditto-extended-api
  labels:
    app.kubernetes.io/name: opentwins-ditto-extended-api
spec:
  type: NodePort
  ports:
  - name: http
    nodePort: 30526
    port: 8080
    protocol: TCP
    targetPort: 8080
  selector:
    app.kubernetes.io/name: opentwins-ditto-extended-api
```

To deploy them in the Kubernetes cluster we will use the commands:

```bash
kubectl apply -f deployment.yaml -n opentwins
kubectl apply -f service.yaml -n opentwins
```

**Verify that the component is running correctly** by querying the pod status, which should be Running and Ready 1/1. 


### OpenTwins app plugin for Grafana

The code of this plugin can be found in [his repository](https://github.com/ertis-research/grafana-app-opentwins) and the latest version of the plugin will always be compiled in a zip file as a [release](https://github.com/ertis-research/grafana-app-opentwins/releases/tag/latest).

The installation of the OpenTwins plugin in Grafana will depend on how Grafana was installed and the version you are using. Below, we explain the process for installations done via Helm and for local installations. However, it is highly recommended to check the [official Grafana documentation](https://grafana.com/docs/grafana/latest/administration/plugin-management/#install-grafana-plugins) for detailed instructions on plugin installation, as there may be specific variations depending on your environment or version.

<Tabs groupId="environment">
  <TabItem value="helm" label="Helm" default>

To install the plugin you need to add its compiled code in a folder with the same name as its ID inside the Grafana plugins folder, which is _/var/lib/grafana/plugins_ by default.
To do this using Helm, add an extraInitContainer to your values.yaml, where you navigate to the plugins folder, download the zip of the latest release and unzip it.
Below is what you need to add.

:::danger
Note that some keys may overlap with other keys you already have in your values.yaml, **do not just copy it but mix both YAMLs**
:::

```yaml title=values.yaml
extraInitContainers:
- name: install-opentwins-plugins
  image: busybox
  command:
    - /bin/sh
    - -c
    - |
      #!/bin/sh
      set -euo pipefail
      mkdir -p /grafana-storage/plugins
      cd /grafana-storage/plugins
      wget --no-check-certificate -O ertis-opentwins.zip https://github.com/ertis-research/opentwins-in-grafana/releases/download/latest/ertis-opentwins.zip
      unzip -o ertis-opentwins.zip
      rm ertis-opentwins.zip
  volumeMounts:
  - name: storage
    mountPath: /grafana-storage
``` 

At the moment the plugin is not signed, so you will have to add the plugin id (_ertis-opentwins_) to the list of unsigned plugins, which is also defined inside the values.yaml. This will allow Grafana to show it as an installable plugin (if not, it will not appear at all). Below is what you need to add (note that if you have followed the manual installation of the [essential functionality](./essential.md#grafana-v95) you should already have it configured.).

```yaml title=values.yaml
grafana.ini:
  plugins:
    allow_loading_unsigned_plugins: ertis-opentwins,ertis-unity-panel
``` 

Now update the Grafana helm: 

```bash
helm upgrade grafana grafana/grafana -n opentwins --version 8.5.0 -f values.yaml
```

Verify that the Grafana pod is in Running and Ready status. The OpenTwins plugin should now be available for enabling in the Grafana configuration.

  </TabItem>
  <TabItem value="local" label="Local">

To install the plugin on a local Grafana, you must first [download the zip file](https://github.com/ertis-research/grafana-app-opentwins/releases/download/latest/ertis-opentwins.zip) of the latest plugin release and then access the Grafana folder on your PC. 

In this folder you have to find the [Grafana configuration file](https://grafana.com/docs/grafana/v11.3/setup-grafana/configure-grafana). Follow the [Grafana documentation](https://grafana.com/docs/grafana/v11.3/setup-grafana/configure-grafana/#configuration-file-location) to know its location, the name of the file and how to modify it. When you have it, modify the appropriate file by uncommenting and adding the following:

```ini
[plugins]
# Enter a comma-separated list of plugin identifiers to identify plugins to load even if they are unsigned. Plugins with modified signatures are never loaded.
allow_loading_unsigned_plugins = ertis-opentwins
```

In the same file, check the [path to the plugins folder](https://grafana.com/docs/grafana/v11.3/setup-grafana/configure-grafana/#plugins). You can modify it if you consider it convenient. Then, go to that folder and unzip the plugin zip file. You should get a folder with the name "ertis-opentwins" which must have something like this inside (make sure that there are no intermediate folders).

<center> 
<img
    src={require('./img/opentwins-plugin-content.png').default}
    alt="Kubectl get services"
    style={{ width: 700 }}
/>
</center>

For the changes to take effect, **Grafana must be restarted**. Please refer to [its documentation](https://grafana.com/docs/grafana/v11.3/setup-grafana/start-restart-grafana/) to find out how to do this depending on your operating system. The OpenTwins plugin should now be available for enabling in the Grafana configuration.

  </TabItem>
</Tabs>


## Connect
Now you have to **configure the OpenTwins plugin** in Grafana with the Ditto Extended API and Eclipse Ditto URLs.

### Obtain external URLs

Get the name of the services with `kubectl get services`. The method to obtain the URL may vary depending on the configuration of your cluster. The URL for each service will match the cluster IP and the port that will depend on the [type of service](https://kubernetes.io/docs/concepts/services-networking/service/#publishing-services-service-types) (LoadBalancer or NodePort). For example, if our cluster IP is `192.168.32.25` and uses a NodePort service with the port 30718, the URL for Grafana would be `192.168.32.25:30718`.

<details>
  <summary>Are you using <b>Minikube</b> to deploy OpenTwins?</summary>
  <div>

As Minikube is a local cluster, you **cannot directly use the IP of the cluster**. Therefore, you will have to [expose the services](https://minikube.sigs.k8s.io/docs/handbook/accessing/) that you want to use externally with a command.

Open three terminals, one for each service, and run the following command on each terminal with a different service name. These will return a URL of your localhost with a port that will forward all traffic to the specified service. **These are the URLs you should use.**

```bash
minikube service <service-name> --url
```

  </div>
</details>

### Configure OpenTwins plugin

Access Grafana in any browser with the URL you have obtained. The credentials must match those indicated in the Helm values, which by default are user _admin_ and password _admin_. 

Access the left drop-down menu and select `Administration > Plugins`. Once there, find the _OpenTwins_ plugin and activate it by clicking _Enable_. Then, go to the _Configuration_ tab where you will need to enter the Eclipse Ditto and Extended API URLs in the corresponding fields. Use _ditto_ for both the Eclipse Ditto username and password if you have not changed the credentials. Then click on _Save settings_ to complete the plugin configuration.

<details>
  <summary>Screenshots</summary>
  <div>
    <center>
    <img
      src={require('../img/enable-plugin.png').default}
      alt="Plugin"
      style={{ width: 600 }}
    />
    </center>
    <center>
    <img
      src={require('../img/configuration-interfaz.png').default}
      alt="Configuration"
      style={{ width: 600 }}
    />
    </center>
    <center>
    <img
      src={require('../img/opentwins-access.png').default}
      alt="Configuration"
      style={{ width: 400 }}
    />
    </center>
  </div>
</details>

:::note
If you are using the latest version of the interface, you may find two fields intended for an agent service. This functionality is currently under development and is not yet available, so leave them empty and disregard them for now.
:::

Find the available application in the `App > OpenTwins` section of the left drop-down menu. 

**You have now support for the composition of digital twins.**