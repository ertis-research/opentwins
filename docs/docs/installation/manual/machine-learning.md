---
sidebar_position: 3
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Machine Learning

## Prerequisites

Before you begin, ensure you have the following:
- Container manager: Currently tested on [Docker](https://www.docker.com/) and [ContainerD](https://containerd.io/).
- Access to a [Kubernetes](https://kubernetes.io/releases/download/) cluster.
- `kubectl` installed and configured.

## Deploy

To enable machine learning-based data prediction in OpenTwins, you must integrate Kafka-ML along with a service that supplies the required input data for the models. Refer to the [architecture](../../overview/architecture.md#data-prediction-with-machine-learning) documentation to understand how this component interacts with the overall system.

Although OpenTwins includes two built-in services for this purpose, they both depend on Eclipse Hono, which we currently do not recommend. **We strongly advise developing a custom service that sends input data to the Kafka-ML topic**, ensuring it aligns with the specific requirements of your digital twin. This service can obtain the data from the related digital twin as well as from other twins, either real-time via Eclipse Ditto or historical from InfluxDB.

### Kafka-ML

To install Kafka-ML, follow the official instructions in its [GitHub repository](https://github.com/ertis-research/kafka-ml). Kafka-ML can be deployed in a Kubernetes cluster (recommended) or locally, depending on your infrastructure needs.

Kafka-ML facilitates seamless integration of machine learning models with Kafka. It provides the capability to:

- Upload models from TensorFlow or PyTorch.
- Train models directly within the system.
- Deploy models for real-time inference using Kafka streams.

The Kafka-ML workflow is as follows:
1.  Input data is sent through a designated Kafka topic to trigger model inference.
2. Kafka-ML processes the data and runs the prediction.
3. The predicted results are returned via an output Kafka topic.

For an in-depth understanding of Kafka-ML’s features and configuration, we highly recommend consulting the official user guide available in its [GitHub repository](https://github.com/ertis-research/kafka-ml).

In OpenTwins documentation, **we assume that models are already deployed, trained, and ready for inference within Kafka-ML.**

### Eclipse Hono to Kafka-ML

<!--
This component, developed in Python with Flask, has its code available in [its repository](https://github.com/ertis-research/eclipse-hono-to-kafka-ml). It has been containerized with Docker and published in [DockerHub](https://hub.docker.com/r/ertis/ditto-extended-api).
-->

:::warning
This component is currently **unused** because **Eclipse Hono** has been deprecated.  
The plan is to migrate this component to the new architecture so that it reads data directly from **Eclipse Ditto**, ensuring a more efficient and scalable integration.
:::

### Error detection for Eclipse Hono with Kafka-ML

:::warning
This component is currently **unused** because **Eclipse Hono** has been deprecated.  
The plan is to migrate this component to the new architecture so that it reads data directly from **Eclipse Ditto**, ensuring a more efficient and scalable integration.
:::

## Connect

To integrate Kafka-ML with OpenTwins, you need to establish an Eclipse Ditto connection in OpenTwins. This connection enables OpenTwins to receive data from Kafka-ML and update the corresponding digital twin. The process involves defining an Eclipse Ditto [JavaScript mapping](https://eclipse.dev/ditto/3.3/connectivity-mapping.html#javascript-mapping-engine) to convert incoming messages into the [Ditto Protocol](https://eclipse.dev/ditto/3.3/protocol-examples-mergefeatures.html).

The connection must follow this data flow:
1. **Kafka-ML** generates predictions and send data to **Apache Kafka**.  
2. **Eclipse Ditto** receives the data via a configured connection.  
3. The **JavaScript mapping** in Eclipse Ditto processes the data and updates the corresponding digital twin in OpenTwins.  

### Connection example

Consider a **machine learning model** in Kafka-ML that predicts **humidity** and **temperature** for a device. The model outputs data in the format `[humidity, temperature]`.

In OpenTwins, a twin exists with the ID `example:sensor`, containing two features: **humidity** and **temperature**. Each feature in Eclipse Ditto [must include](https://eclipse.dev/ditto/3.3/basic-thing.html#api-version-2) properties and a value. The following image shows the structure of the digital twin in Eclipse Ditto:

<center>
  <img
    src={require('./img/kafkaml1.png').default}
    alt="Example digital twin structure"
    style={{ width: 300 }}
  />
</center>

To establish the connection between Kafka-ML and OpenTwins, you need to create an [Apache Kafka connection in Eclipse Ditto](https://eclipse.dev/ditto/3.3/connectivity-protocol-bindings-kafka2.html). This connection receives and transforms predictions from Kafka-ML.

In the connection, a JavaScript mapping must be defined to transform messages from Apache Kafka. The mapping reads incoming messages, converts them to JSON, extracts values for each feature, and formats the message according to the Ditto Protocol before sending it. The function name _mapToDittoProtocolMsg_ and the return _Ditto.buildDittoProtocolMsg_ must always be included. The return value can be a single Ditto message or a list of Ditto messages.

Here is the JavaScript mapping for this example:

```js
function mapToDittoProtocolMsg(headers, textPayload, bytePayload, contentType) {
    const jsonData = JSON.parse(textPayload);
    const humidity = jsonData[0];
    const temperature = jsonData[1];

    headers = Object.assign(headers, { 'Content-Type': 'application/merge-patch+json' });

    var features = {
          humidity: {
               properties: {
                    value: humidity 
               }
         },
         temperature: {
              properties: {
                   value: temperature
              }
         }
    };

    return Ditto.buildDittoProtocolMsg(
        'example',
        'sensor', 
        'things',
        'twin',
        'commands',
        'merge',
        '/features',
        headers,
        features
    );
}
```

In OpenTwins, there are two ways to create the connection: via the **OpenTwins UI** or the **Eclipse Ditto API**. In both cases we recommend to refer to the [Eclipse Ditto documentation](https://eclipse.dev/ditto/3.3/connectivity-overview.html).

<Tabs groupId="environment">
  <TabItem value="ui" label="OpenTwins UI" default>


1. In Grafana, open the OpenTwins app from the left-side menu.

2. Navigate to the **Connections** section and click **Create new connection**.

3. Select **Kafka** as the connection type and fill in the required details for the Apache Kafka broker.

    - Provide a unique identifier for the connection (avoid special characters except -).
    - If authentication is required, include the username and password in the URI.
    - The bootstrap servers usually match the URI.
    - Leave other fields as default unless necessary.

4. Under **Message Mapping**, click Add JavaScript Mapping, enter an identifier, and paste the JavaScript code from above.

5. Under **Sources**, click Add Source, then:
    - Specify the **Kafka topic** where Kafka-ML outputs predictions.
    - Set **Authorization Context** to `pre-authenticated:kafkaml-connection`.
    - Select the previously defined JavaScript mapping under **Payload Mapping**.

6. Click **Create Connection**. If a success message appears, the **Kafka-ML model is now connected**.


  </TabItem>
  <TabItem value="api" label="Eclipse Ditto API"> 


:::info
If you are using **Minikube**, you must expose the Eclipse Ditto nginx service in order to access it from your *localhost*. To do this, find the name of the service with `kubectl get services` and then run `minikube service <service-name> --url`.
:::

1. Open an API tool such as [Postman](https://www.postman.com/).
2. Create a **JSON payload** for the connection configuration based on the [Eclipse Ditto documentation](https://eclipse.dev/ditto/3.3/connectivity-protocol-bindings-kafka2.html#establishing-connecting-to-an-apache-kafka-endpoint).
3. Embed the JavaScript mapping within the JSON configuration.
    - Ensure all lines end with semicolons (;).
    - Use **single quotes** ('), not double quotes (").
    - Remove all line breaks.
    - If issues arise, replace single quotes with \u0027.
4. Add the **source configuration**, including the Kafka topic and **JavaScript mapping identifier**. Example JSON payload for output topic `kafkaml-output-topic`:
    
    ```json
    {
        "name": "kafkaml-source-connection",
        "connectionType": "kafka",
        "connectionStatus": "open",
        "uri": "tcp://{KAFKA-IP}:{KAFKA-PORT}",
        "specificConfig": {
            "saslMechanism": "plain",
            "bootstrapServers": "{KAFKA-IP}:{KAFKA-PORT}"
        },
        "sources": [
            {
                "addresses": [
                    "kafkaml-output-topic"
                ],
                "qos": 1,
                "authorizationContext": [
                    "pre-authenticated:kafkaml-connection"
                ],
                "payloadMapping": [
                    "jsmapping"
                ]
            }
        ],
        "targets": [],
        "clientCount": 1,
        "mappingDefinitions": {
            "jsmapping": {
                "mappingEngine": "JavaScript",
                "options": {
                    "incomingScript": "function mapToDittoProtocolMsg(headers, textPayload, bytePayload, contentType) { const jsonData = JSON.parse(textPayload); const humidity = jsonData[0]; const temperature = jsonData[1]; headers = Object.assign(headers, { 'Content-Type': 'application/merge-patch+json' }); var features = { humidity: { properties: { value: humidity } }, temperature: { properties: { value: temperature } } }; return Ditto.buildDittoProtocolMsg( 'example', 'sensor', 'things', 'twin', 'commands', 'merge', '/features', headers, features ); }"
                }
            }
        },
        "tags": []
    }
    ```

5. Send a [**PUT request**](https://eclipse.dev/ditto/3.3/http-api-doc.html#/Connections/put_api_2_connections__connectionId_) to:
    
    ```
    http://{ECLIPSE_DITTO_URI}/api/2/connections/{ID_CONNECTION}
    ```
    - Include the JSON payload created earlier as the **request body**.
    - Replace `{ECLIPSE_DITTO_URI}` with the corresponding Eclipse Ditto IP and port.
    - Replace `{ID_CONNECTION}` with the desired connection identifier (e.g., `kafkaml-source-connection`).
    - Use **Basic Authentication** with the default username (`devops`) and password (`foobar`), or use the configured credentials.


  </TabItem>
</Tabs>


Once the integration is complete, Kafka-ML predictions will be processed and updated in OpenTwins. For example, if Kafka-ML outputs `[23.45, 23.12]`, the update message to digital twin will appear as follows:

<center>
  <img
    src={require('./img/kafkaml2.png').default}
    alt="Example digital twin message"
    style={{ width: 300 }}
  />
</center>

With this setup, **a machine learning model in Kafka-ML and OpenTwins are connected**. Once the model is activated (via a script or any other method you choose), the generated data will automatically update the specified digital twin according to your configured settings. Additionally, in InfluxDB, you can differentiate between generated and real data using the authorization context.
