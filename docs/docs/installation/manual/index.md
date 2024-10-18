---
sidebar_position: 2
---

import DocCardList from '@theme/DocCardList';

# Manual

:::warning

The documentation of this method is being written right now. We recommend using [helm installation](../using-helm.mdx).

:::

This section will explain how to deploy the platform manually. Basically, you will have to deploy or install the different components and then connect them. The procedure explained below is the one followed to deploy them in **Kubernetes** using in most cases the **Helm** option, but any other installation in which all the components are correctly installed and there is some kind of network between them to be able to communicate can be used. 

It is not necessary to deploy all components if not all functionalities are to be used. Check the [architecture](../../overview/architecture.md) section to find out which ones are essential and what functionality is covered by each of them.

Follow the instructions for the features you want to include in OpenTwins:

<DocCardList className="DocCardList--no-description"/>