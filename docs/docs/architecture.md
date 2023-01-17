---
sidebar_position: 2
---

# Architecture

This platform is built around the [Eclipse Ditto](https://www.eclipse.org/ditto/) digital twin framework. The following image shows the current architecture of the platform, which is intended to be extended over time. Each of the colours represents components that serve a certain functionality. These components are mostly external projects to our organisation, although there are also certain components that have had to be created especially for the platform. The code and documentation for these can be found in their respective repositories, which are all linked in the [components folder](/components).

![Architecture](img/architecture.jpg)

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