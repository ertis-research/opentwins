"use strict";(self.webpackChunkdocs=self.webpackChunkdocs||[]).push([[3329],{3905:(e,t,n)=>{n.d(t,{Zo:()=>p,kt:()=>h});var a=n(7294);function o(e,t,n){return t in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function i(e,t){var n=Object.keys(e);if(Object.getOwnPropertySymbols){var a=Object.getOwnPropertySymbols(e);t&&(a=a.filter((function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable}))),n.push.apply(n,a)}return n}function r(e){for(var t=1;t<arguments.length;t++){var n=null!=arguments[t]?arguments[t]:{};t%2?i(Object(n),!0).forEach((function(t){o(e,t,n[t])})):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(n)):i(Object(n)).forEach((function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(n,t))}))}return e}function s(e,t){if(null==e)return{};var n,a,o=function(e,t){if(null==e)return{};var n,a,o={},i=Object.keys(e);for(a=0;a<i.length;a++)n=i[a],t.indexOf(n)>=0||(o[n]=e[n]);return o}(e,t);if(Object.getOwnPropertySymbols){var i=Object.getOwnPropertySymbols(e);for(a=0;a<i.length;a++)n=i[a],t.indexOf(n)>=0||Object.prototype.propertyIsEnumerable.call(e,n)&&(o[n]=e[n])}return o}var l=a.createContext({}),c=function(e){var t=a.useContext(l),n=t;return e&&(n="function"==typeof e?e(t):r(r({},t),e)),n},p=function(e){var t=c(e.components);return a.createElement(l.Provider,{value:t},e.children)},d="mdxType",u={inlineCode:"code",wrapper:function(e){var t=e.children;return a.createElement(a.Fragment,{},t)}},m=a.forwardRef((function(e,t){var n=e.components,o=e.mdxType,i=e.originalType,l=e.parentName,p=s(e,["components","mdxType","originalType","parentName"]),d=c(n),m=o,h=d["".concat(l,".").concat(m)]||d[m]||u[m]||i;return n?a.createElement(h,r(r({ref:t},p),{},{components:n})):a.createElement(h,r({ref:t},p))}));function h(e,t){var n=arguments,o=t&&t.mdxType;if("string"==typeof e||o){var i=n.length,r=new Array(i);r[0]=m;var s={};for(var l in t)hasOwnProperty.call(t,l)&&(s[l]=t[l]);s.originalType=e,s[d]="string"==typeof e?e:o,r[1]=s;for(var c=2;c<i;c++)r[c]=n[c];return a.createElement.apply(null,r)}return a.createElement.apply(null,n)}m.displayName="MDXCreateElement"},5162:(e,t,n)=>{n.d(t,{Z:()=>r});var a=n(7294),o=n(6010);const i={tabItem:"tabItem_Ymn6"};function r(e){let{children:t,hidden:n,className:r}=e;return a.createElement("div",{role:"tabpanel",className:(0,o.Z)(i.tabItem,r),hidden:n},t)}},5488:(e,t,n)=>{n.d(t,{Z:()=>u});var a=n(7462),o=n(7294),i=n(6010),r=n(2389),s=n(7392),l=n(7094),c=n(2466);const p={tabList:"tabList__CuJ",tabItem:"tabItem_LNqP"};function d(e){const{lazy:t,block:n,defaultValue:r,values:d,groupId:u,className:m}=e,h=o.Children.map(e.children,(e=>{if((0,o.isValidElement)(e)&&"value"in e.props)return e;throw new Error(`Docusaurus error: Bad <Tabs> child <${"string"==typeof e.type?e.type:e.type.name}>: all children of the <Tabs> component should be <TabItem>, and every <TabItem> should have a unique "value" prop.`)})),f=d??h.map((e=>{let{props:{value:t,label:n,attributes:a}}=e;return{value:t,label:n,attributes:a}})),g=(0,s.l)(f,((e,t)=>e.value===t.value));if(g.length>0)throw new Error(`Docusaurus error: Duplicate values "${g.map((e=>e.value)).join(", ")}" found in <Tabs>. Every value needs to be unique.`);const k=null===r?r:r??h.find((e=>e.props.default))?.props.value??h[0].props.value;if(null!==k&&!f.some((e=>e.value===k)))throw new Error(`Docusaurus error: The <Tabs> has a defaultValue "${k}" but none of its children has the corresponding value. Available values are: ${f.map((e=>e.value)).join(", ")}. If you intend to show no default tab, use defaultValue={null} instead.`);const{tabGroupChoices:v,setTabGroupChoices:b}=(0,l.U)(),[y,N]=(0,o.useState)(k),w=[],{blockElementScrollPositionUntilNextRender:_}=(0,c.o5)();if(null!=u){const e=v[u];null!=e&&e!==y&&f.some((t=>t.value===e))&&N(e)}const T=e=>{const t=e.currentTarget,n=w.indexOf(t),a=f[n].value;a!==y&&(_(t),N(a),null!=u&&b(u,String(a)))},E=e=>{let t=null;switch(e.key){case"Enter":T(e);break;case"ArrowRight":{const n=w.indexOf(e.currentTarget)+1;t=w[n]??w[0];break}case"ArrowLeft":{const n=w.indexOf(e.currentTarget)-1;t=w[n]??w[w.length-1];break}}t?.focus()};return o.createElement("div",{className:(0,i.Z)("tabs-container",p.tabList)},o.createElement("ul",{role:"tablist","aria-orientation":"horizontal",className:(0,i.Z)("tabs",{"tabs--block":n},m)},f.map((e=>{let{value:t,label:n,attributes:r}=e;return o.createElement("li",(0,a.Z)({role:"tab",tabIndex:y===t?0:-1,"aria-selected":y===t,key:t,ref:e=>w.push(e),onKeyDown:E,onClick:T},r,{className:(0,i.Z)("tabs__item",p.tabItem,r?.className,{"tabs__item--active":y===t})}),n??t)}))),t?(0,o.cloneElement)(h.filter((e=>e.props.value===y))[0],{className:"margin-top--md"}):o.createElement("div",{className:"margin-top--md"},h.map(((e,t)=>(0,o.cloneElement)(e,{key:t,hidden:e.props.value!==y})))))}function u(e){const t=(0,r.Z)();return o.createElement(d,(0,a.Z)({key:String(t)},e))}},1320:(e,t,n)=>{n.r(t),n.d(t,{assets:()=>p,contentTitle:()=>l,default:()=>h,frontMatter:()=>s,metadata:()=>c,toc:()=>d});var a=n(7462),o=(n(7294),n(3905)),i=n(5488),r=n(5162);const s={sidebar_position:1},l="DT definition and monitoring (required)",c={unversionedId:"installation/manual/essential",id:"installation/manual/essential",title:"DT definition and monitoring (required)",description:"This component is the essential functionality of OpenTwins and is required for the system to function properly. Regardless of your specific use case or configuration, it must be installed as a prerequisite. Please ensure that this component is installed correctly before proceeding with the configuration.",source:"@site/docs/installation/manual/essential.md",sourceDirName:"installation/manual",slug:"/installation/manual/essential",permalink:"/opentwins/docs/installation/manual/essential",draft:!1,editUrl:"https://github.com/facebook/docusaurus/tree/main/packages/create-docusaurus/templates/shared/docs/installation/manual/essential.md",tags:[],version:"current",sidebarPosition:1,frontMatter:{sidebar_position:1},sidebar:"tutorialSidebar",previous:{title:"Manual",permalink:"/opentwins/docs/installation/manual/"},next:{title:"Composition (recommended)",permalink:"/opentwins/docs/installation/manual/composition"}},p={},d=[{value:"Prerequisites",id:"prerequisites",level:2},{value:"Deploy",id:"deploy",level:2},{value:"MongoDB v6.0",id:"mongodb-v60",level:3},{value:"Eclipse Ditto v3.3",id:"eclipse-ditto-v33",level:3},{value:"InfluxDB v2",id:"influxdb-v2",level:3},{value:"Mosquitto v2.0",id:"mosquitto-v20",level:3},{value:"Apache Kafka v3.4",id:"apache-kafka-v34",level:3},{value:"Grafana v9.5",id:"grafana-v95",level:3},{value:"Eclipse Hono v2.4",id:"eclipse-hono-v24",level:3},{value:"Connect",id:"connect",level:2},{value:"Eclipse Ditto and InfluxDB",id:"eclipse-ditto-and-influxdb",level:3},{value:"InfluxDB and Grafana",id:"influxdb-and-grafana",level:3},{value:"Eclipse Ditto and Eclipse Hono",id:"eclipse-ditto-and-eclipse-hono",level:3}],u={toc:d},m="wrapper";function h(e){let{components:t,...s}=e;return(0,o.kt)(m,(0,a.Z)({},u,s,{components:t,mdxType:"MDXLayout"}),(0,o.kt)("h1",{id:"dt-definition-and-monitoring-required"},"DT definition and monitoring (required)"),(0,o.kt)("p",null,(0,o.kt)("strong",{parentName:"p"},"This component is the essential functionality of OpenTwins and is required for the system to function properly"),". Regardless of your specific use case or configuration, it must be installed as a prerequisite. Please ensure that this component is installed correctly before proceeding with the configuration."),(0,o.kt)("h2",{id:"prerequisites"},"Prerequisites"),(0,o.kt)("p",null,"Before you begin, ensure you have the following:"),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},"Container manager: Currently tested on ",(0,o.kt)("a",{parentName:"li",href:"https://www.docker.com/"},"Docker")," and ",(0,o.kt)("a",{parentName:"li",href:"https://containerd.io/"},"ContainerD"),"."),(0,o.kt)("li",{parentName:"ul"},"Access to a ",(0,o.kt)("a",{parentName:"li",href:"https://kubernetes.io/releases/download/"},"Kubernetes")," (recommended) or ",(0,o.kt)("a",{parentName:"li",href:"https://k3s.io/"},"K3s")," cluster."),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("inlineCode",{parentName:"li"},"kubectl")," installed and configured."),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://helm.sh/docs/intro/install/"},"Helm")," version 16.14 or above.")),(0,o.kt)("h2",{id:"deploy"},"Deploy"),(0,o.kt)("admonition",{type:"tip"},(0,o.kt)("p",{parentName:"admonition"},"Note that the values files have the variables that we recommend for the installation of each Helm Chart, but they ",(0,o.kt)("strong",{parentName:"p"},"can be extended or modified according to your needs")," (please consult the Helm Chart documentation for each component).")),(0,o.kt)("p",null,"We recommend installing all components in the same Kubernetes namespace to make it easier to identify and control them all. In our case the namespace will be ",(0,o.kt)("em",{parentName:"p"},"opentwins"),"."),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"kubectl create namespace opentwins\n")),(0,o.kt)("p",null,"We installed all the components with their Helm versions and kept most of the values in their default configuration, except for those that are important for the interconnection of the components. In addition, we configure the services as NodePort to facilitate external access and set a specific port for each one. "),(0,o.kt)("admonition",{type:"warning"},(0,o.kt)("p",{parentName:"admonition"},"Depending on how you have persistence configured in your cluster, you may need to deploy ",(0,o.kt)("a",{parentName:"p",href:"https://kubernetes.io/docs/concepts/storage/persistent-volumes/"},"persistent volumes")," for MongoDB, InfluxDB and Grafana. The values for MongoDB are shown below, but they all follow the same template."),(0,o.kt)("pre",{parentName:"admonition"},(0,o.kt)("code",{parentName:"pre",className:"language-yaml"},"apiVersion: v1\nkind: PersistentVolume\nmetadata:\n  name: pv-opentwins-mongodb\nspec:\n  accessModes:\n    - ReadWriteOnce\n  capacity:\n    storage: 8Gi\n  hostPath:\n    path: /mnt/opentwins/mongodb\n    type: DirectoryOrCreate\n"))),(0,o.kt)("p",null,"Listed below are the essential components of the ",(0,o.kt)("a",{parentName:"p",href:"/opentwins/docs/overview/architecture"},"architecture")," along with their versions used, their Helm values and a link to the repository explaining their installation."),(0,o.kt)("h3",{id:"mongodb-v60"},"MongoDB v6.0"),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://www.mongodb.com/docs/v6.0/introduction/"},"App v6.0 documentation")),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://github.com/bitnami/charts/tree/main/bitnami/mongodb"},"Helm documentation"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm install mongodb -n opentwins oci://registry-1.docker.io/bitnamicharts/mongodb --version 13.8.3 -f values.yaml\n")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},"service:\n  type: NodePort\n  nodePorts:\n    mongodb: 30717\npersistence:\n  enabled: true\nvolumePermissions:\n  enabled: true\nauth:\n  enabled: false\n")),(0,o.kt)("h3",{id:"eclipse-ditto-v33"},"Eclipse Ditto v3.3"),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://eclipse.dev/ditto/3.3/intro-overview.html"},"App v3.3 documentation")),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://github.com/eclipse-ditto/ditto/tree/master/deployment/helm/ditto"},"Helm documentation"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm install --dependency-update -n opentwins ditto oci://registry-1.docker.io/eclipse/ditto --version 3.3.7 --wait -f values.yaml\n")),(0,o.kt)("admonition",{type:"warning"},(0,o.kt)("ul",{parentName:"admonition"},(0,o.kt)("li",{parentName:"ul"},"We advise not to modify any authentication configuration due to a bug in Eclipse Ditto that may cause access errors."),(0,o.kt)("li",{parentName:"ul"},"In the following values you have to replace ",(0,o.kt)("em",{parentName:"li"},"mongodb-service-name")," by the MongoDB service name"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},"global:\n  hashedBasicAuthUsers: false\n  basicAuthUsers:\n    ditto:\n      user: ditto\n      password: ditto\n    devops:\n      user: devops\n      password: foobar\nnginx:\n  service:\n    type: NodePort\n    nodePort: 30525\nswaggerui:\n  enabled: false\ndittoui:\n  enabled: false\nmongodb:\n  enabled: false\ndbconfig:\n  policies:\n    uri: 'mongodb://<mongodb-service-name>:27017/ditto'\n  things:\n    uri: 'mongodb://<mongodb-service-name>:27017/ditto'\n  connectivity:\n    uri: 'mongodb://<mongodb-service-name>:27017/ditto'\n  thingsSearch:\n    uri: 'mongodb://<mongodb-service-name>:27017/ditto'\ngateway:\n  config:\n    authentication:\n      enablePreAuthentication: true\n      devops:\n        devopsPassword: foobar\n        statusPassword: foobar\n")),(0,o.kt)("h3",{id:"influxdb-v2"},"InfluxDB v2"),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://docs.influxdata.com/influxdb/v2/"},"App v2 documentation")),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://github.com/influxdata/helm-charts/tree/master/charts/influxdb2"},"Helm documentation"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm repo add influxdata https://helm.influxdata.com/\nhelm repo update\nhelm install -n opentwins influxdb influxdata/influxdb2 --version 2.1.1 -f values.yaml\n")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},"persistence:\n  enabled: true\nservice:\n  type: NodePort\n  nodePort: 30716\nimage:\n  pullPolicy: Always\n")),(0,o.kt)("h3",{id:"mosquitto-v20"},"Mosquitto v2.0"),(0,o.kt)("admonition",{type:"tip"},(0,o.kt)("p",{parentName:"admonition"},"OpenTwins supports the use of Mosquitto and Kafka as intermediaries, but ",(0,o.kt)("strong",{parentName:"p"},"we recommend using Mosquitto")," due to its simpler configuration. Since there is no official Helm chart for Mosquitto, we have created one of our own that works fine, although there is no documentation yet. However, you can install Mosquitto in any of the ",(0,o.kt)("a",{parentName:"p",href:"https://mosquitto.org/download/"},"available ways"),".")),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://mosquitto.org/documentation/"},"App documentation")),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://github.com/ertis-research/Helm-charts/blob/main/mosquitto/values.yaml"},"Helm values file"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm repo add ertis https://ertis-research.github.io/Helm-charts/\nhelm repo update\nhelm install mosquitto ertis/mosquitto -n opentwins --wait --dependency-update -f values.yaml\n")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},"service:\n  type: NodePort\n  nodePort: 30511\nconfiguration:\n  authentication:\n    enabled: false\n")),(0,o.kt)("h3",{id:"apache-kafka-v34"},"Apache Kafka v3.4"),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://kafka.apache.org/34/documentation.html"},"App v3.4 documentation")),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://github.com/bitnami/charts/tree/main/bitnami/kafka"},"Helm documentation"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm install kafka oci://registry-1.docker.io/bitnamicharts/kafka --version 22.0.0 -f values.yaml\n")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},"autoCreateTopicsEnable: true\n")),(0,o.kt)("h3",{id:"grafana-v95"},"Grafana v9.5"),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://grafana.com/docs/grafana/v9.5/"},"App v9.5.1 documentation")),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://github.com/grafana/helm-charts/tree/main/charts/grafana"},"Helm documentation"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm repo add grafana https://grafana.github.io/helm-charts\nhelm repo update\nhelm install grafana grafana/grafana -n opentwins --version 6.56.1 -f values.yaml\n")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},"persistence:\n  enabled: true\nservice:\n  type: NodePort\n  nodePort: 30718\ngrafana.ini:\n  plugins:\n    plugin_admin_enabled: true\n    allow_loading_unsigned_plugins: ertis-opentwins,ertis-unity-panel\nextraInitContainers:\n- name: install-opentwins-plugins\n  image: busybox\n  command:\n    - /bin/sh\n    - -c\n    - |\n      #!/bin/sh\n      set -euo pipefail\n      mkdir -p /grafana-storage/plugins\n      cd /grafana-storage/plugins\n      wget --no-check-certificate -O ertis-opentwins.zip https://github.com/ertis-research/opentwins-in-grafana/releases/download/latest/ertis-opentwins.zip\n      unzip -o ertis-opentwins.zip\n      rm ertis-opentwins.zip\n      wget --no-check-certificate -O ertis-unity-panel.zip https://github.com/ertis-research/grafana-panel-unity/releases/download/latest/ertis-unity-panel.zip\n      unzip -o ertis-unity-panel.zip\n      rm ertis-unity-panel.zip\n  volumeMounts:\n  - name: storage\n    mountPath: /grafana-storage\n")),(0,o.kt)("h3",{id:"eclipse-hono-v24"},"Eclipse Hono v2.4"),(0,o.kt)("admonition",{type:"warning"},(0,o.kt)("p",{parentName:"admonition"},"This component is completely optional. We maintain support for its connection to OpenTwins, but ",(0,o.kt)("strong",{parentName:"p"},"we do not recommend its use"),". For a large number of devices or messages it increases considerably the latency of the platform.")),(0,o.kt)("ul",null,(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://eclipse.dev/hono/docs/2.4/"},"App v2.4 documentation")),(0,o.kt)("li",{parentName:"ul"},(0,o.kt)("a",{parentName:"li",href:"https://github.com/eclipse/packages/tree/master/charts/hono"},"Helm documentation"))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm repo add eclipse-iot https://eclipse.org/packages/charts\nhelm repo update\nhelm install hono eclipse-iot/hono -n opentwins -f values.yaml --version=2.5.5\n")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},"prometheus:\n  createInstance: false\ngrafana:\n  enabled: false\nuseLoadBalancer: false\nprobes:\n  livenessProbe:\n    initialDelaySeconds: 900\n  readinessProbe:\n    initialDelaySeconds: 45\nmessagingNetworkTypes:\n  - amqp\nkafkaMessagingClusterExample:\n  enabled: false\namqpMessagingNetworkExample:\n  enabled: true\ndeviceRegistryExample:\n  type: mongodb\n  addExampleData: false\n  mongoDBBasedDeviceRegistry:\n    mongodb:\n      host: '{{ .Release.Name }}-mongodb'\n      port: 27017\n      dbName: hono\n  hono:\n    registry:\n      http:\n        insecurePortEnabled: true\nadapters:\n  mqtt:\n    hono:\n      mqtt:\n        insecurePortEnabled: true\n  http:\n    hono:\n      http:\n        insecurePortEnabled: true\n  amqp:\n    hono:\n      amqp:\n        insecurePortEnabled: true\n\n")),(0,o.kt)("h2",{id:"connect"},"Connect"),(0,o.kt)("admonition",{type:"tip"},(0,o.kt)("p",{parentName:"admonition"},"Check ",(0,o.kt)("a",{parentName:"p",href:"/opentwins/docs/overview/architecture"},"architecture")," to see which connections you need to set up")),(0,o.kt)("h3",{id:"eclipse-ditto-and-influxdb"},"Eclipse Ditto and InfluxDB"),(0,o.kt)("p",null,"The process to connect Eclipse Ditto and InfluxDB will depend on Mosquitto or Apache Kafka. Choose the option you have selected in each step."),(0,o.kt)("ol",null,(0,o.kt)("li",{parentName:"ol"},(0,o.kt)("p",{parentName:"li"},"You have to add an output connection in Eclipse Ditto that publishes the events of the twins in the intermediary. This is done with a ",(0,o.kt)("inlineCode",{parentName:"p"},"POST")," request to the URL ",(0,o.kt)("inlineCode",{parentName:"p"},"http://DITTO_NGINX_URL/api/2/connections")," with the following body and the basic credentials: user ",(0,o.kt)("em",{parentName:"p"},'"devops"')," and password ",(0,o.kt)("em",{parentName:"p"},'"foobar"'),". Remember to replace ",(0,o.kt)("strong",{parentName:"p"},"DITTO_NGINX_URL")," by a URL that allows access to the Eclipse Ditto Nginx service, you can check how to do it ",(0,o.kt)("a",{parentName:"p",href:"https://kubernetes.io/docs/tasks/access-application-cluster/service-access-application-cluster/"},"here"),"."),(0,o.kt)("p",{parentName:"li"},"You can check if the connection is working properly by reading the ",(0,o.kt)("em",{parentName:"p"},"opentwins")," topic in the selected broker with some tool or script and sending updates to some twin in ",(0,o.kt)("a",{parentName:"p",href:"https://eclipse.dev/ditto/protocol-overview.html"},"Ditto Protocol")," format. To create the twin check ",(0,o.kt)("a",{parentName:"p",href:"https://eclipse.dev/ditto/http-api-doc.html#/Things/put_api_2_things__thingId_"},"here")," and to see an example of an update message check ",(0,o.kt)("a",{parentName:"p",href:"https://eclipse.dev/ditto/protocol-examples-modifyfeatures.html#modifyfeatures"},"here"),"."))),(0,o.kt)(i.Z,{groupId:"intermediary",mdxType:"Tabs"},(0,o.kt)(r.Z,{value:"mosquitto",label:"Mosquitto",default:!0,mdxType:"TabItem"},(0,o.kt)("admonition",{type:"warning"},(0,o.kt)("p",{parentName:"admonition"},"Change ",(0,o.kt)("strong",{parentName:"p"},"MOSQUITTO_SERVICE_NAME")," to the name of the Mosquitto service. You can check it with ",(0,o.kt)("inlineCode",{parentName:"p"},"kubectl get services"),".")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-json",metastring:'title="PUT http://DITTO_NGINX_URL/api/2/connections"',title:'"PUT','http://DITTO_NGINX_URL/api/2/connections"':!0},'{\n  "name": "mosquitto-target-connection",\n  "connectionType": "mqtt-5",\n  "connectionStatus": "open",\n  "uri": "tcp://MOSQUITTO_SERVICE_NAME:1883",\n  "clientCount": 1,\n  "failoverEnabled": true,\n  "sources": [],\n  "targets": [\n    {\n      "address": "opentwins/{{ topic:channel }}/{{ topic:criterion }}/{{ thing:namespace }}/{{ thing:name }}",\n      "topics": [\n        "_/_/things/twin/events?extraFields=thingId,attributes/_parents,features/idSimulationRun/properties/value",\n        "_/_/things/live/messages",\n        "_/_/things/live/commands"\n      ],\n      "qos": 1,\n      "authorizationContext": [\n        "nginx:ditto"\n      ]\n    }\n  ]\n}\n'))),(0,o.kt)(r.Z,{value:"kafka",label:"Apache Kafka",mdxType:"TabItem"},(0,o.kt)("admonition",{type:"warning"},(0,o.kt)("p",{parentName:"admonition"},"Change ",(0,o.kt)("strong",{parentName:"p"},"KAFKA_SERVICE_NAME")," to the name of the Apache Kafka service. You can check it with ",(0,o.kt)("inlineCode",{parentName:"p"},"kubectl get services"),".")),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-json",metastring:'title="PUT http://DITTO_NGINX_URL/api/2/connections"',title:'"PUT','http://DITTO_NGINX_URL/api/2/connections"':!0},'{\n  "name": "kafka-target-connection",\n  "connectionType": "kafka",\n  "connectionStatus": "open",\n  "uri": "tcp://KAFKA_SERVICE_NAME:9092",\n  "specificConfig": {\n      "bootstrapServers": "KAFKA_SERVICE_NAME:9092",\n      "saslMechanism": "plain"\n  },\n  "failoverEnabled": true,\n  "sources": [],\n  "targets": [\n    {\n      "address": "opentwins",\n      "topics": [\n        "_/_/things/twin/events?extraFields=thingId,attributes/_parents,features/idSimulationRun/properties/value",\n        "_/_/things/live/messages",\n        "_/_/things/live/commands"\n      ],\n      "authorizationContext": [\n        "nginx:ditto"\n      ]\n    }\n  ]\n}\n')))),(0,o.kt)("ol",{start:2},(0,o.kt)("li",{parentName:"ol"},(0,o.kt)("p",{parentName:"li"},"Now we will need to obtain a token in InfluxDB with write permissions. We will then access from a browser to the InfluxDB interface and ",(0,o.kt)("a",{parentName:"p",href:"https://docs.influxdata.com/influxdb/v2/admin/organizations/create-org/"},"create an ",(0,o.kt)("em",{parentName:"a"},"opentwins")," organization"),". Then, follow the instructions in their documentation to ",(0,o.kt)("a",{parentName:"p",href:"https://docs.influxdata.com/influxdb/v2/admin/tokens/create-token/"},"create an API token")," in the organization. Save this token because we will use it next.")),(0,o.kt)("li",{parentName:"ol"},(0,o.kt)("p",{parentName:"li"},"An instance of Telegraf must be deployed to read the events written in the intermediary broker and write them to the database. For this we will use the Telegraf Helm and add the necessary configuration in its values. You can check to Telegraf v1 documentation for both the ",(0,o.kt)("a",{parentName:"p",href:"https://docs.influxdata.com/telegraf/v1/"},"application")," and ",(0,o.kt)("a",{parentName:"p",href:"https://github.com/influxdata/helm-charts/tree/master/charts/telegraf"},"Helm")," for more information."),(0,o.kt)("p",{parentName:"li"},"The commands to deploy it are the following, using the necessary values file in each case."))),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"helm repo add influxdata https://helm.influxdata.com/\nhelm repo update\nhelm install -n opentwins telegraf influxdata/telegraf -f values.yaml --version=1.8.27 --set tplVersion=2\n")),(0,o.kt)(i.Z,{groupId:"intermediary",mdxType:"Tabs"},(0,o.kt)(r.Z,{value:"mosquitto",label:"Mosquitto",default:!0,mdxType:"TabItem"},(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},'service:\n  enabled: false\nconfig:\n  agent:\n    debug: true\n  processors:\n    - rename:\n        replace:\n         - tag: "extra_attributes__parents"\n           dest: "parent"         \n         - tag: "headers_ditto-originator"\n           dest: "originator"\n         - tag: "extra_features_idSimulationRun_properties_value"\n           dest: "idSimulationRun"\n         - tag: "extra_thingId"\n           dest: "thingId"\n  outputs:\n    - influxdb_v2:\n        urls:\n          - "http://INFLUX_SERVICE_NAME:INFLUX_PORT"\n        token: "INFLUXDB_TOKEN"\n        organization: "opentwins"\n        bucket: "default"\n  inputs:\n    - mqtt_consumer:\n        servers:\n          - "tcp://MOSQUITTO_SERVICE_NAME:1883"\n        topics:\n          - "opentwins/#"\n        qos: 1\n        tag_keys:\n          - "extra_attributes__parents"\n          - "extra_thingId"\n          - "headers_ditto-originator"\n          - "extra_features_idSimulationRun_properties_value"\n          - "value_time_properties_value"   \n        data_format: "json"\nmetrics:\n  internal:\n    enabled: false\n'))),(0,o.kt)(r.Z,{value:"kafka",label:"Apache Kafka",mdxType:"TabItem"},(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-yaml",metastring:'title="values.yaml"',title:'"values.yaml"'},'service:\n  enabled: false\nconfig:\n  agent:\n    debug: true\n  processors:\n    - rename:\n        replace:\n         - tag: "extra_attributes__parents"\n           dest: "parent"         \n         - tag: "headers_ditto-originator"\n           dest: "originator"\n         - tag: "extra_features_idSimulationRun_properties_value"\n           dest: "idSimulationRun"\n         - tag: "extra_thingId"\n           dest: "thingId"\n  outputs:\n    - influxdb_v2:\n        urls:\n          - "http://INFLUX_SERVICE_NAME:INFLUX_PORT"\n        token: "INFLUXDB_TOKEN"\n        organization: "opentwins"\n        bucket: "default"\n  inputs:\n    - kafka_consumer:\n        brokers:\n          - "KAFKA_SERVICE_NAME:9092"\n        topics:\n          - "opentwins"\n        tag_keys:\n          - "extra_attributes__parents"\n          - "extra_thingId"\n          - "headers_ditto-originator"\n          - "extra_features_idSimulationRun_properties_value"\n          - "value_time_properties_value"   \n        data_format: "json"\nmetrics:\n  internal:\n    enabled: false\n')))),(0,o.kt)("p",null,"With this Eclipse Ditto and InfluxDB should be connected. You can check this by sending update messages to Eclipse Ditto and verifying if they are correctly written to the InfluxDB bucket. If not, check if the messages are arriving correctly to the intermediate broker and, if so, check the logs of the Telegraf pod to see if there is any error in the configuration (usually connection problems)."),(0,o.kt)("h3",{id:"influxdb-and-grafana"},"InfluxDB and Grafana"),(0,o.kt)("ol",null,(0,o.kt)("li",{parentName:"ol"},"Obtain a ",(0,o.kt)("a",{parentName:"li",href:"https://docs.influxdata.com/influxdb/v2/admin/tokens/create-token/"},"read access token in InfluxDB")," for Grafana."),(0,o.kt)("li",{parentName:"ol"},"Access ",(0,o.kt)("inlineCode",{parentName:"li"},"Configuration > Data sources")," on the Grafana interface and click on ",(0,o.kt)("em",{parentName:"li"},"Add data source"),"."),(0,o.kt)("li",{parentName:"ol"},"Select ",(0,o.kt)("em",{parentName:"li"},"InfluxDB")," from the list. In the setup form it is very important to select ",(0,o.kt)("em",{parentName:"li"},"Flux")," as query language. It will be necessary to fill in the URL section with the one that corresponds to InfluxDB service. You will also have to activate ",(0,o.kt)("em",{parentName:"li"},"Auth Basic")," and fill in the fields (in our case we have set the default admin of InfluxDB, but you can create a new user and fill in these fields). In the InfluxDB details you should indicate the organization, the bucket (default is ",(0,o.kt)("em",{parentName:"li"},"default"),") and the token you have generated. "),(0,o.kt)("li",{parentName:"ol"},"When saving and testing, it should come out that at least one bucket has been found, indicating that they are already connected.")),(0,o.kt)("h3",{id:"eclipse-ditto-and-eclipse-hono"},"Eclipse Ditto and Eclipse Hono"),(0,o.kt)("p",null,"In the following diagram you can see how Eclipse Hono and Eclipse Ditto are related in OpenTwins. "),(0,o.kt)("center",null,(0,o.kt)("img",{src:n(2728).Z,alt:"Ditto and Hono relationship",style:{width:500}})),(0,o.kt)("p",null,"Basically, you will need to ",(0,o.kt)("strong",{parentName:"p"},"create a connection between both for each Eclipse Hono tenant you want to use"),". ",(0,o.kt)("a",{parentName:"p",href:"https://www.eclipse.org/hono/docs/concepts/tenancy/"},"Tenants")," basically act as device containers, so you could simply create a single tenant connected to Eclipse Ditto and store all the devices you need there. In this case we will do it this way, but you could create as many tenants and connections as your needs require."),(0,o.kt)("p",null,"The first thing to do is to check the IPs and ports to use with ",(0,o.kt)("inlineCode",{parentName:"p"},"kubectl get services -n $NS"),". At this point we are interested in the ",(0,o.kt)("em",{parentName:"p"},"dt-service-device-registry-ext")," and ",(0,o.kt)("em",{parentName:"p"},"dt-ditto-nginx")," services, which correspond to Eclipse Hono and Eclipse Ditto respectively (if you have followed these instructions and services are NodePort, you will have to use port 3XXXX). "),(0,o.kt)("p",null,"We will then create a Hono tenant called, for example, ditto (you must override the variable ",(0,o.kt)("strong",{parentName:"p"},"HONO_TENANT")," if you have chosen another name)."),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},"HONO_TENANT=ditto\ncurl -i -X POST http://$HONO_IP:$HONO_PORT/v1/tenants/$HONO_TENANT\n")),(0,o.kt)("p",null,"Now we will create the connection from Eclipse Ditto, which will act as a consumer of the AMQP endpoint of that tenant. To do this you will need to know the Eclipse Ditto devops password with the following command (the variable ",(0,o.kt)("strong",{parentName:"p"},"RELEASE")," is the name we gave to the Helm release when installing cloud2edge, if you have followed these instructions it should be dt)."),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},'RELEASE=dt\nDITTO_DEVOPS_PWD=$(kubectl --namespace ${NS} get secret ${RELEASE}-ditto-gateway-secret -o jsonpath="{.data.devops-password}" | base64 --decode)\n')),(0,o.kt)("p",null,"Now we ",(0,o.kt)("a",{parentName:"p",href:"https://www.eclipse.org/ditto/connectivity-manage-connections.html#create-connection"},"create the connection from Eclipse Ditto")," with the following command."),(0,o.kt)("pre",null,(0,o.kt)("code",{parentName:"pre",className:"language-bash"},'curl -i -X POST -u devops:${DITTO_DEVOPS_PWD} -H \'Content-Type: application/json\' --data \'{\n  "targetActorSelection": "/system/sharding/connection",\n  "headers": {\n    "aggregate": false\n  },\n  "piggybackCommand": {\n    "type": "connectivity.commands:createConnection",\n    "connection": {\n      "id": "hono-connection-for-\'"${HONO_TENANT}"\'",\n      "connectionType": "amqp-10",\n      "connectionStatus": "open",\n      "uri": "amqp://consumer%40HONO:verysecret@\'"${RELEASE}"\'-dispatch-router-ext:15672",\n      "failoverEnabled": true,\n      "sources": [\n        {\n          "addresses": [\n            "telemetry/\'"${HONO_TENANT}"\'",\n            "event/\'"${HONO_TENANT}"\'"\n          ],\n          "authorizationContext": [\n            "pre-authenticated:hono-connection"\n          ],\n          "enforcement": {\n            "input": "{{ header:device_id }}",\n            "filters": [\n              "{{ entity:id }}"\n            ]\n          },\n          "headerMapping": {\n            "hono-device-id": "{{ header:device_id }}",\n            "content-type": "{{ header:content-type }}"\n          },\n          "replyTarget": {\n            "enabled": true,\n            "address": "{{ header:reply-to }}",\n            "headerMapping": {\n              "to": "command/\'"${HONO_TENANT}"\'/{{ header:hono-device-id }}",\n              "subject": "{{ header:subject | fn:default(topic:action-subject) | fn:default(topic:criterion) }}-response",\n              "correlation-id": "{{ header:correlation-id }}",\n              "content-type": "{{ header:content-type | fn:default(\'"\'"\'application/vnd.eclipse.ditto+json\'"\'"\') }}"\n            },\n            "expectedResponseTypes": [\n              "response",\n              "error"\n            ]\n          },\n          "acknowledgementRequests": {\n            "includes": [],\n            "filter": "fn:filter(header:qos,\'"\'"\'ne\'"\'"\',\'"\'"\'0\'"\'"\')"\n          }\n        },\n        {\n          "addresses": [\n            "command_response/\'"${HONO_TENANT}"\'/replies"\n          ],\n          "authorizationContext": [\n            "pre-authenticated:hono-connection"\n          ],\n          "headerMapping": {\n            "content-type": "{{ header:content-type }}",\n            "correlation-id": "{{ header:correlation-id }}",\n            "status": "{{ header:status }}"\n          },\n          "replyTarget": {\n            "enabled": false,\n            "expectedResponseTypes": [\n              "response",\n              "error"\n            ]\n          }\n        }\n      ],\n      "targets": [\n        {\n          "address": "command/\'"${HONO_TENANT}"\'",\n          "authorizationContext": [\n            "pre-authenticated:hono-connection"\n          ],\n          "topics": [\n            "_/_/things/live/commands",\n            "_/_/things/live/messages"\n          ],\n          "headerMapping": {\n            "to": "command/\'"${HONO_TENANT}"\'/{{ thing:id }}",\n            "subject": "{{ header:subject | fn:default(topic:action-subject) }}",\n            "content-type": "{{ header:content-type | fn:default(\'"\'"\'application/vnd.eclipse.ditto+json\'"\'"\') }}",\n            "correlation-id": "{{ header:correlation-id }}",\n            "reply-to": "{{ fn:default(\'"\'"\'command_response/\'"${HONO_TENANT}"\'/replies\'"\'"\') | fn:filter(header:response-required,\'"\'"\'ne\'"\'"\',\'"\'"\'false\'"\'"\') }}"\n          }\n        },\n        {\n          "address": "command/\'"${HONO_TENANT}"\'",\n          "authorizationContext": [\n            "pre-authenticated:hono-connection"\n          ],\n          "topics": [\n            "_/_/things/twin/events",\n            "_/_/things/live/events"\n          ],\n          "headerMapping": {\n            "to": "command/\'"${HONO_TENANT}"\'/{{ thing:id }}",\n            "subject": "{{ header:subject | fn:default(topic:action-subject) }}",\n            "content-type": "{{ header:content-type | fn:default(\'"\'"\'application/vnd.eclipse.ditto+json\'"\'"\') }}",\n            "correlation-id": "{{ header:correlation-id }}"\n          }\n        }\n      ]\n    }\n  }\n}\' http://$DITTO_IP:$DITTO_PORT/devops/piggyback/connectivity\n')),(0,o.kt)("p",null,"This connection is configured so that if an ",(0,o.kt)("a",{parentName:"p",href:"https://www.eclipse.org/hono/docs/concepts/device-identity/"},"Eclipse Hono device")," has the ",(0,o.kt)("a",{parentName:"p",href:"https://www.eclipse.org/ditto/basic-thing.html#thing-id"},"ThingId")," of an Eclipse Ditto twin as its identifier, its messages will be redirected to that twin directly (explained in more detail in the ",(0,o.kt)("a",{parentName:"p",href:"#usage"},"usage")," section)."),(0,o.kt)("p",null,(0,o.kt)("strong",{parentName:"p"},"Now you have all the essential OpenTwins functionality (DT definition and monitoring) working.")))}h.isMDXComponent=!0},2728:(e,t,n)=>{n.d(t,{Z:()=>a});const a=n.p+"assets/images/ditto-hono-relationship-05edfdb26b9df807a0860fad82192684.jpg"}}]);