---
sidebar_position: 5
---

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# 3D representation

## Prerequisites

Before you begin, make sure you have the following:
- A container manager: Currently tested on [Docker](https://www.docker.com/) and [ContainerD](https://containerd.io/).
- Access to a [Kubernetes](https://kubernetes.io/releases/download/) (recommended) or [K3s](https://k3s.io/) cluster.
- `kubectl` installed and properly configured.

## Deploy

To integrate OpenTwins with a 3D representation using Unity, you need to add a specific plugin to Grafana. For more details on its functionality and how it connects with other components, refer to the [architecture](../../overview/architecture.md#3d-representation) section.

:::info
This plugin only displays 3D models developed in Unity and exported as WebGL. It does not support model development. **To create or modify 3D models, you need to install [Unity](https://unity.com/es/download).**
:::

### Unity panel plugin for Grafana

The code of this plugin can be found in [his repository](https://github.com/ertis-research/grafana-panel-unity) and the latest version of the plugin will always be compiled in a zip file as a [release](https://github.com/ertis-research/grafana-panel-unity/releases/tag/latest).

The installation of the Unity plugin in Grafana will depend on how Grafana was installed and the version you are using. Below, we explain the process for installations done via Helm and for local installations. However, it is highly recommended to check the [official Grafana documentation](https://grafana.com/docs/grafana/latest/administration/plugin-management/#install-grafana-plugins) for detailed instructions on plugin installation, as there may be specific variations depending on your environment or version.

<Tabs groupId="environment">
  <TabItem value="helm" label="Helm" default>

To install the plugin you need to add its compiled code in a folder with the same name as its ID inside the Grafana plugins folder, which is _/var/lib/grafana/plugins_ by default.
For Helm-based deployments, you can achieve this by adding an `extraInitContainer` to your `values.yaml` file. This container will navigate to the plugins folder, download the latest release zip file, and unzip it.

Below is the necessary configuration:

:::danger
Some keys may overlap with existing ones in your `values.yaml` file. **Do not copy and paste blindly, merge them carefully**.
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
      wget --no-check-certificate -O ertis-unity-panel.zip https://github.com/ertis-research/grafana-panel-unity/releases/download/latest/ertis-unity-panel.zip
      unzip -o ertis-unity-panel.zip
      rm ertis-unity-panel.zip
  volumeMounts:
  - name: storage
    mountPath: /grafana-storage
``` 

Since the plugin is currently unsigned, you need to explicitly allow its use in Grafana by adding its ID (_ertis-unity-panel_) to the list of unsigned plugins in your `values.yaml` file. This step ensures Grafana recognizes the plugin; otherwise, it will not appear in the UI.
Below is what you need to add (note that if you have followed the manual installation of the [essential functionality](./essential.md#grafana-v95) you should already have it configured.).

```yaml title=values.yaml
grafana.ini:
  plugins:
    allow_loading_unsigned_plugins: ertis-opentwins,ertis-unity-panel
``` 

Once this is configured, update the Grafana Helm deployment:

```bash
helm upgrade grafana grafana/grafana -n opentwins --version 8.5.0 -f values.yaml
```

Verify that the Grafana pod is in Running and Ready state. The Unity panel plugin should now be available in Grafana as a panel type.

  </TabItem>
  <TabItem value="local" label="Local">

To install the plugin on a locally hosted Grafana instance, follow these steps:

1. [Download the zip file](https://github.com/ertis-research/grafana-panel-unity/releases/download/latest/ertis-unity-panel.zip) of the latest plugin release.

2. Locate the **[Grafana configuration file](https://grafana.com/docs/grafana/v11.3/setup-grafana/configure-grafana)** on your system. Refer to the [Grafana documentation](https://grafana.com/docs/grafana/v11.3/setup-grafana/configure-grafana/#configuration-file-location) to find its exact location, name, and how to modify it.

3. Open the configuration file and **uncomment/add** the following:

```ini
[plugins]
# Enter a comma-separated list of plugin identifiers to identify plugins to load even if they are unsigned. Plugins with modified signatures are never loaded.
allow_loading_unsigned_plugins = ertis-unity-panel
```

4. In the same configuration file, check or modify the **path to the plugins folder** ([see Grafana documentation](https://grafana.com/docs/grafana/v11.3/setup-grafana/configure-grafana/#plugins)).

5. Navigate to the plugins folder and extract the downloaded zip file. Ensure the extracted folder is named `ertis-unity-panel` and contains the correct structure (no intermediate folders).

<center> 
<img
    src={require('./img/opentwins-plugin-panel-content.png').default}
    alt="Kubectl get services"
    style={{ width: 700 }}
/>
</center>

6. Restart Grafana for the changes to take effect. Consult the [Grafana restart guide](https://grafana.com/docs/grafana/v11.3/setup-grafana/start-restart-grafana/) for specific instructions based on your operating system.

The Unity panel plugin should now be available as a panel type in Grafana.

  </TabItem>
</Tabs>

## Connect

This plugin **does not require any additional configuration to connect**. It should be available in Grafana's plugin list and as an option in the plugin types when creating a panel in a dashboard.