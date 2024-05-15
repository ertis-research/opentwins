"use strict";(self.webpackChunkdocs=self.webpackChunkdocs||[]).push([[9048],{3905:(e,t,a)=>{a.d(t,{Zo:()=>u,kt:()=>b});var n=a(7294);function r(e,t,a){return t in e?Object.defineProperty(e,t,{value:a,enumerable:!0,configurable:!0,writable:!0}):e[t]=a,e}function o(e,t){var a=Object.keys(e);if(Object.getOwnPropertySymbols){var n=Object.getOwnPropertySymbols(e);t&&(n=n.filter((function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable}))),a.push.apply(a,n)}return a}function i(e){for(var t=1;t<arguments.length;t++){var a=null!=arguments[t]?arguments[t]:{};t%2?o(Object(a),!0).forEach((function(t){r(e,t,a[t])})):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(a)):o(Object(a)).forEach((function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(a,t))}))}return e}function s(e,t){if(null==e)return{};var a,n,r=function(e,t){if(null==e)return{};var a,n,r={},o=Object.keys(e);for(n=0;n<o.length;n++)a=o[n],t.indexOf(a)>=0||(r[a]=e[a]);return r}(e,t);if(Object.getOwnPropertySymbols){var o=Object.getOwnPropertySymbols(e);for(n=0;n<o.length;n++)a=o[n],t.indexOf(a)>=0||Object.prototype.propertyIsEnumerable.call(e,a)&&(r[a]=e[a])}return r}var l=n.createContext({}),c=function(e){var t=n.useContext(l),a=t;return e&&(a="function"==typeof e?e(t):i(i({},t),e)),a},u=function(e){var t=c(e.components);return n.createElement(l.Provider,{value:t},e.children)},p="mdxType",m={inlineCode:"code",wrapper:function(e){var t=e.children;return n.createElement(n.Fragment,{},t)}},d=n.forwardRef((function(e,t){var a=e.components,r=e.mdxType,o=e.originalType,l=e.parentName,u=s(e,["components","mdxType","originalType","parentName"]),p=c(a),d=r,b=p["".concat(l,".").concat(d)]||p[d]||m[d]||o;return a?n.createElement(b,i(i({ref:t},u),{},{components:a})):n.createElement(b,i({ref:t},u))}));function b(e,t){var a=arguments,r=t&&t.mdxType;if("string"==typeof e||r){var o=a.length,i=new Array(o);i[0]=d;var s={};for(var l in t)hasOwnProperty.call(t,l)&&(s[l]=t[l]);s.originalType=e,s[p]="string"==typeof e?e:r,i[1]=s;for(var c=2;c<o;c++)i[c]=a[c];return n.createElement.apply(null,i)}return n.createElement.apply(null,a)}d.displayName="MDXCreateElement"},5162:(e,t,a)=>{a.d(t,{Z:()=>i});var n=a(7294),r=a(6010);const o={tabItem:"tabItem_Ymn6"};function i(e){let{children:t,hidden:a,className:i}=e;return n.createElement("div",{role:"tabpanel",className:(0,r.Z)(o.tabItem,i),hidden:a},t)}},5488:(e,t,a)=>{a.d(t,{Z:()=>m});var n=a(7462),r=a(7294),o=a(6010),i=a(2389),s=a(7392),l=a(7094),c=a(2466);const u={tabList:"tabList__CuJ",tabItem:"tabItem_LNqP"};function p(e){const{lazy:t,block:a,defaultValue:i,values:p,groupId:m,className:d}=e,b=r.Children.map(e.children,(e=>{if((0,r.isValidElement)(e)&&"value"in e.props)return e;throw new Error(`Docusaurus error: Bad <Tabs> child <${"string"==typeof e.type?e.type:e.type.name}>: all children of the <Tabs> component should be <TabItem>, and every <TabItem> should have a unique "value" prop.`)})),f=p??b.map((e=>{let{props:{value:t,label:a,attributes:n}}=e;return{value:t,label:a,attributes:n}})),h=(0,s.l)(f,((e,t)=>e.value===t.value));if(h.length>0)throw new Error(`Docusaurus error: Duplicate values "${h.map((e=>e.value)).join(", ")}" found in <Tabs>. Every value needs to be unique.`);const g=null===i?i:i??b.find((e=>e.props.default))?.props.value??b[0].props.value;if(null!==g&&!f.some((e=>e.value===g)))throw new Error(`Docusaurus error: The <Tabs> has a defaultValue "${g}" but none of its children has the corresponding value. Available values are: ${f.map((e=>e.value)).join(", ")}. If you intend to show no default tab, use defaultValue={null} instead.`);const{tabGroupChoices:v,setTabGroupChoices:w}=(0,l.U)(),[A,y]=(0,r.useState)(g),E=[],{blockElementScrollPositionUntilNextRender:T}=(0,c.o5)();if(null!=m){const e=v[m];null!=e&&e!==A&&f.some((t=>t.value===e))&&y(e)}const x=e=>{const t=e.currentTarget,a=E.indexOf(t),n=f[a].value;n!==A&&(T(t),y(n),null!=m&&w(m,String(n)))},j=e=>{let t=null;switch(e.key){case"Enter":x(e);break;case"ArrowRight":{const a=E.indexOf(e.currentTarget)+1;t=E[a]??E[0];break}case"ArrowLeft":{const a=E.indexOf(e.currentTarget)-1;t=E[a]??E[E.length-1];break}}t?.focus()};return r.createElement("div",{className:(0,o.Z)("tabs-container",u.tabList)},r.createElement("ul",{role:"tablist","aria-orientation":"horizontal",className:(0,o.Z)("tabs",{"tabs--block":a},d)},f.map((e=>{let{value:t,label:a,attributes:i}=e;return r.createElement("li",(0,n.Z)({role:"tab",tabIndex:A===t?0:-1,"aria-selected":A===t,key:t,ref:e=>E.push(e),onKeyDown:j,onClick:x},i,{className:(0,o.Z)("tabs__item",u.tabItem,i?.className,{"tabs__item--active":A===t})}),a??t)}))),t?(0,r.cloneElement)(b.filter((e=>e.props.value===A))[0],{className:"margin-top--md"}):r.createElement("div",{className:"margin-top--md"},b.map(((e,t)=>(0,r.cloneElement)(e,{key:t,hidden:e.props.value!==A})))))}function m(e){const t=(0,i.Z)();return r.createElement(p,(0,n.Z)({key:String(t)},e))}},6618:(e,t,a)=>{a.r(t),a.d(t,{assets:()=>u,contentTitle:()=>l,default:()=>b,frontMatter:()=>s,metadata:()=>c,toc:()=>p});var n=a(7462),r=(a(7294),a(3905)),o=a(5488),i=a(5162);const s={sidebar_position:2},l="Create a digital twin",c={unversionedId:"guides/dt-schema-creation",id:"guides/dt-schema-creation",title:"Create a digital twin",description:"The way to interact with Eclipse Ditto and therefore create not only digital twins, but connections, etc. is through http requests and methods.",source:"@site/docs/guides/dt-schema-creation.mdx",sourceDirName:"guides",slug:"/guides/dt-schema-creation",permalink:"/opentwins/docs/guides/dt-schema-creation",draft:!1,editUrl:"https://github.com/facebook/docusaurus/tree/main/packages/create-docusaurus/templates/shared/docs/guides/dt-schema-creation.mdx",tags:[],version:"current",sidebarPosition:2,frontMatter:{sidebar_position:2},sidebar:"tutorialSidebar",previous:{title:"Create a type",permalink:"/opentwins/docs/guides/type-creation"},next:{title:"Examples",permalink:"/opentwins/docs/category/examples"}},u={},p=[],m={toc:p},d="wrapper";function b(e){let{components:t,...s}=e;return(0,r.kt)(d,(0,n.Z)({},m,s,{components:t,mdxType:"MDXLayout"}),(0,r.kt)("h1",{id:"create-a-digital-twin"},"Create a digital twin"),(0,r.kt)("p",null,"The way to interact with ",(0,r.kt)("a",{parentName:"p",href:"https://eclipse.dev/ditto/index.html"},"Eclipse Ditto")," and therefore create not only digital twins, but connections, etc. is through http requests and methods.\nAlthough the graphical interface of OpenTwins makes it unnecessary to go so low level, the option to communicate directly with Eclipse Ditto is still available."),(0,r.kt)(o.Z,{className:"unique-tabs",defaultValue:"ui",values:[{label:"Using Grafana interface",value:"ui"},{label:"Using http methods",value:"http"}],mdxType:"Tabs"},(0,r.kt)(i.Z,{value:"ui",mdxType:"TabItem"},(0,r.kt)("p",null,'To create a new digital twin schema using OpenTwins plugin in Grafana just select "Create new twin" button in Twins tab.\n',(0,r.kt)("img",{alt:"CreateTwin",src:a(2382).Z,width:"177",height:"42"}))),(0,r.kt)(i.Z,{value:"http",mdxType:"TabItem"},"This is an orange")))}b.isMDXComponent=!0},2382:(e,t,a)=>{a.d(t,{Z:()=>n});const n="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAALEAAAAqCAYAAAD8iLpFAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAiPSURBVHhe7Z17cFT1Fce/u8m+d5PdLJvdbJbNEAIEpARBMuERRsJArEILlQzCoAbqdgriADvOaAu0RlHBESrO8OhEjYhFKbbaFvhDWqcgmmKsPAsKgTTv92Ozm2Qfyabn7t4QCBtISHbl1t8n+c3de+65J/u793vP7/z2JjeibgIMhoAR80sGQ7AwETMEDxMxQ/AwETMET8iJ3eGvnNh1uBEtri7ewmB8v2jVUXh6gR4L0jW8pZeQmZgJmHGvwemR02UoQoqYCZhxL9KfLllNzBA8TMQMwcNEzBA8TMQMwcNEzBA8TMQMwcNEzBA8ERdxQlx0oDEYw0XERbx+0YhAGza0MqRP0WL1Yh0WjZXByJsZkSIaWYsSsGt5LMbzlkgTcRFrFOJAGw4mZ5txZKMFO1fo8URmHJ77pQUf5SVifVoEMr1Vg18sHoHlE/l1oTLkfsgxN02JKZNVyOAtkUawNbFxdgK2ZSugaGpD/vuVeOzFCvz6Ly6USORYusyI1VbeMVwYVfhJZizmjeLXhcqQ++HCxj2VWPdmHQp4S6QJ+Vts0+1X+VfDz+6nzYHlml1VgeXdoULeZhPmy9rxxuvVONjCmzmmmXB4mQrur6ux5EA7pj+SgFUpXTh+0o+Mh9QY19yKebubyFGCbC4DTZDBgE58W9yK9w624kwwCpUpSuRkx2JBSnB7SWkb3jvcjFPcz5oVj/wMBcaao+FucKOszo133mpEIbdfvBr2n2oxYySNBg4PvilswStfdnBbbmW8Hq/Pl6PljAOusVrM4vZp6MDHR2rxhxtOgXGcDmvmqpGWKEZ7pQfHv2jA7892AmYtXspRQVHcjGePtAd8Zy9MwOPJwDefVmPPJc4Si03r1DCWOfDMx66Az3VC9eNoFx4eZMwlyxKRreKPwQD7dLcU7hjNv+olrJmYm8BtW2nCrjXm622MWRZoN9o4n0FN9qxKjNEBjVddNwuYo6gRG9+qxmsnvIFVXbwME5PUsOVoYHb7UOX0k1WBtXYLXpgph8bpxrk6McZPM2CHXY/Jgb1U2LjaBPsDMkjqOvB5lQjJk+Pw6pNxt6/7RutRYDfi0VFRaC4hUUhkeGSJCQULFbxDH9QSpCbJkfVjA+bFdaPZ0Q2tVY21T5jwMO9izDAh3xaHLCr2rxV70KZXIPfxRGzNoONV5YVPLcfMiSr+fSnx4CQl9VeJ9BT+eJJIZyTJIG13B9fvxF3ENCXIMXGkBHRKBtSn4Sb85YSIvm9o3HpI22AwRiGGFvUNzuD6Tfhw9mI7vqqgTHWdbpw+Wo6fba/Ek/tJ9bO0WGgR4dwxsr1Zg+fzy5B7zI0oiwaPTSN3bTcun3Pi3T9VYHl+LV4tKKcM1AXFSAUe5MKdrIPtuAfcT686Xwkbn4WXZMUgVexGwWulsBXUwL61AgUlIqQ+EHvbE+gracKqbRRneymeLfLRxEGO2dz7gAwrMlUwOGjI/m057BTT9lIdPnVEI3NGLE1i23GshPwNMszVkrtVhVS9H62UQJOp1uWYniyFzu/F+VM3Hg+ekP0YYkye/vs0/IRVxNVNnXjunZpA6dDTrtBwyLUbbZwP5xs+fCg+0Rt/3mgpXQRd8Ci12EAlBdeWqylrIApGEzm0tOPQkRb8S6ShbQZsXWNB7ugoOloi8ugPNe430+Fs7YZmTjDmhsVaaLu66ARGg0bjfqmqcqCWf32mnRspeqBsSBnYRQKayr/PDYsViO7wQxwrwVTyKLziRaNYivtIIMY0GZI6PfjbWS+kiTQ3oItgNmVI1Hvwj74j1m0Yjpj992n4EebE7konuKrWYo4Nrt+EBGkTaOiz9F+eSEiMXNcTrDJM6GmUmetL3bjaTJu0MXh580jsfVSHbKqZ9VQTX2m804kggXNHUx7dG5PaOEknLpR6URN0GiTBmFK19KaY8T4vLpT76KIjijpQ7BYhJZmyvVUK1Hhw6IIHlXIZ7s9QI9UAVJa3IVDKDpRwxAwjwhRxixNnqyn3jdZgfZ863zjPgN89lYB16f3nzH83dMJPXxc/o+FuJ9/21WHLvkpsOUkOmTHI0vnx5Z/L8NDLFbDtrsEXLXd6PEcHKhy08HiwvycmtU0f1GHTzjp8FHQaJF7UcmN9UxvF6I25hWL2lDA0e0RhqR9qswZzjCKUlrtQe4lE6IxCSqYSyeJO/Od8cII2cMIRM3xEXMROGgq5NjQ82P5PF+rFMuT83IrtNGzPn6KFfeVIvJutgMzRhoOfeXjfW6n9vA3nvFRXLjBh1SQaLi0qrFpqxoHfJCHvR+Tg9pN8xDDQBDQJ0UhNN2DpmD4XhcsPblpjsOgwP3CTpROHznegQ6fE2pU6zLHQBGeSDr9abcUnm42YHdxrkDhx9DsfopNoZFgcg7R4GmWmG/CK3Yrjz8Rdv7Fz8JoHXg1NmNU+fEsZk9vvdJUfiQlSSJ0enDof9AvJLf0IMqSYESbiIn7jk4ZAGzJFtbDtd+AiiTEjU4+8FXrk3CdBZ5kDL+6pwV9vVwO2NOOFD1pRqlbClmvBh3YTbCkinP57PXZzJ+dYMw79txujZpjw4Y4kvL1IjprKPn8ac8mBI5e7EDsmDnlP6QMTt9pjlM0LfdBOiAsIrSA3DlPFHdj3x0acCO41aE4coPf0nR9jZhqw93kr9ubEwNjiws6DTddrThR5cI3LC04vvubH+IAIaemq6sDRoCk0IfoRYCgxI0zEPycOC/FyzBwBVFx0o5Q3DZSkUUpYJF0ovuzpFQWP0aJASkz3XcTl6nIJZK3ePp+SDAHu9ro5Cp6Gdpyt420/QEJ9Tvz/IWLGD4aI3+xgMCIBEzFD8DARMwQPEzFD8DARMwQPEzFD8IQUMfcEQgbjXqM/XYYUMfcITSZkxr1Ez6NdQ8H+8QxD8LCamCF4mIgZgoeJmCF4mIgZAgf4Hz9nf/pCnYTRAAAAAElFTkSuQmCC"}}]);