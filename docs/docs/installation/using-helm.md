---
sidebar_position: 2
---

# Helm

## Installation
First of all, you have to add ERTIS Research group helm repository to your helm repository list:

```bash
helm repo add ertis https://ertis-research.github.io/Helm-charts/
```

Once done, the next step is installing the chart by executing this line on your terminal (in our case, we will use `opentwins` as release name and `opentwins` as namespace, but you can choose the one that you prefeer). To customize the installation, please refer to [Helm's values](https://github.com/ertis-research/Helm-charts/blob/main/OpenTwins/values.yaml) file.

```bash
helm upgrade --install opentwins ertis/OpenTwins -n opentwins --wait --dependency-update
```

After waiting some time, the installation will be ready for use.


## Lightweight installation
As described in the main page, OpenTwins has it's own lightweight version that aims to run on IoT devices such as Raspberry Pi devices.
To install this versión, you have to follow the first step in order to add ERTIS repository to your repository list and then install the platform using the command bellow:
```bash
helm install ot ertis/OpenTwins-Lightweight -n opentwins
``` 
In this case connections still need to be made for the platform to work properly.
