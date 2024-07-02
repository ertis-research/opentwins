"use strict";(self.webpackChunkdocs=self.webpackChunkdocs||[]).push([[1826],{3905:(e,t,n)=>{n.d(t,{Zo:()=>p,kt:()=>h});var a=n(7294);function r(e,t,n){return t in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function i(e,t){var n=Object.keys(e);if(Object.getOwnPropertySymbols){var a=Object.getOwnPropertySymbols(e);t&&(a=a.filter((function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable}))),n.push.apply(n,a)}return n}function o(e){for(var t=1;t<arguments.length;t++){var n=null!=arguments[t]?arguments[t]:{};t%2?i(Object(n),!0).forEach((function(t){r(e,t,n[t])})):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(n)):i(Object(n)).forEach((function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(n,t))}))}return e}function l(e,t){if(null==e)return{};var n,a,r=function(e,t){if(null==e)return{};var n,a,r={},i=Object.keys(e);for(a=0;a<i.length;a++)n=i[a],t.indexOf(n)>=0||(r[n]=e[n]);return r}(e,t);if(Object.getOwnPropertySymbols){var i=Object.getOwnPropertySymbols(e);for(a=0;a<i.length;a++)n=i[a],t.indexOf(n)>=0||Object.prototype.propertyIsEnumerable.call(e,n)&&(r[n]=e[n])}return r}var s=a.createContext({}),u=function(e){var t=a.useContext(s),n=t;return e&&(n="function"==typeof e?e(t):o(o({},t),e)),n},p=function(e){var t=u(e.components);return a.createElement(s.Provider,{value:t},e.children)},c="mdxType",d={inlineCode:"code",wrapper:function(e){var t=e.children;return a.createElement(a.Fragment,{},t)}},m=a.forwardRef((function(e,t){var n=e.components,r=e.mdxType,i=e.originalType,s=e.parentName,p=l(e,["components","mdxType","originalType","parentName"]),c=u(n),m=r,h=c["".concat(s,".").concat(m)]||c[m]||d[m]||i;return n?a.createElement(h,o(o({ref:t},p),{},{components:n})):a.createElement(h,o({ref:t},p))}));function h(e,t){var n=arguments,r=t&&t.mdxType;if("string"==typeof e||r){var i=n.length,o=new Array(i);o[0]=m;var l={};for(var s in t)hasOwnProperty.call(t,s)&&(l[s]=t[s]);l.originalType=e,l[c]="string"==typeof e?e:r,o[1]=l;for(var u=2;u<i;u++)o[u]=n[u];return a.createElement.apply(null,o)}return a.createElement.apply(null,n)}m.displayName="MDXCreateElement"},5162:(e,t,n)=>{n.d(t,{Z:()=>o});var a=n(7294),r=n(6010);const i={tabItem:"tabItem_Ymn6"};function o(e){let{children:t,hidden:n,className:o}=e;return a.createElement("div",{role:"tabpanel",className:(0,r.Z)(i.tabItem,o),hidden:n},t)}},5488:(e,t,n)=>{n.d(t,{Z:()=>d});var a=n(7462),r=n(7294),i=n(6010),o=n(2389),l=n(7392),s=n(7094),u=n(2466);const p={tabList:"tabList__CuJ",tabItem:"tabItem_LNqP"};function c(e){const{lazy:t,block:n,defaultValue:o,values:c,groupId:d,className:m}=e,h=r.Children.map(e.children,(e=>{if((0,r.isValidElement)(e)&&"value"in e.props)return e;throw new Error(`Docusaurus error: Bad <Tabs> child <${"string"==typeof e.type?e.type:e.type.name}>: all children of the <Tabs> component should be <TabItem>, and every <TabItem> should have a unique "value" prop.`)})),f=c??h.map((e=>{let{props:{value:t,label:n,attributes:a}}=e;return{value:t,label:n,attributes:a}})),g=(0,l.l)(f,((e,t)=>e.value===t.value));if(g.length>0)throw new Error(`Docusaurus error: Duplicate values "${g.map((e=>e.value)).join(", ")}" found in <Tabs>. Every value needs to be unique.`);const b=null===o?o:o??h.find((e=>e.props.default))?.props.value??h[0].props.value;if(null!==b&&!f.some((e=>e.value===b)))throw new Error(`Docusaurus error: The <Tabs> has a defaultValue "${b}" but none of its children has the corresponding value. Available values are: ${f.map((e=>e.value)).join(", ")}. If you intend to show no default tab, use defaultValue={null} instead.`);const{tabGroupChoices:v,setTabGroupChoices:k}=(0,s.U)(),[y,w]=(0,r.useState)(b),N=[],{blockElementScrollPositionUntilNextRender:O}=(0,u.o5)();if(null!=d){const e=v[d];null!=e&&e!==y&&f.some((t=>t.value===e))&&w(e)}const T=e=>{const t=e.currentTarget,n=N.indexOf(t),a=f[n].value;a!==y&&(O(t),w(a),null!=d&&k(d,String(a)))},x=e=>{let t=null;switch(e.key){case"Enter":T(e);break;case"ArrowRight":{const n=N.indexOf(e.currentTarget)+1;t=N[n]??N[0];break}case"ArrowLeft":{const n=N.indexOf(e.currentTarget)-1;t=N[n]??N[N.length-1];break}}t?.focus()};return r.createElement("div",{className:(0,i.Z)("tabs-container",p.tabList)},r.createElement("ul",{role:"tablist","aria-orientation":"horizontal",className:(0,i.Z)("tabs",{"tabs--block":n},m)},f.map((e=>{let{value:t,label:n,attributes:o}=e;return r.createElement("li",(0,a.Z)({role:"tab",tabIndex:y===t?0:-1,"aria-selected":y===t,key:t,ref:e=>N.push(e),onKeyDown:x,onClick:T},o,{className:(0,i.Z)("tabs__item",p.tabItem,o?.className,{"tabs__item--active":y===t})}),n??t)}))),t?(0,r.cloneElement)(h.filter((e=>e.props.value===y))[0],{className:"margin-top--md"}):r.createElement("div",{className:"margin-top--md"},h.map(((e,t)=>(0,r.cloneElement)(e,{key:t,hidden:e.props.value!==y})))))}function d(e){const t=(0,o.Z)();return r.createElement(c,(0,a.Z)({key:String(t)},e))}},6029:(e,t,n)=>{n.r(t),n.d(t,{assets:()=>s,contentTitle:()=>o,default:()=>d,frontMatter:()=>i,metadata:()=>l,toc:()=>u});var a=n(7462),r=(n(7294),n(3905));n(5488),n(5162);const i={sidebar_position:2},o="Helm",l={unversionedId:"installation/using-helm",id:"installation/using-helm",title:"Helm",description:"Standard version",source:"@site/docs/installation/using-helm.mdx",sourceDirName:"installation",slug:"/installation/using-helm",permalink:"/opentwins/docs/installation/using-helm",draft:!1,editUrl:"https://github.com/facebook/docusaurus/tree/main/packages/create-docusaurus/templates/shared/docs/installation/using-helm.mdx",tags:[],version:"current",sidebarPosition:2,frontMatter:{sidebar_position:2},sidebar:"tutorialSidebar",previous:{title:"Requirements",permalink:"/opentwins/docs/installation/requirements"},next:{title:"Manual",permalink:"/opentwins/docs/installation/manual"}},s={},u=[{value:"Standard version",id:"standard-version",level:2},{value:"Installation",id:"installation",level:3},{value:"Configuration",id:"configuration",level:3},{value:"Obtain external URLs for Eclipse Ditto, Ditto extended API and Grafana.",id:"obtain-external-urls-for-eclipse-ditto-ditto-extended-api-and-grafana",level:4},{value:"Add URLs to OpenTwins plugin configuration",id:"add-urls-to-opentwins-plugin-configuration",level:4},{value:"Lightweight version",id:"lightweight-version",level:2}],p={toc:u},c="wrapper";function d(e){let{components:t,...i}=e;return(0,r.kt)(c,(0,a.Z)({},p,i,{components:t,mdxType:"MDXLayout"}),(0,r.kt)("h1",{id:"helm"},"Helm"),(0,r.kt)("h2",{id:"standard-version"},"Standard version"),(0,r.kt)("h3",{id:"installation"},"Installation"),(0,r.kt)("p",null,"First of all, you have to add ERTIS Research group helm repository to your helm repository list:"),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"helm repo add ertis https://ertis-research.github.io/Helm-charts/\n")),(0,r.kt)("p",null,"Once done, the next step is installing the chart by executing this line on your terminal (in our case, we will use ",(0,r.kt)("inlineCode",{parentName:"p"},"opentwins")," as release name and ",(0,r.kt)("inlineCode",{parentName:"p"},"opentwins")," as namespace, but you can choose the one that you prefeer). To customize the installation, please refer to ",(0,r.kt)("a",{parentName:"p",href:"https://github.com/ertis-research/Helm-charts/blob/main/OpenTwins/values.yaml"},"Helm's values")," file."),(0,r.kt)("admonition",{type:"warning"},(0,r.kt)("p",{parentName:"admonition"},"We recommend to modify the ",(0,r.kt)("strong",{parentName:"p"},"default passwords and tokens")," for Grafana and InfluxDB before deploying the platform. Currently, to avoid potential problems, we do not recommend changing Eclipse Ditto's username and password.")),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"helm upgrade --install opentwins ertis/OpenTwins --wait --dependency-update --debug\n")),(0,r.kt)("p",null,"After waiting some time, the installation will be ready for use."),(0,r.kt)("h3",{id:"configuration"},"Configuration"),(0,r.kt)("p",null,"If you have kept the default values of the Helm chart, you will only need to ",(0,r.kt)("strong",{parentName:"p"},"configure the OpenTwins interface plugin for Grafana"),"."),(0,r.kt)("h4",{id:"obtain-external-urls-for-eclipse-ditto-ditto-extended-api-and-grafana"},"Obtain external URLs for Eclipse Ditto, Ditto extended API and Grafana."),(0,r.kt)("p",null,"Get the name of the services with ",(0,r.kt)("inlineCode",{parentName:"p"},"kubectl get services"),". The result will look something similar to the following image. If you have changed the name of the release, the names will not be preceded by ",(0,r.kt)("em",{parentName:"p"},"opentwins"),", but by the name you have assigned. We are interested in the services ",(0,r.kt)("em",{parentName:"p"},"opentwins-grafana"),", ",(0,r.kt)("em",{parentName:"p"},"opentwins-ditto-nginx")," and ",(0,r.kt)("em",{parentName:"p"},"opentwins-ditto-extended-api"),"."),(0,r.kt)("center",null,(0,r.kt)("img",{src:n(8831).Z,alt:"Kubectl get services",style:{width:700}})),(0,r.kt)("p",null,"The method to obtain the URL may vary depending on the configuration of your cluster. Generally, the URL for each service will match the cluster IP and the NodePort (the number after the colon). For example, if our cluster IP is ",(0,r.kt)("inlineCode",{parentName:"p"},"192.168.32.25"),", the URL for Grafana would be ",(0,r.kt)("inlineCode",{parentName:"p"},"192.168.32.25:30718"),"."),(0,r.kt)("details",null,(0,r.kt)("summary",null,"Are you using ",(0,r.kt)("b",null,"Minikube")," to deploy OpenTwins?"),(0,r.kt)("div",null,(0,r.kt)("p",null,"As Minikube is a local cluster, you ",(0,r.kt)("strong",{parentName:"p"},"cannot directly use the IP of the cluster"),". Therefore, you will have to ",(0,r.kt)("a",{parentName:"p",href:"https://minikube.sigs.k8s.io/docs/handbook/accessing/"},"expose the services")," that you want to use externally with a command."),(0,r.kt)("p",null,"Open three terminals, one for each service, and run the following command on each terminal with a different service name. These will return a URL of your localhost with a port that will forward all traffic to the specified service. ",(0,r.kt)("strong",{parentName:"p"},"These are the URLs you should use.")),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"minikube service <service-name> --url\n")))),(0,r.kt)("h4",{id:"add-urls-to-opentwins-plugin-configuration"},"Add URLs to OpenTwins plugin configuration"),(0,r.kt)("p",null,"Access Grafana in any browser with the URL you have obtained. The credentials must match those indicated in the Helm values, which by default are user ",(0,r.kt)("em",{parentName:"p"},"admin")," and password ",(0,r.kt)("em",{parentName:"p"},"admin"),". "),(0,r.kt)("p",null,"Access the left drop-down menu and select ",(0,r.kt)("inlineCode",{parentName:"p"},"Administration > Plugins"),". Once there, find the ",(0,r.kt)("em",{parentName:"p"},"OpenTwins")," plugin and activate it by clicking ",(0,r.kt)("em",{parentName:"p"},"Enable"),". Then, go to the ",(0,r.kt)("em",{parentName:"p"},"Configuration")," tab where you will need to enter the Eclipse Ditto and Extended API URLs in the corresponding fields. Use ",(0,r.kt)("em",{parentName:"p"},"ditto")," for both the Eclipse Ditto username and password for the moment. Then click on ",(0,r.kt)("em",{parentName:"p"},"Save settings")," to complete the plugin configuration."),(0,r.kt)("admonition",{type:"note"},(0,r.kt)("p",{parentName:"admonition"},"If you are using the latest version of the interface, you may find two fields intended for an agent service. This functionality is currently under development and is not yet available, so leave them empty and disregard them for now.")),(0,r.kt)("p",null,"Find the available application in the ",(0,r.kt)("inlineCode",{parentName:"p"},"App > OpenTwins")," section of the left drop-down menu. "),(0,r.kt)("p",null,(0,r.kt)("strong",{parentName:"p"},"You can now start using OpenTwins"),". "),(0,r.kt)("details",null,(0,r.kt)("summary",null,"Screenshots"),(0,r.kt)("div",null,(0,r.kt)("center",null,(0,r.kt)("img",{src:n(6482).Z,alt:"Plugin",style:{width:600}})),(0,r.kt)("center",null,(0,r.kt)("img",{src:n(3508).Z,alt:"Configuration",style:{width:600}})),(0,r.kt)("center",null,(0,r.kt)("img",{src:n(8407).Z,alt:"Configuration",style:{width:400}})))),(0,r.kt)("h2",{id:"lightweight-version"},"Lightweight version"),(0,r.kt)("p",null,"OpenTwins has it's own lightweight version that aims to run on IoT devices such as Raspberry Pi devices.\nTo install this versi\xf3n, you have to follow the first step in order to add ERTIS repository to your repository list and then install the platform using the command bellow:"),(0,r.kt)("pre",null,(0,r.kt)("code",{parentName:"pre",className:"language-bash"},"helm install ot ertis/OpenTwins-Lightweight -n opentwins\n")),(0,r.kt)("p",null,"In this case connections still need to be made for the platform to work properly."))}d.isMDXComponent=!0},3508:(e,t,n)=>{n.d(t,{Z:()=>a});const a=n.p+"assets/images/configuration-interfaz-c20eaffea1d3bec206f55464ba19679a.png"},6482:(e,t,n)=>{n.d(t,{Z:()=>a});const a=n.p+"assets/images/enable-plugin-0aa4a65c98ecbff05f27d2b540455e1b.png"},8831:(e,t,n)=>{n.d(t,{Z:()=>a});const a=n.p+"assets/images/kubectlgetsvc-f4edab151d7278aa24b49da8b2bcc0e3.png"},8407:(e,t,n)=>{n.d(t,{Z:()=>a});const a=n.p+"assets/images/opentwins-access-2eb74b7ab18c9e4a88906e0a9a5a13fd.png"}}]);