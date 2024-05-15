"use strict";(self.webpackChunkdocs=self.webpackChunkdocs||[]).push([[4482],{3905:(e,t,n)=>{n.d(t,{Zo:()=>c,kt:()=>d});var r=n(7294);function a(e,t,n){return t in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function i(e,t){var n=Object.keys(e);if(Object.getOwnPropertySymbols){var r=Object.getOwnPropertySymbols(e);t&&(r=r.filter((function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable}))),n.push.apply(n,r)}return n}function o(e){for(var t=1;t<arguments.length;t++){var n=null!=arguments[t]?arguments[t]:{};t%2?i(Object(n),!0).forEach((function(t){a(e,t,n[t])})):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(n)):i(Object(n)).forEach((function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(n,t))}))}return e}function l(e,t){if(null==e)return{};var n,r,a=function(e,t){if(null==e)return{};var n,r,a={},i=Object.keys(e);for(r=0;r<i.length;r++)n=i[r],t.indexOf(n)>=0||(a[n]=e[n]);return a}(e,t);if(Object.getOwnPropertySymbols){var i=Object.getOwnPropertySymbols(e);for(r=0;r<i.length;r++)n=i[r],t.indexOf(n)>=0||Object.prototype.propertyIsEnumerable.call(e,n)&&(a[n]=e[n])}return a}var s=r.createContext({}),p=function(e){var t=r.useContext(s),n=t;return e&&(n="function"==typeof e?e(t):o(o({},t),e)),n},c=function(e){var t=p(e.components);return r.createElement(s.Provider,{value:t},e.children)},u="mdxType",m={inlineCode:"code",wrapper:function(e){var t=e.children;return r.createElement(r.Fragment,{},t)}},h=r.forwardRef((function(e,t){var n=e.components,a=e.mdxType,i=e.originalType,s=e.parentName,c=l(e,["components","mdxType","originalType","parentName"]),u=p(n),h=a,d=u["".concat(s,".").concat(h)]||u[h]||m[h]||i;return n?r.createElement(d,o(o({ref:t},c),{},{components:n})):r.createElement(d,o({ref:t},c))}));function d(e,t){var n=arguments,a=t&&t.mdxType;if("string"==typeof e||a){var i=n.length,o=new Array(i);o[0]=h;var l={};for(var s in t)hasOwnProperty.call(t,s)&&(l[s]=t[s]);l.originalType=e,l[u]="string"==typeof e?e:a,o[1]=l;for(var p=2;p<i;p++)o[p]=n[p];return r.createElement.apply(null,o)}return r.createElement.apply(null,n)}h.displayName="MDXCreateElement"},9304:(e,t,n)=>{n.r(t),n.d(t,{assets:()=>s,contentTitle:()=>o,default:()=>m,frontMatter:()=>i,metadata:()=>l,toc:()=>p});var r=n(7462),a=(n(7294),n(3905));const i={sidebar_position:2},o="Helm",l={unversionedId:"installation/using-helm",id:"installation/using-helm",title:"Helm",description:"Installation",source:"@site/docs/installation/using-helm.md",sourceDirName:"installation",slug:"/installation/using-helm",permalink:"/opentwins/docs/installation/using-helm",draft:!1,editUrl:"https://github.com/facebook/docusaurus/tree/main/packages/create-docusaurus/templates/shared/docs/installation/using-helm.md",tags:[],version:"current",sidebarPosition:2,frontMatter:{sidebar_position:2},sidebar:"tutorialSidebar",previous:{title:"Requirements",permalink:"/opentwins/docs/installation/requirements"},next:{title:"Manual",permalink:"/opentwins/docs/category/manual"}},s={},p=[{value:"Installation",id:"installation",level:2},{value:"Lightweight installation",id:"lightweight-installation",level:2}],c={toc:p},u="wrapper";function m(e){let{components:t,...n}=e;return(0,a.kt)(u,(0,r.Z)({},c,n,{components:t,mdxType:"MDXLayout"}),(0,a.kt)("h1",{id:"helm"},"Helm"),(0,a.kt)("h2",{id:"installation"},"Installation"),(0,a.kt)("p",null,"First of all, you have to add ERTIS Research group helm repository to your helm repository list:"),(0,a.kt)("pre",null,(0,a.kt)("code",{parentName:"pre",className:"language-bash"},"helm repo add ertis https://ertis-research.github.io/Helm-charts/\n")),(0,a.kt)("p",null,"Once done, the next step is installing the chart by executing this line on your terminal (in our case, we will use ",(0,a.kt)("inlineCode",{parentName:"p"},"opentwins")," as release name and ",(0,a.kt)("inlineCode",{parentName:"p"},"opentwins")," as namespace, but you can choose the one that you prefeer). To customize the installation, please refer to ",(0,a.kt)("a",{parentName:"p",href:"https://github.com/ertis-research/Helm-charts/blob/main/OpenTwins/values.yaml"},"Helm's values")," file."),(0,a.kt)("pre",null,(0,a.kt)("code",{parentName:"pre",className:"language-bash"},"helm upgrade --install opentwins ertis/OpenTwins -n opentwins --wait --dependency-update\n")),(0,a.kt)("p",null,"After waiting some time, the installation will be ready for use."),(0,a.kt)("h2",{id:"lightweight-installation"},"Lightweight installation"),(0,a.kt)("p",null,"As described in the main page, OpenTwins has it's own lightweight version that aims to run on IoT devices such as Raspberry Pi devices.\nTo install this versi\xf3n, you have to follow the first step in order to add ERTIS repository to your repository list and then install the platform using the command bellow:"),(0,a.kt)("pre",null,(0,a.kt)("code",{parentName:"pre",className:"language-bash"},"helm install ot ertis/OpenTwins-Lightweight -n opentwins\n")),(0,a.kt)("p",null,"In this case connections still need to be made for the platform to work properly."))}m.isMDXComponent=!0}}]);