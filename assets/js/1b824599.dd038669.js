"use strict";(self.webpackChunkdocs=self.webpackChunkdocs||[]).push([[3584],{3905:(e,t,n)=>{n.d(t,{Zo:()=>p,kt:()=>h});var a=n(7294);function r(e,t,n){return t in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function o(e,t){var n=Object.keys(e);if(Object.getOwnPropertySymbols){var a=Object.getOwnPropertySymbols(e);t&&(a=a.filter((function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable}))),n.push.apply(n,a)}return n}function i(e){for(var t=1;t<arguments.length;t++){var n=null!=arguments[t]?arguments[t]:{};t%2?o(Object(n),!0).forEach((function(t){r(e,t,n[t])})):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(n)):o(Object(n)).forEach((function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(n,t))}))}return e}function s(e,t){if(null==e)return{};var n,a,r=function(e,t){if(null==e)return{};var n,a,r={},o=Object.keys(e);for(a=0;a<o.length;a++)n=o[a],t.indexOf(n)>=0||(r[n]=e[n]);return r}(e,t);if(Object.getOwnPropertySymbols){var o=Object.getOwnPropertySymbols(e);for(a=0;a<o.length;a++)n=o[a],t.indexOf(n)>=0||Object.prototype.propertyIsEnumerable.call(e,n)&&(r[n]=e[n])}return r}var l=a.createContext({}),c=function(e){var t=a.useContext(l),n=t;return e&&(n="function"==typeof e?e(t):i(i({},t),e)),n},p=function(e){var t=c(e.components);return a.createElement(l.Provider,{value:t},e.children)},u="mdxType",d={inlineCode:"code",wrapper:function(e){var t=e.children;return a.createElement(a.Fragment,{},t)}},m=a.forwardRef((function(e,t){var n=e.components,r=e.mdxType,o=e.originalType,l=e.parentName,p=s(e,["components","mdxType","originalType","parentName"]),u=c(n),m=r,h=u["".concat(l,".").concat(m)]||u[m]||d[m]||o;return n?a.createElement(h,i(i({ref:t},p),{},{components:n})):a.createElement(h,i({ref:t},p))}));function h(e,t){var n=arguments,r=t&&t.mdxType;if("string"==typeof e||r){var o=n.length,i=new Array(o);i[0]=m;var s={};for(var l in t)hasOwnProperty.call(t,l)&&(s[l]=t[l]);s.originalType=e,s[u]="string"==typeof e?e:r,i[1]=s;for(var c=2;c<o;c++)i[c]=n[c];return a.createElement.apply(null,i)}return a.createElement.apply(null,n)}m.displayName="MDXCreateElement"},5162:(e,t,n)=>{n.d(t,{Z:()=>i});var a=n(7294),r=n(6010);const o={tabItem:"tabItem_Ymn6"};function i(e){let{children:t,hidden:n,className:i}=e;return a.createElement("div",{role:"tabpanel",className:(0,r.Z)(o.tabItem,i),hidden:n},t)}},5488:(e,t,n)=>{n.d(t,{Z:()=>d});var a=n(7462),r=n(7294),o=n(6010),i=n(2389),s=n(7392),l=n(7094),c=n(2466);const p={tabList:"tabList__CuJ",tabItem:"tabItem_LNqP"};function u(e){const{lazy:t,block:n,defaultValue:i,values:u,groupId:d,className:m}=e,h=r.Children.map(e.children,(e=>{if((0,r.isValidElement)(e)&&"value"in e.props)return e;throw new Error(`Docusaurus error: Bad <Tabs> child <${"string"==typeof e.type?e.type:e.type.name}>: all children of the <Tabs> component should be <TabItem>, and every <TabItem> should have a unique "value" prop.`)})),f=u??h.map((e=>{let{props:{value:t,label:n,attributes:a}}=e;return{value:t,label:n,attributes:a}})),b=(0,s.l)(f,((e,t)=>e.value===t.value));if(b.length>0)throw new Error(`Docusaurus error: Duplicate values "${b.map((e=>e.value)).join(", ")}" found in <Tabs>. Every value needs to be unique.`);const y=null===i?i:i??h.find((e=>e.props.default))?.props.value??h[0].props.value;if(null!==y&&!f.some((e=>e.value===y)))throw new Error(`Docusaurus error: The <Tabs> has a defaultValue "${y}" but none of its children has the corresponding value. Available values are: ${f.map((e=>e.value)).join(", ")}. If you intend to show no default tab, use defaultValue={null} instead.`);const{tabGroupChoices:g,setTabGroupChoices:k}=(0,l.U)(),[v,w]=(0,r.useState)(y),T=[],{blockElementScrollPositionUntilNextRender:O}=(0,c.o5)();if(null!=d){const e=g[d];null!=e&&e!==v&&f.some((t=>t.value===e))&&w(e)}const N=e=>{const t=e.currentTarget,n=T.indexOf(t),a=f[n].value;a!==v&&(O(t),w(a),null!=d&&k(d,String(a)))},x=e=>{let t=null;switch(e.key){case"Enter":N(e);break;case"ArrowRight":{const n=T.indexOf(e.currentTarget)+1;t=T[n]??T[0];break}case"ArrowLeft":{const n=T.indexOf(e.currentTarget)-1;t=T[n]??T[T.length-1];break}}t?.focus()};return r.createElement("div",{className:(0,o.Z)("tabs-container",p.tabList)},r.createElement("ul",{role:"tablist","aria-orientation":"horizontal",className:(0,o.Z)("tabs",{"tabs--block":n},m)},f.map((e=>{let{value:t,label:n,attributes:i}=e;return r.createElement("li",(0,a.Z)({role:"tab",tabIndex:v===t?0:-1,"aria-selected":v===t,key:t,ref:e=>T.push(e),onKeyDown:x,onClick:N},i,{className:(0,o.Z)("tabs__item",p.tabItem,i?.className,{"tabs__item--active":v===t})}),n??t)}))),t?(0,r.cloneElement)(h.filter((e=>e.props.value===v))[0],{className:"margin-top--md"}):r.createElement("div",{className:"margin-top--md"},h.map(((e,t)=>(0,r.cloneElement)(e,{key:t,hidden:e.props.value!==v})))))}function d(e){const t=(0,i.Z)();return r.createElement(u,(0,a.Z)({key:String(t)},e))}},2045:(e,t,n)=>{n.r(t),n.d(t,{assets:()=>p,contentTitle:()=>l,default:()=>h,frontMatter:()=>s,metadata:()=>c,toc:()=>u});var a=n(7462),r=(n(7294),n(3905)),o=n(5488),i=n(5162);const s={sidebar_position:1},l="Raspberry example",c={unversionedId:"getting-started/raspberry-example/raspberry-example",id:"getting-started/raspberry-example/raspberry-example",title:"Raspberry example",description:"Requirements",source:"@site/docs/getting-started/raspberry-example/raspberry-example.mdx",sourceDirName:"getting-started/raspberry-example",slug:"/getting-started/raspberry-example/",permalink:"/opentwins/docs/getting-started/raspberry-example/",draft:!1,editUrl:"https://github.com/facebook/docusaurus/tree/main/packages/create-docusaurus/templates/shared/docs/getting-started/raspberry-example/raspberry-example.mdx",tags:[],version:"current",sidebarPosition:1,frontMatter:{sidebar_position:1},sidebar:"tutorialSidebar",previous:{title:"Getting Started",permalink:"/opentwins/docs/category/getting-started"},next:{title:"Sending data to Ditto",permalink:"/opentwins/docs/getting-started/raspberry-example/sending-data"}},p={},u=[{value:"Requirements",id:"requirements",level:2},{value:"First step. Creating the twin",id:"first-step-creating-the-twin",level:2},{value:"Second step. Recieving the data",id:"second-step-recieving-the-data",level:2}],d={toc:u},m="wrapper";function h(e){let{components:t,...n}=e;return(0,r.kt)(m,(0,a.Z)({},d,n,{components:t,mdxType:"MDXLayout"}),(0,r.kt)("h1",{id:"raspberry-example"},"Raspberry example"),(0,r.kt)("h2",{id:"requirements"},"Requirements"),(0,r.kt)("p",null,"The only requisites are:"),(0,r.kt)("ul",null,(0,r.kt)("li",{parentName:"ul"},"Collect ",(0,r.kt)("inlineCode",{parentName:"li"},"IP")," address of Ditto."),(0,r.kt)("li",{parentName:"ul"},"Collect ",(0,r.kt)("inlineCode",{parentName:"li"},"USER")," and ",(0,r.kt)("inlineCode",{parentName:"li"},"PASSWORD"))),(0,r.kt)("h2",{id:"first-step-creating-the-twin"},"First step. Creating the twin"),(0,r.kt)("p",null,"First of all, you need to understand how twins work:\nA twin has two main components:"),(0,r.kt)("ul",null,(0,r.kt)("li",{parentName:"ul"},(0,r.kt)("strong",{parentName:"li"},"attributes"),". It contains the basic information of the twin, such as the name, location, etc."),(0,r.kt)("li",{parentName:"ul"},(0,r.kt)("strong",{parentName:"li"},"features"),". It contains the variables of the twin. Imagine a twin of a sensor that measures humidity and temperature. You will have two features: humidity and temperature.\nEach feature must contain a field called ",(0,r.kt)("inlineCode",{parentName:"li"},"properties")," that contains, as its name says, every property of the feature, for example, the value of the temperature and the time the value has been measured.")),(0,r.kt)("p",null,"Once we know wich data will store our twin, it is time to create it.\nTo create a twin, we need to make HTTP requests, we recommend you to use Postman. We need to create a ",(0,r.kt)("inlineCode",{parentName:"p"},"PUT")," request to the Ditto url with the next pattern and a specific payload."),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"PUT http://{DITTO_IP}:{PORT}/api/2/things/{nameOfThing}\n")),(0,r.kt)("p",null,'The payload has the attributes and features of the twin mentioned above. As attributes we have the location, in this case "Spain".'),(0,r.kt)("p",null,"As features we have temperature and humidity. In this case both features has the same properties, value and timestamp, but they dont have to fit."),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-json"},'{\n    "attributes": {\n        "location": "Spain"\n    },\n    "features": {\n        "temperature": {\n            "properties": {\n                "value": null,\n                "timestamp": null\n            }\n        },\n        "humidity": {\n            "properties": {\n                "value": null,\n                "timestamp": null\n            }\n        }\n    }\n}\n')),(0,r.kt)("p",null,"Once we have checked that all the data is correct, just click send. You should recieve a 200 code of a correct execution."),(0,r.kt)("p",null,"To check if the twin has been created properly, just send a ",(0,r.kt)("inlineCode",{parentName:"p"},"GET")," request to the same url."),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"GET http://{DITTO_IP}:{PORT}/api/2/things/{nameOfThing}\n")),(0,r.kt)("p",null,"You should be granted with the schema of the new twin."),(0,r.kt)("h2",{id:"second-step-recieving-the-data"},"Second step. Recieving the data"),(0,r.kt)("p",null,"A digital twin is a copy of a real object or process, but we just have a schema, so we need to feed it with data. To achieve this we can use both the Kafka or MQTT broker that are installed with the platform."),(0,r.kt)("p",null,"Ditto needs to recieve the data in a specific format called ",(0,r.kt)("a",{parentName:"p",href:"https://www.eclipse.org/ditto/protocol-overview.html"},(0,r.kt)("inlineCode",{parentName:"a"},"Ditto Protocol")),", so we need the data to be sent in that format. But don't worry if you recieve the data on other format, Ditto gives us the chance to create a mapping with Javascript to change the format when the data arrives to Ditto(We will always recommend you to send the data on Ditto protocol)."),(0,r.kt)("p",null,"Asuming that we recieve that data in Ditto protocol we can configure the connection with one of the two brokers, Kafka or MQTT. To create a connection you can proceed with the same steps as creating the twins, make a ",(0,r.kt)("inlineCode",{parentName:"p"},"POST")," request to the url and a payload that contains the connection information."),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"POST http://{DITTO_IP}:{PORT}/api/2/connections\n")),(0,r.kt)(o.Z,{mdxType:"Tabs"},(0,r.kt)(i.Z,{value:"kafka",label:"Kafka",mdxType:"TabItem"},(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-json"},'  {\n    "name": "{NAME OF THE CONNECTION}",\n    "connectionType": "kafka",\n    "connectionStatus": "open",\n    "uri": "tcp://KAFKA_BROKER_IP",\n    "sources": [\n      {\n        "addresses": [\n            {"list Of topics to read"}\n        ],\n        "consumerCount": 1,\n        "qos": 1,\n        "authorizationContext": [\n            "nginx:ditto"\n        ],\n        "headerMapping": {\n            "correlation-id": "{{header:correlation-id}}",\n            "namespace": "{{ entity:namespace }}",\n            "content-type": "{{header:content-type}}",\n            "connection": "{{ connection:id }}",\n            "id": "{{ entity:id }}",\n            "reply-to": "{{header:reply-to}}"\n        },\n        "replyTarget": {\n            "address": "{{header:reply-to}}",\n            "headerMapping": {\n                "content-type": "{{header:content-type}}",\n                "correlation-id": "{{header:correlation-id}}"\n            },\n            "expectedResponseTypes": [\n                "response",\n                "error"\n            ],\n            "enabled": true\n        }\n      }\n    ],\n    "targets": [],\n    "clientCount": 5,\n    "failoverEnabled": true,\n    "validateCertificates": true,\n    "processorPoolSize": 1,\n    "specificConfig": {\n        "saslMechanism": "plain",\n        "bootstrapServers": "KAFKA_BROKER_IP"\n    },\n    "tags": []\n  }\n'))),(0,r.kt)(i.Z,{value:"mqtt",label:"MQTT",mdxType:"TabItem"},(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-json"},'  {\n    "name": "{NAME OF THE CONNECTION}",\n    "connectionType": "mqtt-5",\n    "connectionStatus": "open",\n    "uri": "tcp://MQTT_BROKER_IP",\n    "sources": [\n      {\n        "addresses": [\n            {"list Of topics to read"}\n        ],\n        "consumerCount": 1,\n        "qos": 1,\n        "authorizationContext": [\n            "nginx:ditto"\n        ],\n        "headerMapping": {\n            "correlation-id": "{{header:correlation-id}}",\n            "namespace": "{{ entity:namespace }}",\n            "content-type": "{{header:content-type}}",\n            "connection": "{{ connection:id }}",\n            "id": "{{ entity:id }}",\n            "reply-to": "{{header:reply-to}}"\n        },\n        "replyTarget": {\n            "address": "{{header:reply-to}}",\n            "headerMapping": {\n                "content-type": "{{header:content-type}}",\n                "correlation-id": "{{header:correlation-id}}"\n            },\n            "expectedResponseTypes": [\n                "response",\n                "error"\n            ],\n            "enabled": true\n        }\n      }\n    ],\n    "targets": [],\n    "clientCount": 1,\n    "failoverEnabled": true,\n    "validateCertificates": true,\n    "processorPoolSize": 1,\n    "tags": []\n  }\n')))),(0,r.kt)("p",null,"Once we have checked that all the data is correct, just click send. You should recieve a 200 code of a correct execution."),(0,r.kt)("p",null,"To check if the twin has been created properly, just send a ",(0,r.kt)("inlineCode",{parentName:"p"},"GET")," request to the same url adding the if of the new connection"),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"GET http://{DITTO_IP}:{PORT}/api/2/connections/{connectionID}\n")),(0,r.kt)("p",null,"You should be granted with the information of the connection."),(0,r.kt)("p",null,"With all this setup, the configuration should be already done, and Ditto should be recieving the data from the broker. If you want to create an example script to send the data, just click on the next link."))}h.isMDXComponent=!0}}]);