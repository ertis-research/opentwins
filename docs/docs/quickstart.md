---
sidebar_position: 1
---

# Quickstart

Welcome to OpenTwins, a flexible platform adapted to your needs! Although OpenTwins offers extensive customization options, we understand the importance of simplicity for beginners. Therefore, let's embark on a short journey together, showing you the quickest route to deploy the platform and develop a simple, functional digital twin.

## Prerequisites
Please be sure you have the following utilities installed on your host machine:

- [Docker](https://www.docker.com/)
- [Kubernetes](https://kubernetes.io/releases/download/)
- [Helm](https://helm.sh/docs/intro/install/) v3

If you don't have a Kubernetes cluster, you can set one up on local using [minikube](https://minikube.sigs.k8s.io/docs/). For a smooth deployment experience, we suggest you use the following minimum configuration values.

```bash
minikube start --cpus 4 --disk-size 40gb --memory 8192
```
```bash
kubectl config use-context minikube
```

## Installation
The quickest way to deploy OpenTwins is [using Helm](https://helm.sh/docs/intro/using_helm/).

The following command adds the ERTIS repository where the OpenTwins helm chart is located.

```bash
helm repo add ertis https://ertis-research.github.io/Helm-charts/
```

To deploy the platform with recommended functionality, use the command below:

```bash
helm upgrade --install opentwins ertis/OpenTwins -n opentwins --wait --dependency-update
```

To modify the components to be deployed and connected during the installation, you can check the [installation via Helm](./installation/using-helm.md).


## Define your first digital twin

A digital twin is composed of static and dynamic information.

## Link the digital twin to a data input

## Visualize twin data