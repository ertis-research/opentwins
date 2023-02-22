---
sidebar_position: 1
---

# Using Helm

## Requirements

- Container manager:
  - Tested on [Docker](https://www.docker.com/) and [ContainerD](https://containerd.io/).
- [Kubernetes](https://kubernetes.io/releases/download/):
  - Tested on [Kubernetes](https://kubernetes.io/releases/download/) and [K3s](https://k3s.io/).
- [Helm](https://helm.sh/docs/intro/install/) version 16.14 or above.


## Installation
First of all, you have to add Ertis Research group helm repository to your helm repository list:
```bash
helm repo add Ertis https://ertis-research.github.io/Helm-charts/
```

Once done, the next step is installing the chart by executing this line on your terminal (in our case, we will use `ot` as release name and `digitaltwins` as namespace, but you can choose the one that you prefeer):

```bash
helm install ot Ertis/OpenTwins -n digitaltwins
```

After waiting some time, the installation is done, but you still need to configure several conections (we are working on making it automatic) as described on the [wiki](https://ertis-research.github.io/digital-twins-platform/).


## Lightweight installation
As described in the main page, OpenTwins has it's own lightweight version that aims to run on IoT devices such as Raspberry Pi devices.
To install this versión, you have to follow the first step in order to add Ertis repository to your repository list and then install the platform using the command bellow:
```bash
helm install ot Ertis/OpenTwins-Lightweight -n digitaltwins
``` 
As in the previous case, some connections still need to be made for the platform to work properly.