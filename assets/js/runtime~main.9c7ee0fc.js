(()=>{"use strict";var e,a,c,f,t,b={},r={};function d(e){var a=r[e];if(void 0!==a)return a.exports;var c=r[e]={id:e,loaded:!1,exports:{}};return b[e].call(c.exports,c,c.exports,d),c.loaded=!0,c.exports}d.m=b,d.c=r,e=[],d.O=(a,c,f,t)=>{if(!c){var b=1/0;for(i=0;i<e.length;i++){c=e[i][0],f=e[i][1],t=e[i][2];for(var r=!0,o=0;o<c.length;o++)(!1&t||b>=t)&&Object.keys(d.O).every((e=>d.O[e](c[o])))?c.splice(o--,1):(r=!1,t<b&&(b=t));if(r){e.splice(i--,1);var n=f();void 0!==n&&(a=n)}}return a}t=t||0;for(var i=e.length;i>0&&e[i-1][2]>t;i--)e[i]=e[i-1];e[i]=[c,f,t]},d.n=e=>{var a=e&&e.__esModule?()=>e.default:()=>e;return d.d(a,{a:a}),a},c=Object.getPrototypeOf?e=>Object.getPrototypeOf(e):e=>e.__proto__,d.t=function(e,f){if(1&f&&(e=this(e)),8&f)return e;if("object"==typeof e&&e){if(4&f&&e.__esModule)return e;if(16&f&&"function"==typeof e.then)return e}var t=Object.create(null);d.r(t);var b={};a=a||[null,c({}),c([]),c(c)];for(var r=2&f&&e;"object"==typeof r&&!~a.indexOf(r);r=c(r))Object.getOwnPropertyNames(r).forEach((a=>b[a]=()=>e[a]));return b.default=()=>e,d.d(t,b),t},d.d=(e,a)=>{for(var c in a)d.o(a,c)&&!d.o(e,c)&&Object.defineProperty(e,c,{enumerable:!0,get:a[c]})},d.f={},d.e=e=>Promise.all(Object.keys(d.f).reduce(((a,c)=>(d.f[c](e,a),a)),[])),d.u=e=>"assets/js/"+({53:"935f2afb",114:"908ba98b",123:"b00f9e27",799:"c0eb0ada",948:"8717b14a",1914:"d9f32620",2267:"59362658",2362:"e273c56f",2535:"814f3328",2690:"8dd02d2f",3085:"1f391b9e",3089:"a6aa9e1f",3237:"1df93b7f",3291:"49fbfc4c",3310:"dad39c83",3514:"73664a40",3584:"1b824599",3608:"9e4087bc",4013:"01a85c17",4482:"0b39e3dd",4576:"0598cbc5",4699:"85531627",4758:"d95c34ea",5041:"aa3c268d",5391:"9281dd35",5856:"2b39e8cb",5927:"5281b7a2",5969:"638abf38",6103:"ccc49370",7063:"ec3c7536",7081:"cbe8eee7",7214:"b9febb2b",7414:"393be207",7431:"1c4bf583",7495:"f8bfe6dc",7632:"0964aedb",7771:"6bdbbebb",7841:"3c4035d3",7918:"17896441",7949:"6cb63160",8148:"0fa86ba7",8610:"6875c492",8636:"f4f34a3a",8883:"926438fb",8906:"1ba72f0d",9003:"925b3f96",9312:"1dfe998c",9514:"1be78505",9521:"3c0fcc1c",9607:"dd403c78",9642:"7661071f",9671:"0e384e19",9817:"14eb3368"}[e]||e)+"."+{53:"b6aeca14",114:"a7d9cf5e",123:"b0a38fcd",210:"6e5e9f1a",799:"b281447b",948:"74619e7a",1914:"fa03cfff",2267:"22a5d353",2362:"c362744b",2529:"cec79ce1",2535:"0c1d9999",2690:"7fc106b4",3085:"8fc7c9b3",3089:"845cad8c",3237:"0f17e7ad",3291:"a2fd67e9",3310:"db0e19f5",3514:"d45d5659",3584:"dd038669",3608:"472c889f",4013:"75a76f22",4482:"025a1ab4",4576:"b9f9eae9",4699:"61a331f6",4758:"47a8623d",4972:"b60a5582",5041:"11260222",5391:"056d9a3c",5856:"fec78146",5927:"053b3bee",5969:"9e27bd44",6103:"d9c41d1e",7063:"adf7bfc2",7081:"234c3bce",7214:"6ab01aa0",7414:"6ad33c11",7431:"58dbf46d",7495:"e20617d7",7632:"f08e1154",7771:"28d91a55",7841:"22203ca8",7918:"4f945c03",7949:"6f683a2c",8148:"55d23946",8610:"f37b7b5c",8636:"77d55ebe",8883:"09609f7d",8906:"628ac649",9003:"a62a82fe",9312:"4c75b949",9514:"685933da",9521:"e76176e0",9607:"8539a427",9642:"1cbb68b7",9671:"0b015ace",9817:"716e9ec1"}[e]+".js",d.miniCssF=e=>{},d.g=function(){if("object"==typeof globalThis)return globalThis;try{return this||new Function("return this")()}catch(e){if("object"==typeof window)return window}}(),d.o=(e,a)=>Object.prototype.hasOwnProperty.call(e,a),f={},t="docs:",d.l=(e,a,c,b)=>{if(f[e])f[e].push(a);else{var r,o;if(void 0!==c)for(var n=document.getElementsByTagName("script"),i=0;i<n.length;i++){var u=n[i];if(u.getAttribute("src")==e||u.getAttribute("data-webpack")==t+c){r=u;break}}r||(o=!0,(r=document.createElement("script")).charset="utf-8",r.timeout=120,d.nc&&r.setAttribute("nonce",d.nc),r.setAttribute("data-webpack",t+c),r.src=e),f[e]=[a];var l=(a,c)=>{r.onerror=r.onload=null,clearTimeout(s);var t=f[e];if(delete f[e],r.parentNode&&r.parentNode.removeChild(r),t&&t.forEach((e=>e(c))),a)return a(c)},s=setTimeout(l.bind(null,void 0,{type:"timeout",target:r}),12e4);r.onerror=l.bind(null,r.onerror),r.onload=l.bind(null,r.onload),o&&document.head.appendChild(r)}},d.r=e=>{"undefined"!=typeof Symbol&&Symbol.toStringTag&&Object.defineProperty(e,Symbol.toStringTag,{value:"Module"}),Object.defineProperty(e,"__esModule",{value:!0})},d.p="/opentwins/",d.gca=function(e){return e={17896441:"7918",59362658:"2267",85531627:"4699","935f2afb":"53","908ba98b":"114",b00f9e27:"123",c0eb0ada:"799","8717b14a":"948",d9f32620:"1914",e273c56f:"2362","814f3328":"2535","8dd02d2f":"2690","1f391b9e":"3085",a6aa9e1f:"3089","1df93b7f":"3237","49fbfc4c":"3291",dad39c83:"3310","73664a40":"3514","1b824599":"3584","9e4087bc":"3608","01a85c17":"4013","0b39e3dd":"4482","0598cbc5":"4576",d95c34ea:"4758",aa3c268d:"5041","9281dd35":"5391","2b39e8cb":"5856","5281b7a2":"5927","638abf38":"5969",ccc49370:"6103",ec3c7536:"7063",cbe8eee7:"7081",b9febb2b:"7214","393be207":"7414","1c4bf583":"7431",f8bfe6dc:"7495","0964aedb":"7632","6bdbbebb":"7771","3c4035d3":"7841","6cb63160":"7949","0fa86ba7":"8148","6875c492":"8610",f4f34a3a:"8636","926438fb":"8883","1ba72f0d":"8906","925b3f96":"9003","1dfe998c":"9312","1be78505":"9514","3c0fcc1c":"9521",dd403c78:"9607","7661071f":"9642","0e384e19":"9671","14eb3368":"9817"}[e]||e,d.p+d.u(e)},(()=>{var e={1303:0,532:0};d.f.j=(a,c)=>{var f=d.o(e,a)?e[a]:void 0;if(0!==f)if(f)c.push(f[2]);else if(/^(1303|532)$/.test(a))e[a]=0;else{var t=new Promise(((c,t)=>f=e[a]=[c,t]));c.push(f[2]=t);var b=d.p+d.u(a),r=new Error;d.l(b,(c=>{if(d.o(e,a)&&(0!==(f=e[a])&&(e[a]=void 0),f)){var t=c&&("load"===c.type?"missing":c.type),b=c&&c.target&&c.target.src;r.message="Loading chunk "+a+" failed.\n("+t+": "+b+")",r.name="ChunkLoadError",r.type=t,r.request=b,f[1](r)}}),"chunk-"+a,a)}},d.O.j=a=>0===e[a];var a=(a,c)=>{var f,t,b=c[0],r=c[1],o=c[2],n=0;if(b.some((a=>0!==e[a]))){for(f in r)d.o(r,f)&&(d.m[f]=r[f]);if(o)var i=o(d)}for(a&&a(c);n<b.length;n++)t=b[n],d.o(e,t)&&e[t]&&e[t][0](),e[t]=0;return d.O(i)},c=self.webpackChunkdocs=self.webpackChunkdocs||[];c.forEach(a.bind(null,0)),c.push=a.bind(null,c.push.bind(c))})()})();