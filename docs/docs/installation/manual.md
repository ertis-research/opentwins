---
sidebar_position: 3
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Manual

:::warning

The documentation of this method is being written right now. We recommend using [helm installation](../using-helm.mdx).

:::

This section will explain how to deploy the platform manually. Basically, you will have to deploy or install the different components and then connect them. The procedure explained below is the one followed to deploy them in **Kubernetes** using in most cases the **Helm** option, but any other installation in which all the components are correctly installed and there is some kind of network between them to be able to communicate can be used. 

It is not necessary to deploy all components if not all functionalities are to be used. Check the [architecture](../overview/architecture.md) section to find out which ones are essential and what functionality is covered by each of them.

## Essential functionality

### Deploy

:::tip
Note that the values files have the variables that we recommend for the installation of each Helm Chart, but they **can be extended or modified according to your needs** (please consult the Helm Chart documentation for each component).
:::

We recommend installing all components in the same Kubernetes namespace to make it easier to identify and control them all. In our case the namespace will be _opentwins_.

```bash
kubectl create namespace opentwins
```

We installed all the components with their Helm versions and kept most of the values in their default configuration, except for those that are important for the interconnection of the components. In addition, we configure the services as NodePort to facilitate external access and set a specific port for each one. 

:::warning
Depending on how you have persistence configured in your cluster, you may need to deploy [persistent volumes](https://kubernetes.io/docs/concepts/storage/persistent-volumes/) for MongoDB, InfluxDB and Grafana. The values for MongoDB are shown below, but they all follow the same template.

```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: pv-opentwins-mongodb
spec:
  accessModes:
    - ReadWriteOnce
  capacity:
    storage: 8Gi
  hostPath:
    path: /mnt/opentwins/mongodb
    type: DirectoryOrCreate
```
:::

Listed below are the essential components of the [architecture](../overview/architecture.md) along with their versions used, their Helm values and a link to the repository explaining their installation.

#### MongoDB v6.0

- [App v6.0 documentation](https://www.mongodb.com/docs/v6.0/introduction/)
- [Helm documentation](https://github.com/bitnami/charts/tree/main/bitnami/mongodb)

```bash
helm install mongodb -n opentwins oci://registry-1.docker.io/bitnamicharts/mongodb --version 13.8.3 -f values.yaml
```

```yaml title="values.yaml"
service:
  type: NodePort
  nodePorts:
    mongodb: 30717
persistence:
  enabled: true
volumePermissions:
  enabled: true
auth:
  enabled: false
```

#### Eclipse Ditto v3.3

- [App v3.3 documentation](https://eclipse.dev/ditto/3.3/intro-overview.html)
- [Helm documentation](https://github.com/eclipse-ditto/ditto/tree/master/deployment/helm/ditto)

```bash
helm install --dependency-update -n opentwins ditto oci://registry-1.docker.io/eclipse/ditto --version 3.3.7 --wait -f values.yaml
```

:::warning
- We advise not to modify any authentication configuration due to a bug in Eclipse Ditto that may cause access errors.
- In the following values you have to replace _mongodb-service-name_ by the MongoDB service name
:::

```yaml title="values.yaml"
global:
  hashedBasicAuthUsers: false
  basicAuthUsers:
    ditto:
      user: ditto
      password: ditto
    devops:
      user: devops
      password: foobar
nginx:
  service:
    type: NodePort
    nodePort: 30525
swaggerui:
  enabled: false
dittoui:
  enabled: false
mongodb:
  enabled: false
dbconfig:
  policies:
    uri: 'mongodb://<mongodb-service-name>:27017/ditto'
  things:
    uri: 'mongodb://<mongodb-service-name>:27017/ditto'
  connectivity:
    uri: 'mongodb://<mongodb-service-name>:27017/ditto'
  thingsSearch:
    uri: 'mongodb://<mongodb-service-name>:27017/ditto'
gateway:
  config:
    authentication:
      enablePreAuthentication: true
      devops:
        devopsPassword: foobar
        statusPassword: foobar
```

#### InfluxDB v2

- [App v2 documentation](https://docs.influxdata.com/influxdb/v2/)
- [Helm documentation](https://github.com/influxdata/helm-charts/tree/master/charts/influxdb2)

```bash
helm repo add influxdata https://helm.influxdata.com/
helm repo update
helm install -n opentwins influxdb influxdata/influxdb2 --version 2.1.1 -f values.yaml
```

```yaml title="values.yaml"
persistence:
  enabled: true
service:
  type: NodePort
  nodePort: 30716
image:
  pullPolicy: Always
```

#### Mosquitto v2.0

:::tip
OpenTwins supports the use of Mosquitto and Kafka as intermediaries, but **we recommend using Mosquitto** due to its simpler configuration. Since there is no official Helm chart for Mosquitto, we have created one of our own that works fine, although there is no documentation yet. However, you can install Mosquitto in any of the [available ways](https://mosquitto.org/download/).
:::

- [App documentation](https://mosquitto.org/documentation/)
- [Helm values file](https://github.com/ertis-research/Helm-charts/blob/main/mosquitto/values.yaml)

```bash
helm repo add ertis https://ertis-research.github.io/Helm-charts/
helm repo update
helm install mosquitto ertis/mosquitto -n opentwins --wait --dependency-update -f values.yaml
```

```yaml title="values.yaml"
service:
  type: NodePort
  nodePort: 30511
configuration:
  authentication:
    enabled: false
```

#### Apache Kafka v3.4

- [App v3.4 documentation](https://kafka.apache.org/34/documentation.html)
- [Helm documentation](https://github.com/bitnami/charts/tree/main/bitnami/kafka)

```bash
helm install kafka oci://registry-1.docker.io/bitnamicharts/kafka --version 22.0.0 -f values.yaml
```

```yaml title="values.yaml"
autoCreateTopicsEnable: true
```

#### Grafana v9.5

- [App v9.5.1 documentation](https://grafana.com/docs/grafana/v9.5/)
- [Helm documentation](https://github.com/grafana/helm-charts/tree/main/charts/grafana)

```bash
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update
helm install grafana grafana/grafana -n opentwins --version 6.56.1 -f values.yaml
```

```yaml title="values.yaml"
persistence:
  enabled: true
service:
  type: NodePort
  nodePort: 30718
grafana.ini:
  plugins:
    plugin_admin_enabled: true
    allow_loading_unsigned_plugins: 'ertis-opentwins,ertis-unity-panel'
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
      wget --no-check-certificate -O ertis-unity-panel.zip https://github.com/ertis-research/grafana-panel-unity/releases/download/latest/ertis-unity-panel.zip
      unzip -o ertis-unity-panel.zip
      rm ertis-unity-panel.zip
  volumeMounts:
  - name: storage
    mountPath: /grafana-storage
```

#### Eclipse Hono v2.4

:::warning
This component is completely optional. We maintain support for its connection to OpenTwins, but **we do not recommend its use**. For a large number of devices or messages it increases considerably the latency of the platform.
:::

- [App v2.4 documentation](https://eclipse.dev/hono/docs/2.4/)
- [Helm documentation](https://github.com/eclipse/packages/tree/master/charts/hono)

```bash
helm repo add eclipse-iot https://eclipse.org/packages/charts
helm repo update
helm install hono eclipse-iot/hono -n opentwins -f values.yaml --version=2.5.5
```

```yaml title="values.yaml"
prometheus:
  createInstance: false
grafana:
  enabled: false
useLoadBalancer: false
probes:
  livenessProbe:
    initialDelaySeconds: 900
  readinessProbe:
    initialDelaySeconds: 45
messagingNetworkTypes:
  - amqp
kafkaMessagingClusterExample:
  enabled: false
amqpMessagingNetworkExample:
  enabled: true
deviceRegistryExample:
  type: mongodb
  addExampleData: false
  mongoDBBasedDeviceRegistry:
    mongodb:
      host: '{{ .Release.Name }}-mongodb'
      port: 27017
      dbName: hono
  hono:
    registry:
      http:
        insecurePortEnabled: true
adapters:
  mqtt:
    hono:
      mqtt:
        insecurePortEnabled: true
  http:
    hono:
      http:
        insecurePortEnabled: true
  amqp:
    hono:
      amqp:
        insecurePortEnabled: true

```

### Connect

:::tip
Check [architecture](../overview/architecture.md) to see which connections you need to set up
:::

#### Eclipse Ditto and InfluxDB

The process to connect Eclipse Ditto and InfluxDB will depend on Mosquitto or Apache Kafka. Choose the option you have selected in each step.

1. You have to add an output connection in Eclipse Ditto that publishes the events of the twins in the intermediary. This is done with a `POST` request to the URL `http://DITTO_NGINX_URL/api/2/connections` with the following body and the basic credentials: user _"devops"_ and password _"foobar"_. Remember to replace **DITTO_NGINX_URL** by a URL that allows access to the Eclipse Ditto Nginx service, you can check how to do it [here](https://kubernetes.io/docs/tasks/access-application-cluster/service-access-application-cluster/).

  You can check if the connection is working properly by reading the _opentwins_ topic in the selected broker with some tool or script and sending updates to some twin in [Ditto Protocol](https://eclipse.dev/ditto/protocol-overview.html) format. To create the twin check [here](https://eclipse.dev/ditto/http-api-doc.html#/Things/put_api_2_things__thingId_) and to see an example of an update message check [here](https://eclipse.dev/ditto/protocol-examples-modifyfeatures.html#modifyfeatures).

<Tabs groupId="intermediary">
  <TabItem value="mosquitto" label="Mosquitto" default>

:::warning
Change **MOSQUITTO_SERVICE_NAME** to the name of the Mosquitto service. You can check it with `kubectl get services`.
:::

```json title="PUT http://DITTO_NGINX_URL/api/2/connections"
{
  "name": "mosquitto-target-connection",
  "connectionType": "mqtt-5",
  "connectionStatus": "open",
  "uri": "tcp://MOSQUITTO_SERVICE_NAME:1883",
  "clientCount": 1,
  "failoverEnabled": true,
  "sources": [],
  "targets": [
    {
      "address": "opentwins/{{ topic:channel }}/{{ topic:criterion }}/{{ thing:namespace }}/{{ thing:name }}",
      "topics": [
        "_/_/things/twin/events?extraFields=thingId,attributes/_parents,features/idSimulationRun/properties/value",
        "_/_/things/live/messages",
        "_/_/things/live/commands"
      ],
      "qos": 1,
      "authorizationContext": [
        "nginx:ditto"
      ]
    }
  ]
}
```

  </TabItem>
  <TabItem value="kafka" label="Apache Kafka">

:::warning
Change **KAFKA_SERVICE_NAME** to the name of the Apache Kafka service. You can check it with `kubectl get services`.
:::

```json title="PUT http://DITTO_NGINX_URL/api/2/connections"
{
  "name": "kafka-target-connection",
  "connectionType": "kafka",
  "connectionStatus": "open",
  "uri": "tcp://KAFKA_SERVICE_NAME:9092",
  "specificConfig": {
	  "bootstrapServers": "KAFKA_SERVICE_NAME:9092",
	  "saslMechanism": "plain"
  },
  "failoverEnabled": true,
  "sources": [],
  "targets": [
    {
      "address": "opentwins",
      "topics": [
        "_/_/things/twin/events?extraFields=thingId,attributes/_parents,features/idSimulationRun/properties/value",
        "_/_/things/live/messages",
        "_/_/things/live/commands"
      ],
      "authorizationContext": [
        "nginx:ditto"
      ]
    }
  ]
}
```
  </TabItem>
</Tabs>

2. Now we will need to obtain a token in InfluxDB with write permissions. We will then access from a browser to the InfluxDB interface and [create an _opentwins_ organization](https://docs.influxdata.com/influxdb/v2/admin/organizations/create-org/). Then, follow the instructions in their documentation to [create an API token](https://docs.influxdata.com/influxdb/v2/admin/tokens/create-token/) in the organization. Save this token because we will use it next.

3. An instance of Telegraf must be deployed to read the events written in the intermediary broker and write them to the database. For this we will use the Telegraf Helm and add the necessary configuration in its values. You can check to Telegraf v1 documentation for both the [application](https://docs.influxdata.com/telegraf/v1/) and [Helm](https://github.com/influxdata/helm-charts/tree/master/charts/telegraf) for more information.

  The commands to deploy it are the following, using the necessary values file in each case.

```bash
helm repo add influxdata https://helm.influxdata.com/
helm repo update
helm install -n opentwins telegraf influxdata/telegraf -f values.yaml --version=1.8.27 --set tplVersion=2
```

<Tabs groupId="intermediary">
  <TabItem value="mosquitto" label="Mosquitto" default>

```yaml title="values.yaml"
service:
  enabled: false
config:
  agent:
    debug: true
  processors:
    - rename:
        replace:
         - tag: "extra_attributes__parents"
           dest: "parent"         
         - tag: "headers_ditto-originator"
           dest: "originator"
         - tag: "extra_features_idSimulationRun_properties_value"
           dest: "idSimulationRun"
         - tag: "extra_thingId"
           dest: "thingId"
  outputs:
    - influxdb_v2:
        urls:
          - "http://INFLUX_SERVICE_NAME:INFLUX_PORT"
        token: "INFLUXDB_TOKEN"
        organization: "opentwins"
        bucket: "default"
  inputs:
    - mqtt_consumer:
        servers:
          - "tcp://MOSQUITTO_SERVICE_NAME:1883"
        topics:
          - "opentwins/#"
        qos: 1
        tag_keys:
          - "extra_attributes__parents"
          - "extra_thingId"
          - "headers_ditto-originator"
          - "extra_features_idSimulationRun_properties_value"
          - "value_time_properties_value"   
        data_format: "json"
metrics:
  internal:
    enabled: false
```

  </TabItem>
  <TabItem value="kafka" label="Apache Kafka">

```yaml title="values.yaml"
service:
  enabled: false
config:
  agent:
    debug: true
  processors:
    - rename:
        replace:
         - tag: "extra_attributes__parents"
           dest: "parent"         
         - tag: "headers_ditto-originator"
           dest: "originator"
         - tag: "extra_features_idSimulationRun_properties_value"
           dest: "idSimulationRun"
         - tag: "extra_thingId"
           dest: "thingId"
  outputs:
    - influxdb_v2:
        urls:
          - "http://INFLUX_SERVICE_NAME:INFLUX_PORT"
        token: "INFLUXDB_TOKEN"
        organization: "opentwins"
        bucket: "default"
  inputs:
    - kafka_consumer:
        brokers:
          - "KAFKA_SERVICE_NAME:9092"
        topics:
          - "opentwins"
        tag_keys:
          - "extra_attributes__parents"
          - "extra_thingId"
          - "headers_ditto-originator"
          - "extra_features_idSimulationRun_properties_value"
          - "value_time_properties_value"   
        data_format: "json"
metrics:
  internal:
    enabled: false
```

  </TabItem>
</Tabs>

With this Eclipse Ditto and InfluxDB should be connected. You can check this by sending update messages to Eclipse Ditto and verifying if they are correctly written to the InfluxDB bucket. If not, check if the messages are arriving correctly to the intermediate broker and, if so, check the logs of the Telegraf pod to see if there is any error in the configuration (usually connection problems).

#### InfluxDB and Grafana

1. Obtain a [read access token in InfluxDB](https://docs.influxdata.com/influxdb/v2/admin/tokens/create-token/) for Grafana.
2. Access `Configuration > Data sources` on the Grafana interface and click on *Add data source*.
3. Select *InfluxDB* from the list. In the setup form it is very important to select *Flux* as query language. It will be necessary to fill in the URL section with the one that corresponds to InfluxDB service. You will also have to activate _Auth Basic_ and fill in the fields (in our case we have set the default admin of InfluxDB, but you can create a new user and fill in these fields). In the InfluxDB details you should indicate the organization, the bucket (default is *default*) and the token you have generated. 
4. When saving and testing, it should come out that at least one bucket has been found, indicating that they are already connected.

#### Eclipse Ditto and Eclipse Hono

In the following diagram you can see how Eclipse Hono and Eclipse Ditto are related in OpenTwins. 

<center>
  <img
    src={require('./img/ditto-hono-relationship.jpg').default}
    alt="Ditto and Hono relationship"
    style={{ width: 500 }}
  />
</center>

Basically, you will need to **create a connection between both for each Eclipse Hono tenant you want to use**. [Tenants](https://www.eclipse.org/hono/docs/concepts/tenancy/) basically act as device containers, so you could simply create a single tenant connected to Eclipse Ditto and store all the devices you need there. In this case we will do it this way, but you could create as many tenants and connections as your needs require.

The first thing to do is to check the IPs and ports to use with `kubectl get services -n $NS`. At this point we are interested in the *dt-service-device-registry-ext* and *dt-ditto-nginx* services, which correspond to Eclipse Hono and Eclipse Ditto respectively (if you have followed these instructions and services are NodePort, you will have to use port 3XXXX). 

We will then create a Hono tenant called, for example, ditto (you must override the variable **HONO_TENANT** if you have chosen another name).
```bash
HONO_TENANT=ditto
curl -i -X POST http://$HONO_IP:$HONO_PORT/v1/tenants/$HONO_TENANT
```

Now we will create the connection from Eclipse Ditto, which will act as a consumer of the AMQP endpoint of that tenant. To do this you will need to know the Eclipse Ditto devops password with the following command (the variable **RELEASE** is the name we gave to the Helm release when installing cloud2edge, if you have followed these instructions it should be dt).
```bash
RELEASE=dt
DITTO_DEVOPS_PWD=$(kubectl --namespace ${NS} get secret ${RELEASE}-ditto-gateway-secret -o jsonpath="{.data.devops-password}" | base64 --decode)
```
Now we [create the connection from Eclipse Ditto](https://www.eclipse.org/ditto/connectivity-manage-connections.html#create-connection) with the following command.
  
```bash
curl -i -X POST -u devops:${DITTO_DEVOPS_PWD} -H 'Content-Type: application/json' --data '{
  "targetActorSelection": "/system/sharding/connection",
  "headers": {
    "aggregate": false
  },
  "piggybackCommand": {
    "type": "connectivity.commands:createConnection",
    "connection": {
      "id": "hono-connection-for-'"${HONO_TENANT}"'",
      "connectionType": "amqp-10",
      "connectionStatus": "open",
      "uri": "amqp://consumer%40HONO:verysecret@'"${RELEASE}"'-dispatch-router-ext:15672",
      "failoverEnabled": true,
      "sources": [
        {
          "addresses": [
            "telemetry/'"${HONO_TENANT}"'",
            "event/'"${HONO_TENANT}"'"
          ],
          "authorizationContext": [
            "pre-authenticated:hono-connection"
          ],
          "enforcement": {
            "input": "{{ header:device_id }}",
            "filters": [
              "{{ entity:id }}"
            ]
          },
          "headerMapping": {
            "hono-device-id": "{{ header:device_id }}",
            "content-type": "{{ header:content-type }}"
          },
          "replyTarget": {
            "enabled": true,
            "address": "{{ header:reply-to }}",
            "headerMapping": {
              "to": "command/'"${HONO_TENANT}"'/{{ header:hono-device-id }}",
              "subject": "{{ header:subject | fn:default(topic:action-subject) | fn:default(topic:criterion) }}-response",
              "correlation-id": "{{ header:correlation-id }}",
              "content-type": "{{ header:content-type | fn:default('"'"'application/vnd.eclipse.ditto+json'"'"') }}"
            },
            "expectedResponseTypes": [
              "response",
              "error"
            ]
          },
          "acknowledgementRequests": {
            "includes": [],
            "filter": "fn:filter(header:qos,'"'"'ne'"'"','"'"'0'"'"')"
          }
        },
        {
          "addresses": [
            "command_response/'"${HONO_TENANT}"'/replies"
          ],
          "authorizationContext": [
            "pre-authenticated:hono-connection"
          ],
          "headerMapping": {
            "content-type": "{{ header:content-type }}",
            "correlation-id": "{{ header:correlation-id }}",
            "status": "{{ header:status }}"
          },
          "replyTarget": {
            "enabled": false,
            "expectedResponseTypes": [
              "response",
              "error"
            ]
          }
        }
      ],
      "targets": [
        {
          "address": "command/'"${HONO_TENANT}"'",
          "authorizationContext": [
            "pre-authenticated:hono-connection"
          ],
          "topics": [
            "_/_/things/live/commands",
            "_/_/things/live/messages"
          ],
          "headerMapping": {
            "to": "command/'"${HONO_TENANT}"'/{{ thing:id }}",
            "subject": "{{ header:subject | fn:default(topic:action-subject) }}",
            "content-type": "{{ header:content-type | fn:default('"'"'application/vnd.eclipse.ditto+json'"'"') }}",
            "correlation-id": "{{ header:correlation-id }}",
            "reply-to": "{{ fn:default('"'"'command_response/'"${HONO_TENANT}"'/replies'"'"') | fn:filter(header:response-required,'"'"'ne'"'"','"'"'false'"'"') }}"
          }
        },
        {
          "address": "command/'"${HONO_TENANT}"'",
          "authorizationContext": [
            "pre-authenticated:hono-connection"
          ],
          "topics": [
            "_/_/things/twin/events",
            "_/_/things/live/events"
          ],
          "headerMapping": {
            "to": "command/'"${HONO_TENANT}"'/{{ thing:id }}",
            "subject": "{{ header:subject | fn:default(topic:action-subject) }}",
            "content-type": "{{ header:content-type | fn:default('"'"'application/vnd.eclipse.ditto+json'"'"') }}",
            "correlation-id": "{{ header:correlation-id }}"
          }
        }
      ]
    }
  }
}' http://$DITTO_IP:$DITTO_PORT/devops/piggyback/connectivity
```

This connection is configured so that if an [Eclipse Hono device](https://www.eclipse.org/hono/docs/concepts/device-identity/) has the [ThingId](https://www.eclipse.org/ditto/basic-thing.html#thing-id) of an Eclipse Ditto twin as its identifier, its messages will be redirected to that twin directly (explained in more detail in the [usage](#usage) section).

## Compositional support

## Data prediction with machine learning

## 3D representation