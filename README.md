# opentwins-v2-prototype
Experimental prototype for OpenTwins version 2: focused on enhancing scalability and composability.

## Requirements
- .NET 8.0 SDK (v8.0.408) - Windows x64

winget install Dapr.CLI 

En consola con admin:
dapr init


dapr --version
Output:
CLI version: 1.15.1 
Runtime version: 1.15.4

Tenemos 4 proyectos, cada uno por cada componente de la arquictura que tiene que ser desarrollado.


Crear los proyectos .NET:
dotnet new webapi -n Twins -f net8.0
dotnet new webapi -n Things -f net8.0
dotnet new webapi -n Orchestrator -f net8.0

Añadir los paquetes a cada proyecto que lo necesite:
dotnet add package Dapr.Client
dotnet add package Dapr.Actors.AspNetCore
dotnet add package Confluent.Kafka
dotnet add package Dapr.AspNetCore

Ejecutar el proyecto (los contenedores de dapr tienen que estar iniciados!):
dapr run --app-id myapp --app-port 5000 --dapr-http-port 3500 -- dotnet run

Benthos:
docker pull jeffail/benthos:latest
kubectl create configmap benthos-config --from-file=benthos.yaml=kubernetes\benthos-mqtt-kafka\benthos-config.yaml
kubectl apply -f .\kubernetes\benthos-mqtt-kafka\benthos-deploy.yaml

kubectl create configmap benthos-mqtt-kafka-config --from-file=benthos.yaml=kubernetes\benthos-mqtt-kafka\benthos-config-basic.yaml
kubectl create configmap benthos-kafka-timescaledb-config --from-file=benthos.yaml=kubernetes\benthos-kafka-timescaledb\benthos-config-basic.yaml

kubectl apply -f .\kubernetes\benthos-mqtt-kafka\benthos-deploy.yaml
kubectl apply -f .\kubernetes\benthos-kafka-timescaledb\benthos-deploy.yaml


https://github.com/schivei/dgraph4net

Mosquitto:
kubectl apply -f .\kubernetes\mosquitto\mosquitto-deploy.yaml

helm install opentwinsv2-dgraph dgraph/dgraph -f kubernetes/dgraph/values.yaml

Kafka:
helm install kafka oci://registry-1.docker.io/bitnamicharts/kafka -f .\kubernetes\kafka\values.yaml --set externalAccess.controller.service.domain="ip del cluster" --set externalAccess.broker.service.domain="ip del cluster"
helm install kafka-ui kafka-ui/kafka-ui -f .\kubernetes\kafka\values-ui.yaml

Redis:
helm install opentwinsv2-redis oci://registry-1.docker.io/bitnamicharts/redis -f .\kubernetes\redis\values.yaml

PostgreSQL:
helm install opentwinsv2-postgresql oci://registry-1.docker.io/bitnamicharts/postgresql -f kubernetes/postgresql/values.yaml


DGraph:
helm repo add dgraph https://charts.dgraph.io
helm install opentwinsv2-dgraph dgraph/dgraph


Kafka can be accessed by consumers via port 9092 on the following DNS name from within your cluster:

    kafka.opentwinsv2.svc.cluster.local

Each Kafka broker can be accessed by producers via port 9092 on the following DNS name(s) from within your cluster:

    kafka-controller-0.kafka-controller-headless.opentwinsv2.svc.cluster.local:9092
    kafka-controller-1.kafka-controller-headless.opentwinsv2.svc.cluster.local:9092
    kafka-controller-2.kafka-controller-headless.opentwinsv2.svc.cluster.local:9092

{
  "@context": ["https://www.w3.org/2019/wot/td/v1"],
  "id": "urn:dev:wot:com:example:temperature-sensor",
  "title": "TemperatureSensor",
  "properties": {
    "temperature": {
      "type": "number",
      "description": "Current temperature in Celsius",
      "readOnly": true,
      "observable": true,
      "forms": [
        {
          "href": "mqtt://localhost:1883/sensors/temperature",
          "contentType": "application/json",
          "subprotocol": "mqtt"
        }
      ]
    }
  }
}

OLVIDATE DE LO SIGUIENTE: ESTO Configurar Meinberg NTP en Windows https://www.meinbergglobal.com/english/sw/ntp.htm#ntp_stable
CON NTP:
0.pool.ntp.org
1.pool.ntp.org
2.pool.ntp.org
time.google.com
time.cloudflare.com

net start w32time
w32tm /resync desde CMD con privilegios de admin.

w32tm /config /manualpeerlist:"ntp.ubuntu.com" /syncfromflags:manual /update
net stop w32time
net start w32time
w32tm /resync


helm upgrade --install dapr dapr/dapr 
 --version=1.15 
 --namespace dapr-system 
 --create-namespace 
 --set dapr_scheduler.cluster.storageSize=16Gi
 --set global.mtls.enabled=false 
 --wait

helm upgrade --install dapr-dashboard dapr/dapr-dashboard --namespace dapr-system --set serviceType=NodePort

docker build -t REGISTRYID/opentwinsv2-events:0.0.1 . -f Dockerfile.Events
docker push REGISTRYID/opentwinsv2-events:0.0.1