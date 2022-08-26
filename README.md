# Digital Twins Platform

This platform has been designed to facilitate the development of digital twins and is characterised by the exclusive use of open source components. The aim is to achieve a platform that covers all the functionalities that a digital twin may require, from the most basic ones, such as simply checking its real-time state, to more advanced ones, such as the inclusion of predicted or simulated data.

This platform is currently **under development**, so its use in production environments is not recommended at this stage.

## Table of Contents
- [Changelog](#changelog)
- [Architecture](#architecture)
- [Deploy platform in a fast way](#deploy-platform-in-a-fast-way)
    - [Requirements to deploy using Helm](#requirements-to-deploy-using-helm)
    - [Steps to deploy platform using Helm](#steps-to-deploy-platform-using-helm)
- [Deploy platform manually](#deploy-platform-manually)
    - [Requirements to deploy manually](#requirements-to-deploy-manually)
    - [Steps to deploy platform manually](#steps-to-deploy-platform-manually)
      - [Deploying Eclipse Ditto and Eclipse Hono](#deploying-eclipse-ditto-and-eclipse-hono)
      - [Deploying Apache Kafka](#deploying-apache-kafka)
      - [Deploying InfluxDB](#deploying-influxdb)
- [Usage](#usage)
- [Publications](#publications)
- [License](#license)

## Changelog

## Architecture

This platform is built around the [Eclipse Ditto](https://www.eclipse.org/ditto/) digital twin framework. The following image shows the current architecture of the platform, which is intended to be extended over time. Each of the colours represents components that serve a certain functionality. These components are mostly external projects to our organisation, although there are also certain components that have had to be created especially for the platform. The code and documentation for these can be found in their respective repositories, which are all linked in the [components folder](/components).

![Architecture](images/architecture.jpg)

- The **blue part** represents the base of the platform, as it is made up of components that cover the **basic functionality** of any digital twin platform. It is composed of the following components:
  - [Eclipse Ditto](https://www.eclipse.org/ditto/).
  - [Eclipse Hono](https://www.eclipse.org/hono/).
  - [Apache Kafka](https://kafka.apache.org/). It works as an intermediary between Eclipse Ditto and Telegraf, as they cannot connect directly (they need a component that acts as a broker).
  - [Telegraf](https://www.influxdata.com/time-series-platform/telegraf/). This is the recommended data collector for InfluxDB databases. It is in charge of constantly consuming the given Kafka topic and writing the received messages to the specified InfluxDB database.
  - [InfluxDB](https://www.influxdata.com/products/influxdb-overview/).
  - [Grafana](https://grafana.com/oss/grafana/).
  - [Digital Twins plugin for Grafana](https://github.com/ertis-research/digital-twins-plugin-for-Grafana/).
  - [Extended API for Eclipse Ditto](https://github.com/ertis-research/extended-api-for-Eclipse-Ditto/).
- The **yellow part**...
- The **red part**...

## Deploy platform in a fast way
### Requirements to deploy using Helm
- [Docker](https://www.docker.com/)
- [Kubernetes](https://kubernetes.io/)
- [Helm](https://helm.sh/)

### Steps to deploy platform using Helm
*Currently in development*

## Deploy platform manually
This section will explain how to deploy the platform manually. Basically, you will have to deploy or install the different components and then connect them. The procedure explained below is the one followed to deploy them in **Kubernetes** using in most cases the **Helm** option, but any other installation in which all the components are correctly installed and there is some kind of network between them to be able to communicate can be used. 

It is not necessary to deploy all components if not all functionalities are to be used. Check the [architecture](#architecture) section to find out which ones are essential and what functionality is covered by each of them.

### Requirements to deploy manually
- [Docker](https://www.docker.com/)
- [Kubernetes](https://kubernetes.io/)
- [Helm](https://helm.sh/)

### Steps to deploy platform manually
We recommend installing all components in the same Kubernetes namespace to make it easier to identify and control them all. In our case the namespace name will be stored in a bash variable called **NS**.

Also note that the values files have the variables that we recommend for the installation of each Helm Chart, but they can be extended or modified according to your needs (to do so, please consult the Helm Chart documentation).

#### Deploying Eclipse Ditto and Eclipse Hono
To deploy both Eclipse Ditto and Eclipse Hono we will directly install the [cloud2edge package](https://www.eclipse.org/packages/packages/cloud2edge/), which is specially created to allow these two tools to connect correctly.
Before executing the commands we will need to have the files [pv-hono.yaml](files_for_manual_deploy/pv-hono.yaml), [pv-mongodb.yaml](files_for_manual_deploy/pv-mongodb.yaml), [pvc-mongodb.yaml](files_for_manual_deploy/pvc-mongodb.yaml) and [values-cloud2edge.yaml](files_for_manual_deploy/values-cloud2edge.yaml) in the folder where we are in the terminal.
Once ready, and complying with all the [prerequisites](https://www.eclipse.org/packages/prereqs/) of the package, we execute the following commands.
```sh
helm repo add eclipse-iot https://eclipse.org/packages/charts
helm repo update

kubectl create namespace $NS
kubectl apply -f pv-hono.yaml -n $NS
kubectl apply -f pv-mongodb.yaml -n $NS
kubectl apply -f pvc-mongodb.yaml -n $NS

helm install -n $NS --wait --timeout 15m dt eclipse-iot/cloud2edge --version=0.2.3 -f values-cloud2edge.yaml --dependency-update --debug
```
If all pods are running and ready we already have the first two components installed.

#### Deploying Apache Kafka
To deploy Kafka, the yaml files from another project have been reused, but it could also be installed [using Helm](https://github.com/bitnami/charts/tree/master/bitnami/kafka) if you prefer. 

For Kafka to work, it is necessary to install [ZooKeeper](https://zookeeper.apache.org/) beforehand. In addition, [CMAK](https://github.com/deltaprojects/kafka-manager-docker), a tool to manage Apache Kafka, will be used to make it easier to use. Then, for the deployment, the [zookeeper-pod.yaml](files_for_manual_deploy/zookeeper-pod.yaml), [zookeeper-service.yaml](files_for_manual_deploy/zookeeper-service.yaml), [kafka-pod.yaml](files_for_manual_deploy/kafka-pod.yaml), [kafka-service.yaml](files_for_manual_deploy/kafka-service.yaml), [kafka-manager-deployment.yaml](files_for_manual_deploy/kafka-manager-deployment.yaml) and [kafka-manager-service.yaml](files_for_manual_deploy/kafka-manager-service.yaml) files will be needed. Once you have them, you only need to apply them to the chosen namespace.
```sh
kubectl apply -f zookeeper-pod.yaml -n $NS
kubectl apply -f zookeeper-service.yaml -n $NS

kubectl apply -f kafka-pod.yaml -n $NS
kubectl apply -f kafka-service.yaml -n $NS

kubectl apply -f kafka-manager-deployment.yaml -n $NS
kubectl apply -f kafka-manager-service.yaml -n $NS
```

#### Deploying InfluxDB
For InfluxDB, [Helm](https://github.com/influxdata/helm-charts/tree/master/charts/influxdb2) will again be used for deployment. The following [sc-influxdb2.yaml](files_for_manual_deploy/sc-influxdb2.yaml) and [pv-influxdb2.yaml](files_for_manual_deploy/pv-influxdb2.yaml) files will be required to be applied before installation. In addition, the recommended values are in the [values-influxdb2.yaml](files_for_manual_deploy/values-influxdb2.yaml) file (it is recommended that you check it before installing and change the *password* variable to your preference).

```sh
helm repo add influxdata https://helm.influxdata.com/

kubectl apply -f sc-influxdb2.yaml -n $NS
kubectl apply -f pv-influxdb2.yaml -n $NS

helm install -n $NS influxdb influxdata/influxdb2 -f values-influxdb2.yaml --version=2.0.10 
```

#### Connecting Eclipse Hono and Eclipse Ditto
In the following diagram you can see how Eclipse Hono and Eclipse Ditto are related in our platform. 

![Ditto and Hono relationship](images/ditto-hono-relationship.jpg)

Basically, you will need to **create a connection between both for each Eclipse Hono tenant you want to use**. [Tenants](https://www.eclipse.org/hono/docs/concepts/tenancy/) basically act as device containers, so you could simply create a single tenant connected to Eclipse Ditto and store all the devices you need there. In this case we will do it this way, but you could create as many tenants and connections as your needs require.

The first thing to do is to check the IPs and ports to use with `kubectl get services -n $NS`. At this point we are interested in the *dt-service-device-registry-ext* and *dt-ditto-nginx* services, which correspond to Eclipse Hono and Eclipse Ditto respectively (if you have followed these instructions and services are NodePort, you will have to use port 3XXXX). 

We will then create a Hono tenant called, for example, ditto (you must override the variable **HONO_TENANT** if you have chosen another name).
```sh
HONO_TENANT=ditto
curl -i -X POST http://$HONO_IP:$HONO_PORT/v1/tenants/$HONO_TENANT
```

Now we will create the connection from Eclipse Ditto, which will act as a consumer of the AMQP endpoint of that tenant. To do this you will need to know the Eclipse Ditto devops password with the following command (the variable **RELEASE** is the name we gave to the Helm release when installing cloud2edge, if you have followed these instructions it should be dt).
```sh
RELEASE=dt
DITTO_DEVOPS_PWD=$(kubectl --namespace ${NS} get secret ${RELEASE}-ditto-gateway-secret -o jsonpath="{.data.devops-password}" | base64 --decode)
```
Now we [create the connection from Eclipse Ditto](https://www.eclipse.org/ditto/connectivity-manage-connections.html#create-connection) with the following command.
<details>
  <summary>Show command</summary>
  
```sh
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
</details>

This connection is configured so that if an [Eclipse Hono device](https://www.eclipse.org/hono/docs/concepts/device-identity/) has the [ThingId](https://www.eclipse.org/ditto/basic-thing.html#thing-id) of an Eclipse Ditto twin as its identifier, its messages will be redirected to that twin directly (explained in more detail in the [usage](#usage) section).


#### Connecting Eclipse Ditto and Kafka

#### Connecting Kafka and InfluxDB: Deploying Telegraf

#### Connecting InfluxDB and Grafana


## Usage

## Publications

## License







