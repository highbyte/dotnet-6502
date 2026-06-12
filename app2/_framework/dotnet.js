//! Licensed to the .NET Foundation under one or more agreements.
//! The .NET Foundation licenses this file to you under the MIT license.

var e=!1;const t=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,4,1,96,0,0,3,2,1,0,10,8,1,6,0,6,64,25,11,11])),o=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,15,1,13,0,65,1,253,15,65,2,253,15,253,128,2,11])),n=async()=>WebAssembly.validate(new Uint8Array([0,97,115,109,1,0,0,0,1,5,1,96,0,1,123,3,2,1,0,10,10,1,8,0,65,0,253,15,253,98,11])),r=Symbol.for("wasm promise_control");function i(e,t){let o=null;const n=new Promise((function(n,r){o={isDone:!1,promise:null,resolve:t=>{o.isDone||(o.isDone=!0,n(t),e&&e())},reject:e=>{o.isDone||(o.isDone=!0,r(e),t&&t())}}}));o.promise=n;const i=n;return i[r]=o,{promise:i,promise_control:o}}function s(e){return e[r]}function a(e){e&&function(e){return void 0!==e[r]}(e)||Be(!1,"Promise is not controllable")}const l="__mono_message__",c=["debug","log","trace","warn","info","error"],d="MONO_WASM: ";let u,f,m,g,p,h;function w(e){g=e}function b(e){if(Pe.diagnosticTracing){const t="function"==typeof e?e():e;console.debug(d+t)}}function y(e,...t){console.info(d+e,...t)}function v(e,...t){console.info(e,...t)}function E(e,...t){console.warn(d+e,...t)}function _(e,...t){if(t&&t.length>0&&t[0]&&"object"==typeof t[0]){if(t[0].silent)return;if(t[0].toString)return void console.error(d+e,t[0].toString())}console.error(d+e,...t)}function x(e,t,o){return function(...n){try{let r=n[0];if(void 0===r)r="undefined";else if(null===r)r="null";else if("function"==typeof r)r=r.toString();else if("string"!=typeof r)try{r=JSON.stringify(r)}catch(e){r=r.toString()}t(o?JSON.stringify({method:e,payload:r,arguments:n.slice(1)}):[e+r,...n.slice(1)])}catch(e){m.error(`proxyConsole failed: ${e}`)}}}function j(e,t,o){f=t,g=e,m={...t};const n=`${o}/console`.replace("https://","wss://").replace("http://","ws://");u=new WebSocket(n),u.addEventListener("error",A),u.addEventListener("close",S),function(){for(const e of c)f[e]=x(`console.${e}`,T,!0)}()}function R(e){let t=30;const o=()=>{u?0==u.bufferedAmount||0==t?(e&&v(e),function(){for(const e of c)f[e]=x(`console.${e}`,m.log,!1)}(),u.removeEventListener("error",A),u.removeEventListener("close",S),u.close(1e3,e),u=void 0):(t--,globalThis.setTimeout(o,100)):e&&m&&m.log(e)};o()}function T(e){u&&u.readyState===WebSocket.OPEN?u.send(e):m.log(e)}function A(e){m.error(`[${g}] proxy console websocket error: ${e}`,e)}function S(e){m.debug(`[${g}] proxy console websocket closed: ${e}`,e)}function D(){Pe.preferredIcuAsset=O(Pe.config);let e="invariant"==Pe.config.globalizationMode;if(!e)if(Pe.preferredIcuAsset)Pe.diagnosticTracing&&b("ICU data archive(s) available, disabling invariant mode");else{if("custom"===Pe.config.globalizationMode||"all"===Pe.config.globalizationMode||"sharded"===Pe.config.globalizationMode){const e="invariant globalization mode is inactive and no ICU data archives are available";throw _(`ERROR: ${e}`),new Error(e)}Pe.diagnosticTracing&&b("ICU data archive(s) not available, using invariant globalization mode"),e=!0,Pe.preferredIcuAsset=null}const t="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",o=Pe.config.environmentVariables;if(void 0===o[t]&&e&&(o[t]="1"),void 0===o.TZ)try{const e=Intl.DateTimeFormat().resolvedOptions().timeZone||null;e&&(o.TZ=e)}catch(e){y("failed to detect timezone, will fallback to UTC")}}function O(e){var t;if((null===(t=e.resources)||void 0===t?void 0:t.icu)&&"invariant"!=e.globalizationMode){const t=e.applicationCulture||(ke?globalThis.navigator&&globalThis.navigator.languages&&globalThis.navigator.languages[0]:Intl.DateTimeFormat().resolvedOptions().locale),o=e.resources.icu;let n=null;if("custom"===e.globalizationMode){if(o.length>=1)return o[0].name}else t&&"all"!==e.globalizationMode?"sharded"===e.globalizationMode&&(n=function(e){const t=e.split("-")[0];return"en"===t||["fr","fr-FR","it","it-IT","de","de-DE","es","es-ES"].includes(e)?"icudt_EFIGS.dat":["zh","ko","ja"].includes(t)?"icudt_CJK.dat":"icudt_no_CJK.dat"}(t)):n="icudt.dat";if(n)for(let e=0;e<o.length;e++){const t=o[e];if(t.virtualPath===n)return t.name}}return e.globalizationMode="invariant",null}(new Date).valueOf();const C=class{constructor(e){this.url=e}toString(){return this.url}};async function k(e,t){try{const o="function"==typeof globalThis.fetch;if(Se){const n=e.startsWith("file://");if(!n&&o)return globalThis.fetch(e,t||{credentials:"same-origin"});p||(h=Ne.require("url"),p=Ne.require("fs")),n&&(e=h.fileURLToPath(e));const r=await p.promises.readFile(e);return{ok:!0,headers:{length:0,get:()=>null},url:e,arrayBuffer:()=>r,json:()=>JSON.parse(r),text:()=>{throw new Error("NotImplementedException")}}}if(o)return globalThis.fetch(e,t||{credentials:"same-origin"});if("function"==typeof read)return{ok:!0,url:e,headers:{length:0,get:()=>null},arrayBuffer:()=>new Uint8Array(read(e,"binary")),json:()=>JSON.parse(read(e,"utf8")),text:()=>read(e,"utf8")}}catch(t){return{ok:!1,url:e,status:500,headers:{length:0,get:()=>null},statusText:"ERR28: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t},text:()=>{throw t}}}throw new Error("No fetch implementation available")}function I(e){return"string"!=typeof e&&Be(!1,"url must be a string"),!M(e)&&0!==e.indexOf("./")&&0!==e.indexOf("../")&&globalThis.URL&&globalThis.document&&globalThis.document.baseURI&&(e=new URL(e,globalThis.document.baseURI).toString()),e}const U=/^[a-zA-Z][a-zA-Z\d+\-.]*?:\/\//,P=/[a-zA-Z]:[\\/]/;function M(e){return Se||Ie?e.startsWith("/")||e.startsWith("\\")||-1!==e.indexOf("///")||P.test(e):U.test(e)}let L,N=0;const $=[],z=[],W=new Map,F={"js-module-threads":!0,"js-module-runtime":!0,"js-module-dotnet":!0,"js-module-native":!0,"js-module-diagnostics":!0},B={...F,"js-module-library-initializer":!0},V={...F,dotnetwasm:!0,heap:!0,manifest:!0},q={...B,manifest:!0},H={...B,dotnetwasm:!0},J={dotnetwasm:!0,symbols:!0},Z={...B,dotnetwasm:!0,symbols:!0},Q={symbols:!0};function G(e){return!("icu"==e.behavior&&e.name!=Pe.preferredIcuAsset)}function K(e,t,o){null!=t||(t=[]),Be(1==t.length,`Expect to have one ${o} asset in resources`);const n=t[0];return n.behavior=o,X(n),e.push(n),n}function X(e){V[e.behavior]&&W.set(e.behavior,e)}function Y(e){Be(V[e],`Unknown single asset behavior ${e}`);const t=W.get(e);if(t&&!t.resolvedUrl)if(t.resolvedUrl=Pe.locateFile(t.name),F[t.behavior]){const e=ge(t);e?("string"!=typeof e&&Be(!1,"loadBootResource response for 'dotnetjs' type should be a URL string"),t.resolvedUrl=e):t.resolvedUrl=ce(t.resolvedUrl,t.behavior)}else if("dotnetwasm"!==t.behavior)throw new Error(`Unknown single asset behavior ${e}`);return t}function ee(e){const t=Y(e);return Be(t,`Single asset for ${e} not found`),t}let te=!1;async function oe(){if(!te){te=!0,Pe.diagnosticTracing&&b("mono_download_assets");try{const e=[],t=[],o=(e,t)=>{!Z[e.behavior]&&G(e)&&Pe.expected_instantiated_assets_count++,!H[e.behavior]&&G(e)&&(Pe.expected_downloaded_assets_count++,t.push(se(e)))};for(const t of $)o(t,e);for(const e of z)o(e,t);Pe.allDownloadsQueued.promise_control.resolve(),Promise.all([...e,...t]).then((()=>{Pe.allDownloadsFinished.promise_control.resolve()})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e})),await Pe.runtimeModuleLoaded.promise;const n=async e=>{const t=await e;if(t.buffer){if(!Z[t.behavior]){t.buffer&&"object"==typeof t.buffer||Be(!1,"asset buffer must be array-like or buffer-like or promise of these"),"string"!=typeof t.resolvedUrl&&Be(!1,"resolvedUrl must be string");const e=t.resolvedUrl,o=await t.buffer,n=new Uint8Array(o);pe(t),await Ue.beforeOnRuntimeInitialized.promise,Ue.instantiate_asset(t,e,n)}}else J[t.behavior]?("symbols"===t.behavior&&(await Ue.instantiate_symbols_asset(t),pe(t)),J[t.behavior]&&++Pe.actual_downloaded_assets_count):(t.isOptional||Be(!1,"Expected asset to have the downloaded buffer"),!H[t.behavior]&&G(t)&&Pe.expected_downloaded_assets_count--,!Z[t.behavior]&&G(t)&&Pe.expected_instantiated_assets_count--)},r=[],i=[];for(const t of e)r.push(n(t));for(const e of t)i.push(n(e));Promise.all(r).then((()=>{Ce||Ue.coreAssetsInMemory.promise_control.resolve()})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e})),Promise.all(i).then((async()=>{Ce||(await Ue.coreAssetsInMemory.promise,Ue.allAssetsInMemory.promise_control.resolve())})).catch((e=>{throw Pe.err("Error in mono_download_assets: "+e),Xe(1,e),e}))}catch(e){throw Pe.err("Error in mono_download_assets: "+e),e}}}let ne=!1;function re(){if(ne)return;ne=!0;const e=Pe.config,t=[];if(e.assets)for(const t of e.assets)"object"!=typeof t&&Be(!1,`asset must be object, it was ${typeof t} : ${t}`),"string"!=typeof t.behavior&&Be(!1,"asset behavior must be known string"),"string"!=typeof t.name&&Be(!1,"asset name must be string"),t.resolvedUrl&&"string"!=typeof t.resolvedUrl&&Be(!1,"asset resolvedUrl could be string"),t.hash&&"string"!=typeof t.hash&&Be(!1,"asset resolvedUrl could be string"),t.pendingDownload&&"object"!=typeof t.pendingDownload&&Be(!1,"asset pendingDownload could be object"),t.isCore?$.push(t):z.push(t),X(t);else if(e.resources){const o=e.resources;o.wasmNative||Be(!1,"resources.wasmNative must be defined"),o.jsModuleNative||Be(!1,"resources.jsModuleNative must be defined"),o.jsModuleRuntime||Be(!1,"resources.jsModuleRuntime must be defined"),K(z,o.wasmNative,"dotnetwasm"),K(t,o.jsModuleNative,"js-module-native"),K(t,o.jsModuleRuntime,"js-module-runtime"),o.jsModuleDiagnostics&&K(t,o.jsModuleDiagnostics,"js-module-diagnostics");const n=(e,t,o)=>{const n=e;n.behavior=t,o?(n.isCore=!0,$.push(n)):z.push(n)};if(o.coreAssembly)for(let e=0;e<o.coreAssembly.length;e++)n(o.coreAssembly[e],"assembly",!0);if(o.assembly)for(let e=0;e<o.assembly.length;e++)n(o.assembly[e],"assembly",!o.coreAssembly);if(0!=e.debugLevel&&Pe.isDebuggingSupported()){if(o.corePdb)for(let e=0;e<o.corePdb.length;e++)n(o.corePdb[e],"pdb",!0);if(o.pdb)for(let e=0;e<o.pdb.length;e++)n(o.pdb[e],"pdb",!o.corePdb)}if(e.loadAllSatelliteResources&&o.satelliteResources)for(const e in o.satelliteResources)for(let t=0;t<o.satelliteResources[e].length;t++){const r=o.satelliteResources[e][t];r.culture=e,n(r,"resource",!o.coreAssembly)}if(o.coreVfs)for(let e=0;e<o.coreVfs.length;e++)n(o.coreVfs[e],"vfs",!0);if(o.vfs)for(let e=0;e<o.vfs.length;e++)n(o.vfs[e],"vfs",!o.coreVfs);const r=O(e);if(r&&o.icu)for(let e=0;e<o.icu.length;e++){const t=o.icu[e];t.name===r&&n(t,"icu",!1)}if(o.wasmSymbols)for(let e=0;e<o.wasmSymbols.length;e++)n(o.wasmSymbols[e],"symbols",!1)}if(e.appsettings)for(let t=0;t<e.appsettings.length;t++){const o=e.appsettings[t],n=he(o);"appsettings.json"!==n&&n!==`appsettings.${e.applicationEnvironment}.json`||z.push({name:o,behavior:"vfs",cache:"no-cache",useCredentials:!0})}e.assets=[...$,...z,...t]}async function ie(e){const t=await se(e);return await t.pendingDownloadInternal.response,t.buffer}async function se(e){try{return await ae(e)}catch(t){if(!Pe.enableDownloadRetry)throw t;if(Ie||Se)throw t;if(e.pendingDownload&&e.pendingDownloadInternal==e.pendingDownload)throw t;if(e.resolvedUrl&&-1!=e.resolvedUrl.indexOf("file://"))throw t;if(t&&404==t.status)throw t;e.pendingDownloadInternal=void 0,await Pe.allDownloadsQueued.promise;try{return Pe.diagnosticTracing&&b(`Retrying download '${e.name}'`),await ae(e)}catch(t){return e.pendingDownloadInternal=void 0,await new Promise((e=>globalThis.setTimeout(e,100))),Pe.diagnosticTracing&&b(`Retrying download (2) '${e.name}' after delay`),await ae(e)}}}async function ae(e){for(;L;)await L.promise;try{++N,N==Pe.maxParallelDownloads&&(Pe.diagnosticTracing&&b("Throttling further parallel downloads"),L=i());const t=await async function(e){if(e.pendingDownload&&(e.pendingDownloadInternal=e.pendingDownload),e.pendingDownloadInternal&&e.pendingDownloadInternal.response)return e.pendingDownloadInternal.response;if(e.buffer){const t=await e.buffer;return e.resolvedUrl||(e.resolvedUrl="undefined://"+e.name),e.pendingDownloadInternal={url:e.resolvedUrl,name:e.name,response:Promise.resolve({ok:!0,arrayBuffer:()=>t,json:()=>JSON.parse(new TextDecoder("utf-8").decode(t)),text:()=>{throw new Error("NotImplementedException")},headers:{get:()=>{}}})},e.pendingDownloadInternal.response}const t=e.loadRemote&&Pe.config.remoteSources?Pe.config.remoteSources:[""];let o;for(let n of t){n=n.trim(),"./"===n&&(n="");const t=le(e,n);e.name===t?Pe.diagnosticTracing&&b(`Attempting to download '${t}'`):Pe.diagnosticTracing&&b(`Attempting to download '${t}' for ${e.name}`);try{e.resolvedUrl=t;const n=fe(e);if(e.pendingDownloadInternal=n,o=await n.response,!o||!o.ok)continue;return o}catch(e){o||(o={ok:!1,url:t,status:0,statusText:""+e});continue}}const n=e.isOptional||e.name.match(/\.pdb$/)&&Pe.config.ignorePdbLoadErrors;if(o||Be(!1,`Response undefined ${e.name}`),!n){const t=new Error(`download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`);throw t.status=o.status,t}y(`optional download '${o.url}' for ${e.name} failed ${o.status} ${o.statusText}`)}(e);return t?(J[e.behavior]||(e.buffer=await t.arrayBuffer(),++Pe.actual_downloaded_assets_count),e):e}finally{if(--N,L&&N==Pe.maxParallelDownloads-1){Pe.diagnosticTracing&&b("Resuming more parallel downloads");const e=L;L=void 0,e.promise_control.resolve()}}}function le(e,t){let o;return null==t&&Be(!1,`sourcePrefix must be provided for ${e.name}`),e.resolvedUrl?o=e.resolvedUrl:(o=""===t?"assembly"===e.behavior||"pdb"===e.behavior?e.name:"resource"===e.behavior&&e.culture&&""!==e.culture?`${e.culture}/${e.name}`:e.name:t+e.name,o=ce(Pe.locateFile(o),e.behavior)),o&&"string"==typeof o||Be(!1,"attemptUrl need to be path or url string"),o}function ce(e,t){return Pe.modulesUniqueQuery&&q[t]&&(e+=Pe.modulesUniqueQuery),e}let de=0;const ue=new Set;function fe(e){try{e.resolvedUrl||Be(!1,"Request's resolvedUrl must be set");const t=function(e){let t=e.resolvedUrl;if(Pe.loadBootResource){const o=ge(e);if(o instanceof Promise)return o;"string"==typeof o&&(t=o)}const o={};return e.cache?o.cache=e.cache:Pe.config.disableNoCacheFetch||(o.cache="no-cache"),e.useCredentials?o.credentials="include":!Pe.config.disableIntegrityCheck&&e.hash&&(o.integrity=e.hash),Pe.fetch_like(t,o)}(e),o={name:e.name,url:e.resolvedUrl,response:t};return ue.add(e.name),o.response.then((()=>{"assembly"==e.behavior&&Pe.loadedAssemblies.push(e.name),de++,Pe.onDownloadResourceProgress&&Pe.onDownloadResourceProgress(de,ue.size)})),o}catch(t){const o={ok:!1,url:e.resolvedUrl,status:500,statusText:"ERR29: "+t,arrayBuffer:()=>{throw t},json:()=>{throw t}};return{name:e.name,url:e.resolvedUrl,response:Promise.resolve(o)}}}const me={resource:"assembly",assembly:"assembly",pdb:"pdb",icu:"globalization",vfs:"configuration",manifest:"manifest",dotnetwasm:"dotnetwasm","js-module-dotnet":"dotnetjs","js-module-native":"dotnetjs","js-module-runtime":"dotnetjs","js-module-threads":"dotnetjs"};function ge(e){var t;if(Pe.loadBootResource){const o=null!==(t=e.hash)&&void 0!==t?t:"",n=e.resolvedUrl,r=me[e.behavior];if(r){const t=Pe.loadBootResource(r,e.name,n,o,e.behavior);return"string"==typeof t?I(t):t}}}function pe(e){e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null}function he(e){let t=e.lastIndexOf("/");return t>=0&&t++,e.substring(t)}async function we(e){e&&await Promise.all((null!=e?e:[]).map((e=>async function(e){try{const t=e.name;if(!e.moduleExports){const o=ce(Pe.locateFile(t),"js-module-library-initializer");Pe.diagnosticTracing&&b(`Attempting to import '${o}' for ${e}`),e.moduleExports=await import(/*! webpackIgnore: true */o)}Pe.libraryInitializers.push({scriptName:t,exports:e.moduleExports})}catch(t){E(`Failed to import library initializer '${e}': ${t}`)}}(e))))}async function be(e,t){if(!Pe.libraryInitializers)return;const o=[];for(let n=0;n<Pe.libraryInitializers.length;n++){const r=Pe.libraryInitializers[n];r.exports[e]&&o.push(ye(r.scriptName,e,(()=>r.exports[e](...t))))}await Promise.all(o)}async function ye(e,t,o){try{await o()}catch(o){throw E(`Failed to invoke '${t}' on library initializer '${e}': ${o}`),Xe(1,o),o}}function ve(e,t){if(e===t)return e;const o={...t};return void 0!==o.assets&&o.assets!==e.assets&&(o.assets=[...e.assets||[],...o.assets||[]]),void 0!==o.resources&&(o.resources=_e(e.resources||{assembly:[],jsModuleNative:[],jsModuleRuntime:[],wasmNative:[]},o.resources)),void 0!==o.environmentVariables&&(o.environmentVariables={...e.environmentVariables||{},...o.environmentVariables||{}}),void 0!==o.runtimeOptions&&o.runtimeOptions!==e.runtimeOptions&&(o.runtimeOptions=[...e.runtimeOptions||[],...o.runtimeOptions||[]]),Object.assign(e,o)}function Ee(e,t){if(e===t)return e;const o={...t};return o.config&&(e.config||(e.config={}),o.config=ve(e.config,o.config)),Object.assign(e,o)}function _e(e,t){if(e===t)return e;const o={...t};return void 0!==o.coreAssembly&&(o.coreAssembly=[...e.coreAssembly||[],...o.coreAssembly||[]]),void 0!==o.assembly&&(o.assembly=[...e.assembly||[],...o.assembly||[]]),void 0!==o.lazyAssembly&&(o.lazyAssembly=[...e.lazyAssembly||[],...o.lazyAssembly||[]]),void 0!==o.corePdb&&(o.corePdb=[...e.corePdb||[],...o.corePdb||[]]),void 0!==o.pdb&&(o.pdb=[...e.pdb||[],...o.pdb||[]]),void 0!==o.jsModuleWorker&&(o.jsModuleWorker=[...e.jsModuleWorker||[],...o.jsModuleWorker||[]]),void 0!==o.jsModuleNative&&(o.jsModuleNative=[...e.jsModuleNative||[],...o.jsModuleNative||[]]),void 0!==o.jsModuleDiagnostics&&(o.jsModuleDiagnostics=[...e.jsModuleDiagnostics||[],...o.jsModuleDiagnostics||[]]),void 0!==o.jsModuleRuntime&&(o.jsModuleRuntime=[...e.jsModuleRuntime||[],...o.jsModuleRuntime||[]]),void 0!==o.wasmSymbols&&(o.wasmSymbols=[...e.wasmSymbols||[],...o.wasmSymbols||[]]),void 0!==o.wasmNative&&(o.wasmNative=[...e.wasmNative||[],...o.wasmNative||[]]),void 0!==o.icu&&(o.icu=[...e.icu||[],...o.icu||[]]),void 0!==o.satelliteResources&&(o.satelliteResources=function(e,t){if(e===t)return e;for(const o in t)e[o]=[...e[o]||[],...t[o]||[]];return e}(e.satelliteResources||{},o.satelliteResources||{})),void 0!==o.modulesAfterConfigLoaded&&(o.modulesAfterConfigLoaded=[...e.modulesAfterConfigLoaded||[],...o.modulesAfterConfigLoaded||[]]),void 0!==o.modulesAfterRuntimeReady&&(o.modulesAfterRuntimeReady=[...e.modulesAfterRuntimeReady||[],...o.modulesAfterRuntimeReady||[]]),void 0!==o.extensions&&(o.extensions={...e.extensions||{},...o.extensions||{}}),void 0!==o.vfs&&(o.vfs=[...e.vfs||[],...o.vfs||[]]),Object.assign(e,o)}function xe(){const e=Pe.config;if(e.environmentVariables=e.environmentVariables||{},e.runtimeOptions=e.runtimeOptions||[],e.resources=e.resources||{assembly:[],jsModuleNative:[],jsModuleWorker:[],jsModuleRuntime:[],wasmNative:[],vfs:[],satelliteResources:{}},e.assets){Pe.diagnosticTracing&&b("config.assets is deprecated, use config.resources instead");for(const t of e.assets){const o={};switch(t.behavior){case"assembly":o.assembly=[t];break;case"pdb":o.pdb=[t];break;case"resource":o.satelliteResources={},o.satelliteResources[t.culture]=[t];break;case"icu":o.icu=[t];break;case"symbols":o.wasmSymbols=[t];break;case"vfs":o.vfs=[t];break;case"dotnetwasm":o.wasmNative=[t];break;case"js-module-threads":o.jsModuleWorker=[t];break;case"js-module-runtime":o.jsModuleRuntime=[t];break;case"js-module-native":o.jsModuleNative=[t];break;case"js-module-diagnostics":o.jsModuleDiagnostics=[t];break;case"js-module-dotnet":break;default:throw new Error(`Unexpected behavior ${t.behavior} of asset ${t.name}`)}_e(e.resources,o)}}e.debugLevel,e.applicationEnvironment||(e.applicationEnvironment="Production"),e.applicationCulture&&(e.environmentVariables.LANG=`${e.applicationCulture}.UTF-8`),Ue.diagnosticTracing=Pe.diagnosticTracing=!!e.diagnosticTracing,Ue.waitForDebugger=e.waitForDebugger,Pe.maxParallelDownloads=e.maxParallelDownloads||Pe.maxParallelDownloads,Pe.enableDownloadRetry=void 0!==e.enableDownloadRetry?e.enableDownloadRetry:Pe.enableDownloadRetry}let je=!1;async function Re(e){var t;if(je)return void await Pe.afterConfigLoaded.promise;let o;try{if(e.configSrc||Pe.config&&0!==Object.keys(Pe.config).length&&(Pe.config.assets||Pe.config.resources)||(e.configSrc="dotnet.boot.js"),o=e.configSrc,je=!0,o&&(Pe.diagnosticTracing&&b("mono_wasm_load_config"),await async function(e){const t=e.configSrc,o=Pe.locateFile(t);let n=null;void 0!==Pe.loadBootResource&&(n=Pe.loadBootResource("manifest",t,o,"","manifest"));let r,i=null;if(n)if("string"==typeof n)n.includes(".json")?(i=await s(I(n)),r=await Ae(i)):r=(await import(I(n))).config;else{const e=await n;"function"==typeof e.json?(i=e,r=await Ae(i)):r=e.config}else o.includes(".json")?(i=await s(ce(o,"manifest")),r=await Ae(i)):r=(await import(ce(o,"manifest"))).config;function s(e){return Pe.fetch_like(e,{method:"GET",credentials:"include",cache:"no-cache"})}Pe.config.applicationEnvironment&&(r.applicationEnvironment=Pe.config.applicationEnvironment),ve(Pe.config,r)}(e)),xe(),await we(null===(t=Pe.config.resources)||void 0===t?void 0:t.modulesAfterConfigLoaded),await be("onRuntimeConfigLoaded",[Pe.config]),e.onConfigLoaded)try{await e.onConfigLoaded(Pe.config,Le),xe()}catch(e){throw _("onConfigLoaded() failed",e),e}xe(),Pe.afterConfigLoaded.promise_control.resolve(Pe.config)}catch(t){const n=`Failed to load config file ${o} ${t} ${null==t?void 0:t.stack}`;throw Pe.config=e.config=Object.assign(Pe.config,{message:n,error:t,isError:!0}),Xe(1,new Error(n)),t}}function Te(){return!!globalThis.navigator&&(Pe.isChromium||Pe.isFirefox)}async function Ae(e){const t=Pe.config,o=await e.json();t.applicationEnvironment||o.applicationEnvironment||(o.applicationEnvironment=e.headers.get("Blazor-Environment")||e.headers.get("DotNet-Environment")||void 0),o.environmentVariables||(o.environmentVariables={});const n=e.headers.get("DOTNET-MODIFIABLE-ASSEMBLIES");n&&(o.environmentVariables.DOTNET_MODIFIABLE_ASSEMBLIES=n);const r=e.headers.get("ASPNETCORE-BROWSER-TOOLS");return r&&(o.environmentVariables.__ASPNETCORE_BROWSER_TOOLS=r),o}"function"!=typeof importScripts||globalThis.onmessage||(globalThis.dotnetSidecar=!0);const Se="object"==typeof process&&"object"==typeof process.versions&&"string"==typeof process.versions.node,De="function"==typeof importScripts,Oe=De&&"undefined"!=typeof dotnetSidecar,Ce=De&&!Oe,ke="object"==typeof window||De&&!Se,Ie=!ke&&!Se;let Ue={},Pe={},Me={},Le={},Ne={},$e=!1;const ze={},We={config:ze},Fe={mono:{},binding:{},internal:Ne,module:We,loaderHelpers:Pe,runtimeHelpers:Ue,diagnosticHelpers:Me,api:Le};function Be(e,t){if(e)return;const o="Assert failed: "+("function"==typeof t?t():t),n=new Error(o);_(o,n),Ue.nativeAbort(n)}function Ve(){return void 0!==Pe.exitCode}function qe(){return Ue.runtimeReady&&!Ve()}function He(){Ve()&&Be(!1,`.NET runtime already exited with ${Pe.exitCode} ${Pe.exitReason}. You can use runtime.runMain() which doesn't exit the runtime.`),Ue.runtimeReady||Be(!1,".NET runtime didn't start yet. Please call dotnet.create() first.")}function Je(){ke&&(globalThis.addEventListener("unhandledrejection",et),globalThis.addEventListener("error",tt))}let Ze,Qe;function Ge(e){Qe&&Qe(e),Xe(e,Pe.exitReason)}function Ke(e){Ze&&Ze(e||Pe.exitReason),Xe(1,e||Pe.exitReason)}function Xe(t,o){var n,r;const i=o&&"object"==typeof o;t=i&&"number"==typeof o.status?o.status:void 0===t?-1:t;const s=i&&"string"==typeof o.message?o.message:""+o;(o=i?o:Ue.ExitStatus?function(e,t){const o=new Ue.ExitStatus(e);return o.message=t,o.toString=()=>t,o}(t,s):new Error("Exit with code "+t+" "+s)).status=t,o.message||(o.message=s);const a=""+(o.stack||(new Error).stack);try{Object.defineProperty(o,"stack",{get:()=>a})}catch(e){}const l=!!o.silent;if(o.silent=!0,Ve())Pe.diagnosticTracing&&b("mono_exit called after exit");else{try{We.onAbort==Ke&&(We.onAbort=Ze),We.onExit==Ge&&(We.onExit=Qe),ke&&(globalThis.removeEventListener("unhandledrejection",et),globalThis.removeEventListener("error",tt)),Ue.runtimeReady?(Ue.jiterpreter_dump_stats&&Ue.jiterpreter_dump_stats(!1),0===t&&(null===(n=Pe.config)||void 0===n?void 0:n.interopCleanupOnExit)&&Ue.forceDisposeProxies(!0,!0),e&&0!==t&&(null===(r=Pe.config)||void 0===r||r.dumpThreadsOnNonZeroExit)):(Pe.diagnosticTracing&&b(`abort_startup, reason: ${o}`),function(e){Pe.allDownloadsQueued.promise_control.reject(e),Pe.allDownloadsFinished.promise_control.reject(e),Pe.afterConfigLoaded.promise_control.reject(e),Pe.wasmCompilePromise.promise_control.reject(e),Pe.runtimeModuleLoaded.promise_control.reject(e),Ue.dotnetReady&&(Ue.dotnetReady.promise_control.reject(e),Ue.afterInstantiateWasm.promise_control.reject(e),Ue.beforePreInit.promise_control.reject(e),Ue.afterPreInit.promise_control.reject(e),Ue.afterPreRun.promise_control.reject(e),Ue.beforeOnRuntimeInitialized.promise_control.reject(e),Ue.afterOnRuntimeInitialized.promise_control.reject(e),Ue.afterPostRun.promise_control.reject(e))}(o))}catch(e){E("mono_exit A failed",e)}try{l||(function(e,t){if(0!==e&&t){const e=Ue.ExitStatus&&t instanceof Ue.ExitStatus?b:_;"string"==typeof t?e(t):(void 0===t.stack&&(t.stack=(new Error).stack+""),t.message?e(Ue.stringify_as_error_with_stack?Ue.stringify_as_error_with_stack(t.message+"\n"+t.stack):t.message+"\n"+t.stack):e(JSON.stringify(t)))}!Ce&&Pe.config&&(Pe.config.logExitCode?Pe.config.forwardConsoleLogsToWS?R("WASM EXIT "+e):v("WASM EXIT "+e):Pe.config.forwardConsoleLogsToWS&&R())}(t,o),function(e){if(ke&&!Ce&&Pe.config&&Pe.config.appendElementOnExit&&document){const t=document.createElement("label");t.id="tests_done",0!==e&&(t.style.background="red"),t.innerHTML=""+e,document.body.appendChild(t)}}(t))}catch(e){E("mono_exit B failed",e)}Pe.exitCode=t,Pe.exitReason||(Pe.exitReason=o),!Ce&&Ue.runtimeReady&&We.runtimeKeepalivePop()}if(Pe.config&&Pe.config.asyncFlushOnExit&&0===t)throw(async()=>{try{await async function(){try{const e=await import(/*! webpackIgnore: true */"process"),t=e=>new Promise(((t,o)=>{e.on("error",o),e.end("","utf8",t)})),o=t(e.stderr),n=t(e.stdout);let r;const i=new Promise((e=>{r=setTimeout((()=>e("timeout")),1e3)}));await Promise.race([Promise.all([n,o]),i]),clearTimeout(r)}catch(e){_(`flushing std* streams failed: ${e}`)}}()}finally{Ye(t,o)}})(),o;Ye(t,o)}function Ye(e,t){if(Ue.runtimeReady&&Ue.nativeExit)try{Ue.nativeExit(e)}catch(e){!Ue.ExitStatus||e instanceof Ue.ExitStatus||E("set_exit_code_and_quit_now failed: "+e.toString())}if(0!==e||!ke)throw Se&&Ne.process?Ne.process.exit(e):Ue.quit&&Ue.quit(e,t),t}function et(e){ot(e,e.reason,"rejection")}function tt(e){ot(e,e.error,"error")}function ot(e,t,o){e.preventDefault();try{t||(t=new Error("Unhandled "+o)),void 0===t.stack&&(t.stack=(new Error).stack),t.stack=t.stack+"",t.silent||(_("Unhandled error:",t),Xe(1,t))}catch(e){}}!function(e){if($e)throw new Error("Loader module already loaded");$e=!0,Ue=e.runtimeHelpers,Pe=e.loaderHelpers,Me=e.diagnosticHelpers,Le=e.api,Ne=e.internal,Object.assign(Le,{INTERNAL:Ne,invokeLibraryInitializers:be}),Object.assign(e.module,{config:ve(ze,{environmentVariables:{}})});const r={mono_wasm_bindings_is_ready:!1,config:e.module.config,diagnosticTracing:!1,nativeAbort:e=>{throw e||new Error("abort")},nativeExit:e=>{throw new Error("exit:"+e)}},l={gitHash:"901ca941248413c79832d2fdbd709da0c4386353",config:e.module.config,diagnosticTracing:!1,maxParallelDownloads:16,enableDownloadRetry:!0,_loaded_files:[],loadedFiles:[],loadedAssemblies:[],libraryInitializers:[],workerNextNumber:1,actual_downloaded_assets_count:0,actual_instantiated_assets_count:0,expected_downloaded_assets_count:0,expected_instantiated_assets_count:0,afterConfigLoaded:i(),allDownloadsQueued:i(),allDownloadsFinished:i(),wasmCompilePromise:i(),runtimeModuleLoaded:i(),loadingWorkers:i(),is_exited:Ve,is_runtime_running:qe,assert_runtime_running:He,mono_exit:Xe,createPromiseController:i,getPromiseController:s,assertIsControllablePromise:a,mono_download_assets:oe,resolve_single_asset_path:ee,setup_proxy_console:j,set_thread_prefix:w,installUnhandledErrorHandler:Je,retrieve_asset_download:ie,invokeLibraryInitializers:be,isDebuggingSupported:Te,exceptions:t,simd:n,relaxedSimd:o};Object.assign(Ue,r),Object.assign(Pe,l)}(Fe);let nt,rt,it,st=!1,at=!1;async function lt(e){if(!at){if(at=!0,ke&&Pe.config.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&j("main",globalThis.console,globalThis.location.origin),We||Be(!1,"Null moduleConfig"),Pe.config||Be(!1,"Null moduleConfig.config"),"function"==typeof e){const t=e(Fe.api);if(t.ready)throw new Error("Module.ready couldn't be redefined.");Object.assign(We,t),Ee(We,t)}else{if("object"!=typeof e)throw new Error("Can't use moduleFactory callback of createDotnetRuntime function.");Ee(We,e)}await async function(e){if(Se){const e=await import(/*! webpackIgnore: true */"process"),t=14;if(e.versions.node.split(".")[0]<t)throw new Error(`NodeJS at '${e.execPath}' has too low version '${e.versions.node}', please use at least ${t}. See also https://aka.ms/dotnet-wasm-features`)}const t=/*! webpackIgnore: true */import.meta.url,o=t.indexOf("?");var n;if(o>0&&(Pe.modulesUniqueQuery=t.substring(o)),Pe.scriptUrl=t.replace(/\\/g,"/").replace(/[?#].*/,""),Pe.scriptDirectory=(n=Pe.scriptUrl).slice(0,n.lastIndexOf("/"))+"/",Pe.locateFile=e=>"URL"in globalThis&&globalThis.URL!==C?new URL(e,Pe.scriptDirectory).toString():M(e)?e:Pe.scriptDirectory+e,Pe.fetch_like=k,Pe.out=console.log,Pe.err=console.error,Pe.onDownloadResourceProgress=e.onDownloadResourceProgress,ke&&globalThis.navigator){const e=globalThis.navigator,t=e.userAgentData&&e.userAgentData.brands;t&&t.length>0?Pe.isChromium=t.some((e=>"Google Chrome"===e.brand||"Microsoft Edge"===e.brand||"Chromium"===e.brand)):e.userAgent&&(Pe.isChromium=e.userAgent.includes("Chrome"),Pe.isFirefox=e.userAgent.includes("Firefox"))}Ne.require=Se?await import(/*! webpackIgnore: true */"module").then((e=>e.createRequire(/*! webpackIgnore: true */import.meta.url))):Promise.resolve((()=>{throw new Error("require not supported")})),void 0===globalThis.URL&&(globalThis.URL=C)}(We)}}async function ct(e){return await lt(e),Ze=We.onAbort,Qe=We.onExit,We.onAbort=Ke,We.onExit=Ge,We.ENVIRONMENT_IS_PTHREAD?async function(){(function(){const e=new MessageChannel,t=e.port1,o=e.port2;t.addEventListener("message",(e=>{var n,r;n=JSON.parse(e.data.config),r=JSON.parse(e.data.monoThreadInfo),st?Pe.diagnosticTracing&&b("mono config already received"):(ve(Pe.config,n),Ue.monoThreadInfo=r,xe(),Pe.diagnosticTracing&&b("mono config received"),st=!0,Pe.afterConfigLoaded.promise_control.resolve(Pe.config),ke&&n.forwardConsoleLogsToWS&&void 0!==globalThis.WebSocket&&Pe.setup_proxy_console("worker-idle",console,globalThis.location.origin)),t.close(),o.close()}),{once:!0}),t.start(),self.postMessage({[l]:{monoCmd:"preload",port:o}},[o])})(),await Pe.afterConfigLoaded.promise,function(){const e=Pe.config;e.assets||Be(!1,"config.assets must be defined");for(const t of e.assets)X(t),Q[t.behavior]&&z.push(t)}(),setTimeout((async()=>{try{await oe()}catch(e){Xe(1,e)}}),0);const e=dt(),t=await Promise.all(e);return await ut(t),We}():async function(){var e;await Re(We),re();const t=dt();(async function(){try{const e=ee("dotnetwasm");await se(e),e&&e.pendingDownloadInternal&&e.pendingDownloadInternal.response||Be(!1,"Can't load dotnet.native.wasm");const t=await e.pendingDownloadInternal.response,o=t.headers&&t.headers.get?t.headers.get("Content-Type"):void 0;let n;if("function"==typeof WebAssembly.compileStreaming&&"application/wasm"===o)n=await WebAssembly.compileStreaming(t);else{ke&&"application/wasm"!==o&&E('WebAssembly resource does not have the expected content type "application/wasm", so falling back to slower ArrayBuffer instantiation.');const e=await t.arrayBuffer();Pe.diagnosticTracing&&b("instantiate_wasm_module buffered"),n=Ie?await Promise.resolve(new WebAssembly.Module(e)):await WebAssembly.compile(e)}e.pendingDownloadInternal=null,e.pendingDownload=null,e.buffer=null,e.moduleExports=null,Pe.wasmCompilePromise.promise_control.resolve(n)}catch(e){Pe.wasmCompilePromise.promise_control.reject(e)}})(),setTimeout((async()=>{try{D(),await oe()}catch(e){Xe(1,e)}}),0);const o=await Promise.all(t);return await ut(o),await Ue.dotnetReady.promise,await we(null===(e=Pe.config.resources)||void 0===e?void 0:e.modulesAfterRuntimeReady),await be("onRuntimeReady",[Fe.api]),Le}()}function dt(){const e=ee("js-module-runtime"),t=ee("js-module-native");if(nt&&rt)return[nt,rt,it];"object"==typeof e.moduleExports?nt=e.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${e.resolvedUrl}' for ${e.name}`),nt=import(/*! webpackIgnore: true */e.resolvedUrl)),"object"==typeof t.moduleExports?rt=t.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${t.resolvedUrl}' for ${t.name}`),rt=import(/*! webpackIgnore: true */t.resolvedUrl));const o=Y("js-module-diagnostics");return o&&("object"==typeof o.moduleExports?it=o.moduleExports:(Pe.diagnosticTracing&&b(`Attempting to import '${o.resolvedUrl}' for ${o.name}`),it=import(/*! webpackIgnore: true */o.resolvedUrl))),[nt,rt,it]}async function ut(e){const{initializeExports:t,initializeReplacements:o,configureRuntimeStartup:n,configureEmscriptenStartup:r,configureWorkerStartup:i,setRuntimeGlobals:s,passEmscriptenInternals:a}=e[0],{default:l}=e[1],c=e[2];s(Fe),t(Fe),c&&c.setRuntimeGlobals(Fe),await n(We),Pe.runtimeModuleLoaded.promise_control.resolve(),l((e=>(Object.assign(We,{ready:e.ready,__dotnet_runtime:{initializeReplacements:o,configureEmscriptenStartup:r,configureWorkerStartup:i,passEmscriptenInternals:a}}),We))).catch((e=>{if(e.message&&e.message.toLowerCase().includes("out of memory"))throw new Error(".NET runtime has failed to start, because too much memory was requested. Please decrease the memory by adjusting EmccMaximumHeapSize. See also https://aka.ms/dotnet-wasm-features");throw e}))}const ft=new class{withModuleConfig(e){try{return Ee(We,e),this}catch(e){throw Xe(1,e),e}}withOnConfigLoaded(e){try{return Ee(We,{onConfigLoaded:e}),this}catch(e){throw Xe(1,e),e}}withConsoleForwarding(){try{return ve(ze,{forwardConsoleLogsToWS:!0}),this}catch(e){throw Xe(1,e),e}}withExitOnUnhandledError(){try{return ve(ze,{exitOnUnhandledError:!0}),Je(),this}catch(e){throw Xe(1,e),e}}withAsyncFlushOnExit(){try{return ve(ze,{asyncFlushOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withExitCodeLogging(){try{return ve(ze,{logExitCode:!0}),this}catch(e){throw Xe(1,e),e}}withElementOnExit(){try{return ve(ze,{appendElementOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withInteropCleanupOnExit(){try{return ve(ze,{interopCleanupOnExit:!0}),this}catch(e){throw Xe(1,e),e}}withDumpThreadsOnNonZeroExit(){try{return ve(ze,{dumpThreadsOnNonZeroExit:!0}),this}catch(e){throw Xe(1,e),e}}withWaitingForDebugger(e){try{return ve(ze,{waitForDebugger:e}),this}catch(e){throw Xe(1,e),e}}withInterpreterPgo(e,t){try{return ve(ze,{interpreterPgo:e,interpreterPgoSaveDelay:t}),ze.runtimeOptions?ze.runtimeOptions.push("--interp-pgo-recording"):ze.runtimeOptions=["--interp-pgo-recording"],this}catch(e){throw Xe(1,e),e}}withConfig(e){try{return ve(ze,e),this}catch(e){throw Xe(1,e),e}}withConfigSrc(e){try{return e&&"string"==typeof e||Be(!1,"must be file path or URL"),Ee(We,{configSrc:e}),this}catch(e){throw Xe(1,e),e}}withVirtualWorkingDirectory(e){try{return e&&"string"==typeof e||Be(!1,"must be directory path"),ve(ze,{virtualWorkingDirectory:e}),this}catch(e){throw Xe(1,e),e}}withEnvironmentVariable(e,t){try{const o={};return o[e]=t,ve(ze,{environmentVariables:o}),this}catch(e){throw Xe(1,e),e}}withEnvironmentVariables(e){try{return e&&"object"==typeof e||Be(!1,"must be dictionary object"),ve(ze,{environmentVariables:e}),this}catch(e){throw Xe(1,e),e}}withDiagnosticTracing(e){try{return"boolean"!=typeof e&&Be(!1,"must be boolean"),ve(ze,{diagnosticTracing:e}),this}catch(e){throw Xe(1,e),e}}withDebugging(e){try{return null!=e&&"number"==typeof e||Be(!1,"must be number"),ve(ze,{debugLevel:e}),this}catch(e){throw Xe(1,e),e}}withApplicationArguments(...e){try{return e&&Array.isArray(e)||Be(!1,"must be array of strings"),ve(ze,{applicationArguments:e}),this}catch(e){throw Xe(1,e),e}}withRuntimeOptions(e){try{return e&&Array.isArray(e)||Be(!1,"must be array of strings"),ze.runtimeOptions?ze.runtimeOptions.push(...e):ze.runtimeOptions=e,this}catch(e){throw Xe(1,e),e}}withMainAssembly(e){try{return ve(ze,{mainAssemblyName:e}),this}catch(e){throw Xe(1,e),e}}withApplicationArgumentsFromQuery(){try{if(!globalThis.window)throw new Error("Missing window to the query parameters from");if(void 0===globalThis.URLSearchParams)throw new Error("URLSearchParams is supported");const e=new URLSearchParams(globalThis.window.location.search).getAll("arg");return this.withApplicationArguments(...e)}catch(e){throw Xe(1,e),e}}withApplicationEnvironment(e){try{return ve(ze,{applicationEnvironment:e}),this}catch(e){throw Xe(1,e),e}}withApplicationCulture(e){try{return ve(ze,{applicationCulture:e}),this}catch(e){throw Xe(1,e),e}}withResourceLoader(e){try{return Pe.loadBootResource=e,this}catch(e){throw Xe(1,e),e}}async download(){try{await async function(){lt(We),await Re(We),re(),D(),oe(),await Pe.allDownloadsFinished.promise}()}catch(e){throw Xe(1,e),e}}async create(){try{return this.instance||(this.instance=await async function(){return await ct(We),Fe.api}()),this.instance}catch(e){throw Xe(1,e),e}}async run(){try{return We.config||Be(!1,"Null moduleConfig.config"),this.instance||await this.create(),this.instance.runMainAndExit()}catch(e){throw Xe(1,e),e}}},mt=Xe,gt=ct;Ie||"function"==typeof globalThis.URL||Be(!1,"This browser/engine doesn't support URL API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),"function"!=typeof globalThis.BigInt64Array&&Be(!1,"This browser/engine doesn't support BigInt64Array API. Please use a modern version. See also https://aka.ms/dotnet-wasm-features"),ft.withConfig(/*json-start*/{
  "mainAssemblyName": "Highbyte.DotNet6502.App.Avalonia.Browser",
  "resources": {
    "hash": "sha256-2wjt/vIJren4+zZfXdrXa7ayHBO6G6/QOGBjrEzpkzU=",
    "jsModuleNative": [
      {
        "name": "dotnet.native.hnyg3x2ozj.js"
      }
    ],
    "jsModuleRuntime": [
      {
        "name": "dotnet.runtime.a6jcqbs390.js"
      }
    ],
    "wasmNative": [
      {
        "name": "dotnet.native.2b5j1uhkc6.wasm",
        "hash": "sha256-YnVYecs0/qbW6CCOdS/QMUxitRwNUiE+MnwI/l0UDNU=",
        "cache": "force-cache"
      }
    ],
    "icu": [
      {
        "virtualPath": "icudt_CJK.dat",
        "name": "icudt_CJK.tjcz0u77k5.dat",
        "hash": "sha256-SZLtQnRc0JkwqHab0VUVP7T3uBPSeYzxzDnpxPpUnHk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "icudt_EFIGS.dat",
        "name": "icudt_EFIGS.tptq2av103.dat",
        "hash": "sha256-8fItetYY8kQ0ww6oxwTLiT3oXlBwHKumbeP2pRF4yTc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "icudt_no_CJK.dat",
        "name": "icudt_no_CJK.lfu7j35m59.dat",
        "hash": "sha256-L7sV7NEYP37/Qr2FPCePo5cJqRgTXRwGHuwF5Q+0Nfs=",
        "cache": "force-cache"
      }
    ],
    "coreAssembly": [
      {
        "virtualPath": "Avalonia.Base.wasm",
        "name": "Avalonia.Base.uaov91zsiu.wasm",
        "hash": "sha256-xgngcSA2wGsQULmkXbQhwt7LRT4O8UMy9w0dM+u63yc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Browser.wasm",
        "name": "Avalonia.Browser.kbtmihfbbo.wasm",
        "hash": "sha256-ENxzLrSDdy7g+7BpLNgle5K1cBIyct6r02QnUIa/BzY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Controls.wasm",
        "name": "Avalonia.Controls.epwr5dody2.wasm",
        "hash": "sha256-Uhc7tKFoO4Bz6/2/sJVyu+mt8LPX1RWFzPgkKGxkYF4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Dialogs.wasm",
        "name": "Avalonia.Dialogs.frxlqudr3g.wasm",
        "hash": "sha256-K8CAPhO831djqMWarwKjMk1ifjIXl0Cv4ZN72S8/jlQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.HarfBuzz.wasm",
        "name": "Avalonia.HarfBuzz.qujl5quqom.wasm",
        "hash": "sha256-PnvVonYIRp+nDeYiWTPeswunltcfWPZA2FQ18am7n70=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Markup.Xaml.wasm",
        "name": "Avalonia.Markup.Xaml.vk758vn1ln.wasm",
        "hash": "sha256-YbYbiSQxXmSIR62lqAkvVfiErjhOWbdOkXQZIonYgM4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Markup.wasm",
        "name": "Avalonia.Markup.acyrw58gt5.wasm",
        "hash": "sha256-ZkUlmgEDHGLVYoPc13+cIjESIYjOBU9BEwb3d9e9lAA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Metal.wasm",
        "name": "Avalonia.Metal.a7usk1ezlj.wasm",
        "hash": "sha256-GqB5qHIpW6O4WhdiFIbobd6zYjdVbG3xtb+i50xa4zU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.OpenGL.wasm",
        "name": "Avalonia.OpenGL.0wu2hvqwai.wasm",
        "hash": "sha256-4DXhNTpzsnMLVXbmNmHKeBinPWD0d380ZAUcWmIiV2A=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Skia.wasm",
        "name": "Avalonia.Skia.t3ip0uj7os.wasm",
        "hash": "sha256-6U/bxQKVNHFZsn3HGAhOVrxhOnksFxjGgN1lzIoJOYA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Themes.Fluent.wasm",
        "name": "Avalonia.Themes.Fluent.qcuqor6nn7.wasm",
        "hash": "sha256-KqDuG/a8U2dP7qZBHbP8xfJDzWVevd0Qyd1cW9I/jqc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Avalonia.Vulkan.wasm",
        "name": "Avalonia.Vulkan.1z6kt7d5a4.wasm",
        "hash": "sha256-s9YqkmNpdTvpCBi8NgrSf5TxnpmsxiIRBCbCzlTC3H0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Azure.AI.OpenAI.wasm",
        "name": "Azure.AI.OpenAI.k7fififd71.wasm",
        "hash": "sha256-gESjVC+4oRcL8siqX+e2halcjHJDp3JjadKbeCvFX/E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Azure.Core.wasm",
        "name": "Azure.Core.wfs03z7zyd.wasm",
        "hash": "sha256-/A4EsmM7d3Bc1EmUq+u2ymH0vQFFj4InsQHKABkm2d0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "DynamicData.wasm",
        "name": "DynamicData.vjnpiuu9wl.wasm",
        "hash": "sha256-hRL5Ml/L2GzISlBqrIuZ1+o1duNSvAWL4KOTe6DScs0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "HarfBuzzSharp.wasm",
        "name": "HarfBuzzSharp.1kflelvdgj.wasm",
        "hash": "sha256-/0vSNldWPtuqzPaxTIe5Aitb9Zc4HznZO+Q+PnTCcDk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.AI.wasm",
        "name": "Highbyte.DotNet6502.AI.40x0dxjb67.wasm",
        "hash": "sha256-FOi5IppTXlV9KQMoVnk8Ug4dIy1yLCJ1TBYlXuV/fxU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.App.Avalonia.Browser.wasm",
        "name": "Highbyte.DotNet6502.App.Avalonia.Browser.c28daft7wo.wasm",
        "hash": "sha256-7B/e1W0ek7mCGoQznsXiwvXKAf3o9x6dYen3Y8S8iRw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.App.Avalonia.Core.wasm",
        "name": "Highbyte.DotNet6502.App.Avalonia.Core.16eozbwt3s.wasm",
        "hash": "sha256-uaypO5Tsw60Idq7UGNTg6ABstkjDk71OVc0DaCd0sj4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.wasm",
        "name": "Highbyte.DotNet6502.App.Avalonia.Shell.Commodore64.xe9yngpgt1.wasm",
        "hash": "sha256-KastSfVUc69Q5905oKjMhGofpYc00mXchTDnBuAecec=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.App.Avalonia.Shell.Generic.wasm",
        "name": "Highbyte.DotNet6502.App.Avalonia.Shell.Generic.61fj0173sk.wasm",
        "hash": "sha256-Jlk3qq8+odryjnqNFv8nhvqElJX5MrHpep52UkzOJc4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.wasm",
        "name": "Highbyte.DotNet6502.App.Avalonia.Shell.Vic20.wd8w3vowur.wasm",
        "hash": "sha256-dDFJPf/+DWZyVu0pS9Pmre7alWP1lbCJEZz9spD2Y5w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.DebugAdapter.Core.wasm",
        "name": "Highbyte.DotNet6502.DebugAdapter.Core.xolclmsqse.wasm",
        "hash": "sha256-0YWZwguAIUseQToCQV/4DGwkDBkbI7lESDxiXzwj/OA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Impl.Avalonia.Commodore64.wasm",
        "name": "Highbyte.DotNet6502.Impl.Avalonia.Commodore64.8gtkr2p3n9.wasm",
        "hash": "sha256-GGXWA+ql4KdwB4UaJlbPq6ASAFylqMKn+TLTg9vKMvw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Impl.Avalonia.Generic.wasm",
        "name": "Highbyte.DotNet6502.Impl.Avalonia.Generic.krgalcj6s8.wasm",
        "hash": "sha256-YAgRhHMhcR5GSvja62D+MBLVB4IU/TftoCKspYWkAZE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Impl.Avalonia.Vic20.wasm",
        "name": "Highbyte.DotNet6502.Impl.Avalonia.Vic20.zl9qpbe8ev.wasm",
        "hash": "sha256-2ad4skMAV57lPLfV7+0Nsqbf3pBj9z2xKoS5FqlKYUY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Impl.Avalonia.wasm",
        "name": "Highbyte.DotNet6502.Impl.Avalonia.xmyzvwpeij.wasm",
        "hash": "sha256-51apKOP6IBvH5K+bOTUu5icv7ktoD6LW/olYCBNBhyA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Impl.Browser.wasm",
        "name": "Highbyte.DotNet6502.Impl.Browser.y29g0hd33h.wasm",
        "hash": "sha256-BvrZUsvmmijnvR0dLxjZ1zdRZtbaAbVZLI6dzJ2AH8c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Impl.NAudio.wasm",
        "name": "Highbyte.DotNet6502.Impl.NAudio.645jovmv08.wasm",
        "hash": "sha256-prSj0vibtddX1gVrlTA/ZJv3zWhb4sTFNOBc+ZA1Cfs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Monitor.wasm",
        "name": "Highbyte.DotNet6502.Monitor.ejgjifotct.wasm",
        "hash": "sha256-hneOjFyNrr/YrNYWyfa/tpFDrb4xER7liyekTxfxo3c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Remoting.wasm",
        "name": "Highbyte.DotNet6502.Remoting.s7qpbwfyyv.wasm",
        "hash": "sha256-CNTK7gRp+nfOEX1hPTfml6T2hxAknFT+vH44jvAS15g=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Scripting.MoonSharp.wasm",
        "name": "Highbyte.DotNet6502.Scripting.MoonSharp.m84vnpprl7.wasm",
        "hash": "sha256-NvLn77rmi0IMI7kF3bj1A55Tpm/WD0olPe3OYem60j0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Systems.Commodore64.Transport.wasm",
        "name": "Highbyte.DotNet6502.Systems.Commodore64.Transport.inf3v3a6bl.wasm",
        "hash": "sha256-/vyXPUhnk/ZA8svn/c1AithVVNQPiyRBRxBhi2QqPME=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Systems.Commodore64.wasm",
        "name": "Highbyte.DotNet6502.Systems.Commodore64.r0qyaxbg3g.wasm",
        "hash": "sha256-xSmrcOp3K/LM1pT9OQliWJu7JmZnOKzptuv64DFf1wo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Systems.Generic.wasm",
        "name": "Highbyte.DotNet6502.Systems.Generic.frln7xzjmn.wasm",
        "hash": "sha256-60gjvYYWXO57gSWtaAWR18MmYpII6Iclp05ijemV49M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Systems.Plugins.wasm",
        "name": "Highbyte.DotNet6502.Systems.Plugins.9zrf9ylvss.wasm",
        "hash": "sha256-ZbxdxZLP9QYRDNpjMM/Jw6oYqtjfFB6etHeOzZrBdYM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Systems.Vic20.wasm",
        "name": "Highbyte.DotNet6502.Systems.Vic20.mhol2msjv0.wasm",
        "hash": "sha256-xrYPW5qBlqfAXptyWFS1rH56bEQz4YNiAIfLx0NZhHo=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.Systems.wasm",
        "name": "Highbyte.DotNet6502.Systems.3auud7kv4i.wasm",
        "hash": "sha256-NWJPaZuw7zzgx4uY0qog7WYBNUH5iESFooZbUNSsQUY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Highbyte.DotNet6502.wasm",
        "name": "Highbyte.DotNet6502.1w101l0zrx.wasm",
        "hash": "sha256-kd7aY1IkS/tqlt6Pc4WfrdzYzFWR8rTd/M4GNCHayaM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.DotNet.PlatformAbstractions.wasm",
        "name": "Microsoft.DotNet.PlatformAbstractions.yz5hfvkhp2.wasm",
        "hash": "sha256-YvxoXFk33sz5RSeK62bOTWalFlDgqBSweEhHkQGbohY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.Abstractions.wasm",
        "name": "Microsoft.Extensions.Configuration.Abstractions.zhupb2rxhg.wasm",
        "hash": "sha256-p4kvTyhmNVw+8sbgKIX5xjrb6J9DinoAEGFaQiXxmls=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.Binder.wasm",
        "name": "Microsoft.Extensions.Configuration.Binder.wz7s76rbkh.wasm",
        "hash": "sha256-hDnNbYxnhuKchRdISPVPUQO4wq7rFVYtzgT1l+2zEEU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Configuration.wasm",
        "name": "Microsoft.Extensions.Configuration.hu9qujkgbh.wasm",
        "hash": "sha256-Wf7tE64W5gpZDZoNTgzhooapL9wWO7Tvq7XP80224/s=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyInjection.wasm",
        "name": "Microsoft.Extensions.DependencyInjection.nw0r4qlp85.wasm",
        "hash": "sha256-V/nKmZqCh3dOV3oAMlyO3L7N3RyJXCXYwXixVEM0Ins=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyInjection.Abstractions.wasm",
        "name": "Microsoft.Extensions.DependencyInjection.Abstractions.iw8ehv8dux.wasm",
        "hash": "sha256-knHbacNrqlV1UU0xxyzacGIm7MsNSQORvjN6KllNH3c=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.DependencyModel.wasm",
        "name": "Microsoft.Extensions.DependencyModel.vjxlwltei0.wasm",
        "hash": "sha256-5X4eeVtODtGMWxbU3dq7ChvkyXaXqvZqHGQ04ylqBWA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.wasm",
        "name": "Microsoft.Extensions.Logging.rrn7fmo42h.wasm",
        "hash": "sha256-F3uoVk2XyK9xZIb2fhNGNzytiBx2JLPTZlZeS1cj67U=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.Abstractions.wasm",
        "name": "Microsoft.Extensions.Logging.Abstractions.pq5zkr87o6.wasm",
        "hash": "sha256-L9iKe2tivRjD2EsYQYtL7RO2IGXWEhY6//jpO/Gtul8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Logging.Configuration.wasm",
        "name": "Microsoft.Extensions.Logging.Configuration.jeq31ln9rn.wasm",
        "hash": "sha256-lZKSP1Qht2qShKHIIWzCrQdkP235YNT+YmfZLD2eRgw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.ObjectPool.wasm",
        "name": "Microsoft.Extensions.ObjectPool.amq6m7megg.wasm",
        "hash": "sha256-vgFKIvhH6OnJEXTS8x6zcB7uRejS6FRAn9KxTs/gxV0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Options.ConfigurationExtensions.wasm",
        "name": "Microsoft.Extensions.Options.ConfigurationExtensions.unepg0wf27.wasm",
        "hash": "sha256-euZxYjCjnHACPNwEj0m5RwxtU55bcmTLQcHcXaFLLV0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Options.wasm",
        "name": "Microsoft.Extensions.Options.rnw7svdvjc.wasm",
        "hash": "sha256-6VZiFEYzdsbnl3lz80qrbG7itnlvx1Vl6Iy2H4uC0M8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Microsoft.Extensions.Primitives.wasm",
        "name": "Microsoft.Extensions.Primitives.a2628a055t.wasm",
        "hash": "sha256-MjuZUbdlFrIX2wbwiCEox8NEHZI2tNrUWjhflFpzO5I=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "MoonSharp.Interpreter.wasm",
        "name": "MoonSharp.Interpreter.djmhoirfkr.wasm",
        "hash": "sha256-WZz/XeO3yfAVLWDz1YE8UQsYpitWmAIiuQ8A3rUKapU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "NAudio.Core.wasm",
        "name": "NAudio.Core.a9yg8iaiex.wasm",
        "hash": "sha256-bsFReNJwqyVWzKaCyZIibs/SsrOqoNG1nMrbwDvnpTY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "ReactiveUI.wasm",
        "name": "ReactiveUI.yab51ir0np.wasm",
        "hash": "sha256-eqGeJUgy5sM4WbhnYDWYGVSPVEhaoLPfIwxFp53o2LY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Silk.NET.Core.wasm",
        "name": "Silk.NET.Core.5u8ldobrgw.wasm",
        "hash": "sha256-/RzioT1TkORdiXm9+iQJA2RLOUfgNCeeTJkbUgTDO2o=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Silk.NET.OpenAL.Extensions.EXT.wasm",
        "name": "Silk.NET.OpenAL.Extensions.EXT.hln4aknrco.wasm",
        "hash": "sha256-dZo/9Q2A91VOpFm2k4arYV6ELIUFpCE7tiLOCiNTrKc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Silk.NET.OpenAL.wasm",
        "name": "Silk.NET.OpenAL.713ks43qfp.wasm",
        "hash": "sha256-Wyh5aB17Ss1CLGFIwhEYj2tz+iNUMDzfJ7cqZO1agVA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "SixLabors.ImageSharp.wasm",
        "name": "SixLabors.ImageSharp.pi9c3w3mqf.wasm",
        "hash": "sha256-zzFpFB2aOufv+EBs15TxgnwwCG4RAtFGEOox+rnhI7Q=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "SkiaSharp.wasm",
        "name": "SkiaSharp.aknxs8674e.wasm",
        "hash": "sha256-itHEvkwAYpZRWCB5e2DW2UjY3sheI6OCzH17syUmAr0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Splat.Builder.wasm",
        "name": "Splat.Builder.83h6ejcure.wasm",
        "hash": "sha256-BNZYiPDoiydNe+lurB/nJGC6vNasMtRF6raN6v1ZPj0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Splat.Core.wasm",
        "name": "Splat.Core.qsqbru2tkm.wasm",
        "hash": "sha256-lhAt7koMIMHxjy3YGTJXBlQ55Q/OeKk2q7tF5c+HIUs=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Splat.Logging.wasm",
        "name": "Splat.Logging.cn1eqz788f.wasm",
        "hash": "sha256-KMo3djhegLTxNx3BbPbOZVB94o2qsW203T0biNe0EbE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "Splat.wasm",
        "name": "Splat.tbxx4wb4qu.wasm",
        "hash": "sha256-ByXMFb5KFieMCO12KDiMFzZJEYOwFqAz2p3Bg7cGbes=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.wasm",
        "name": "System.Collections.9pw7ul34ww.wasm",
        "hash": "sha256-s9yj0OCVK8/ARxqHaGJINLJ0a//aew9Y58qmdPmTwXE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Concurrent.wasm",
        "name": "System.Collections.Concurrent.xki3q4842r.wasm",
        "hash": "sha256-0eomP0ecZm4cjzzo3P1ZFPwoglEVy4eh576Po6Ittnk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.NonGeneric.wasm",
        "name": "System.Collections.NonGeneric.yahb5y19sc.wasm",
        "hash": "sha256-WmkNFSo289dYpjvaUIGS0VnQMzJNcQ5J3efOoJJcOaA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Collections.Specialized.wasm",
        "name": "System.Collections.Specialized.3itx0wr30j.wasm",
        "hash": "sha256-mwMqkK/jP4QfFVNBwM/ZveCNGi0/LQH9V+FGgGCrfiY=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.CommandLine.wasm",
        "name": "System.CommandLine.g14sia8t5z.wasm",
        "hash": "sha256-dH0Ck33Nd9ZVHSlSzQT5T0FL8rG8fhi+T+zmfkU/fdQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.Annotations.wasm",
        "name": "System.ComponentModel.Annotations.4ys17a6m81.wasm",
        "hash": "sha256-NAhR9TeMuIf2JPmVO5eMrQZ4zwd9jPJSBrn8JEbrfI0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.Primitives.wasm",
        "name": "System.ComponentModel.Primitives.k1jbc2r2hi.wasm",
        "hash": "sha256-KDlN8cpcsSkm5pOjk4wURKb7vdqLuEYZg7AwNnoZTGM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.TypeConverter.wasm",
        "name": "System.ComponentModel.TypeConverter.l7rvg1ajil.wasm",
        "hash": "sha256-f1DZyRemfeLtsML1g4QqQaMnsPSeqZubFxQo2YqitgI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ComponentModel.wasm",
        "name": "System.ComponentModel.nymadks57t.wasm",
        "hash": "sha256-QXB+2iTrGpmogn31AUX+7fQG0wbyvRwUIsMKlYgKm4Y=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Console.wasm",
        "name": "System.Console.cavje832h1.wasm",
        "hash": "sha256-3DxHJtAeVLGFfsttFAdAz4VQd29UTsU8KytERLj30Qk=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Debug.wasm",
        "name": "System.Diagnostics.Debug.qnzsrrzprn.wasm",
        "hash": "sha256-eqWtxRDHsB/wjUqat5n+OGpmp/4O3UrF4BpMumIBdLU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.DiagnosticSource.wasm",
        "name": "System.Diagnostics.DiagnosticSource.ckiytlunrc.wasm",
        "hash": "sha256-hByK53MNgceo3WBU4fYPgyOLNvtwJqDciQl82wEIGos=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.Process.wasm",
        "name": "System.Diagnostics.Process.gwfmmwi48q.wasm",
        "hash": "sha256-AHCge2JGQOUbiwZBrlUdX5C8GIKYDCmI6XhruRzJQjU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Diagnostics.TraceSource.wasm",
        "name": "System.Diagnostics.TraceSource.1p1c01lbj5.wasm",
        "hash": "sha256-lUdTK8N9fm0VOgH5OxXkPUB7daTM3gojZLQH2PuP5PI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Drawing.Primitives.wasm",
        "name": "System.Drawing.Primitives.hktl0q4cza.wasm",
        "hash": "sha256-8XA2cu9I0CR4wCqvzpRCjQZJLQQoFWSLx96dvN6N09I=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Drawing.wasm",
        "name": "System.Drawing.czszwx223f.wasm",
        "hash": "sha256-iJ7JNDOlOIHr1bnPW/3nsyN9yOR9Bf/adENn6fFmH+g=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Formats.Asn1.wasm",
        "name": "System.Formats.Asn1.lxfj5rieho.wasm",
        "hash": "sha256-aejEkkw7NORDbgLULY5ZNPtUZ6xBFCPY8KUy2rC/2jU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Globalization.wasm",
        "name": "System.Globalization.ecfq4pzq2t.wasm",
        "hash": "sha256-0vfkV/41J1aInXlSTk9n+UYjeK/f+n3IakMUqL7KdxM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Compression.wasm",
        "name": "System.IO.Compression.254v5sh9dx.wasm",
        "hash": "sha256-SnSuhGvle6fsdnlsSCHpBhvXxsYOEL3x6W7GUpkhVrw=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.Primitives.wasm",
        "name": "System.IO.FileSystem.Primitives.p5e3q3sdae.wasm",
        "hash": "sha256-bbCdrwGt2l8iYKl0LmGMpqG8UoDoka0n8hjTNJ+Mj3k=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.FileSystem.wasm",
        "name": "System.IO.FileSystem.rc5vhdmypq.wasm",
        "hash": "sha256-Je6aszYuy4iaUwFVUCfhyhef0pe00oZfrYE+K8pPeaU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.Pipelines.wasm",
        "name": "System.IO.Pipelines.rn8hjkm2d3.wasm",
        "hash": "sha256-F+momQ5WP4xfA/Vq9jEIevqHrrx2YXHsyrF9owt1h8w=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.IO.wasm",
        "name": "System.IO.1meus1c8t1.wasm",
        "hash": "sha256-h1pKenunIPKPEYL07jkRr1zsfVSRthSvL0DmjtqJL/k=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.Expressions.wasm",
        "name": "System.Linq.Expressions.phfg68ziby.wasm",
        "hash": "sha256-bUjtbLcENllUJkIZsaKFAAWt+2Ro+E85XODFWHnIDKA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Linq.wasm",
        "name": "System.Linq.dpwkrlttv8.wasm",
        "hash": "sha256-b57Mwgb/1e/zPQictrVUE1QluJfn7gsV7CpJknww6wg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Memory.wasm",
        "name": "System.Memory.ukctki0bj5.wasm",
        "hash": "sha256-7sBIpaMhS90CZgoiNIDxYZEIffufsDYQVr416slqOH0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Memory.Data.wasm",
        "name": "System.Memory.Data.0494l5ivm6.wasm",
        "hash": "sha256-IFC59OmmgBRDP7CV90h7Ik6Ywxmoq+Aod0kY7xUBlG8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Http.wasm",
        "name": "System.Net.Http.ffm8eu1m5f.wasm",
        "hash": "sha256-dUZqKVImoY2saf3WUT33FJ/gMNV6hiXwxZvR1Q6NNv4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Primitives.wasm",
        "name": "System.Net.Primitives.6iv8511073.wasm",
        "hash": "sha256-firTOsGFsR0zB6HbThGC0vAfIHeHkgnnOTN8BIyjkkQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Security.wasm",
        "name": "System.Net.Security.b5618wfwbw.wasm",
        "hash": "sha256-obI40abpkUqvWT/1S5FFMV7yBzmi81x+lT5ptiPUpjE=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.Sockets.wasm",
        "name": "System.Net.Sockets.qio2p827h3.wasm",
        "hash": "sha256-Xl6JlzZW7kilMx9dyRlFNMpoePJwguRArk452p31Hrc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebSockets.Client.wasm",
        "name": "System.Net.WebSockets.Client.x8gcsuali8.wasm",
        "hash": "sha256-v27W7fFACsSpdCiHLDcFNXTir/h9geoOOR5LyOi6H8M=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Net.WebSockets.wasm",
        "name": "System.Net.WebSockets.5yj15qpkek.wasm",
        "hash": "sha256-w4e2A8dcTlOgUBFPi8HKr8f81otDLPXzHn8o5+QD+hU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Numerics.Vectors.wasm",
        "name": "System.Numerics.Vectors.xofiios48n.wasm",
        "hash": "sha256-8ZEsFc6QvOMLZRFVYn/BOFlpUhJOarM54h5BtzOcYdQ=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.ObjectModel.wasm",
        "name": "System.ObjectModel.m99lmhlf8x.wasm",
        "hash": "sha256-1hP6Hrniwh7rSv1rR7UuQe7lqwbyH+VYOhSXr3QRDtM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.CoreLib.wasm",
        "name": "System.Private.CoreLib.zuq6lsz7zi.wasm",
        "hash": "sha256-hR15raQfFiW3zP6GQ40T+3kD8whyHQQYbbuGRnNI+ts=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Uri.wasm",
        "name": "System.Private.Uri.p6uqn611nf.wasm",
        "hash": "sha256-r0V/+jZtW+69egORqxnnbQHVMrwfdpEkkdYVGuuJVV4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Private.Xml.wasm",
        "name": "System.Private.Xml.0eldahs2om.wasm",
        "hash": "sha256-eOROp9cfmOC4o4uik6hAj41M5cNuA3WBgE+8ie/t5do=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reactive.wasm",
        "name": "System.Reactive.3a4myi2bhc.wasm",
        "hash": "sha256-1Xa9Syjn6hf5xQrXRkCkV79HH9c1LS8k09g72y6NQGg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.Extensions.wasm",
        "name": "System.Reflection.Extensions.hepg4o389g.wasm",
        "hash": "sha256-Zwc2GdtbT0OPfhI0H2zM2CejxwUt9ekdVK2EkXc12x0=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Reflection.wasm",
        "name": "System.Reflection.pk2p4oosm0.wasm",
        "hash": "sha256-ST02ZD7eMhMjTJP7+NM7rhW+oX+JPxm1aVvx+6bGmrA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.wasm",
        "name": "System.Runtime.cfqi6in2zt.wasm",
        "hash": "sha256-uGtr/ZlqrQDr7KrPK2+cirHxYJ3Ab47MMlq5XEL7vuc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Extensions.wasm",
        "name": "System.Runtime.Extensions.xsb9tugazi.wasm",
        "hash": "sha256-AZk3SkotNgVzWELzIw9VmHAsCeSofzWMWfW/uuQWcvc=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.wasm",
        "name": "System.Runtime.InteropServices.scy56fjhwn.wasm",
        "hash": "sha256-1PB1azlUun71h+NxlrXfgI3uUB5yCVQ5Onhj4BkjpRA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.InteropServices.JavaScript.wasm",
        "name": "System.Runtime.InteropServices.JavaScript.xv18btnd0r.wasm",
        "hash": "sha256-cxXMlQxubnvtvhRQP2ZbhYicjZmEGlID9XGWcDB/CtI=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Numerics.wasm",
        "name": "System.Runtime.Numerics.h6vk7yq04i.wasm",
        "hash": "sha256-2an/UYBwtldzfBlft9C37aDmnBmgU6VB9EpvXtr5UoA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Formatters.wasm",
        "name": "System.Runtime.Serialization.Formatters.ybnvrkg1qu.wasm",
        "hash": "sha256-3pIgE3P2IEBkv3+ZD8dSqHefYVBa0bndfmmWL5d9WZM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Runtime.Serialization.Primitives.wasm",
        "name": "System.Runtime.Serialization.Primitives.cjdzri177t.wasm",
        "hash": "sha256-SIKKQ2YoFEF0iHYcSZuzYupQUGQ068XVO0nGhpOd1FU=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Security.Cryptography.wasm",
        "name": "System.Security.Cryptography.9vw5l9aois.wasm",
        "hash": "sha256-WzFjMv+4ZW6vKX0+ggQ6JPz28RUqYvJWg/4zsIVSoW8=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.CodePages.wasm",
        "name": "System.Text.Encoding.CodePages.4kdf7kp58p.wasm",
        "hash": "sha256-ZPleSxC4p95/QSbeXf9yDI9hMfHe1yRK9eLhb8dq9y4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.Extensions.wasm",
        "name": "System.Text.Encoding.Extensions.1uq5f7gico.wasm",
        "hash": "sha256-531D008FjDTjPr0whjckTfggKHSkkJHZL7PB12VuHMM=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encoding.wasm",
        "name": "System.Text.Encoding.1j1txz2hrd.wasm",
        "hash": "sha256-/TlmZF0y0eptLx+uGkVjbk84eB6BVfJE0Dk6mGGUyCA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Encodings.Web.wasm",
        "name": "System.Text.Encodings.Web.s1g4bd2nws.wasm",
        "hash": "sha256-wLbiSdSNOGrMjkAHcrF3CNYcnktv4oiZCJ6a/6wENKA=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.Json.wasm",
        "name": "System.Text.Json.ewsybtt8zk.wasm",
        "hash": "sha256-Mur+OvAsVYnHGFFVrPUw4QnJrMm23b3MFHuJXkU3Rio=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Text.RegularExpressions.wasm",
        "name": "System.Text.RegularExpressions.7ayjm8uqla.wasm",
        "hash": "sha256-sXV1JQE3Dzb5U4Vue//ruk2YS9+hFvtH6aKPAAqNwGg=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.wasm",
        "name": "System.Threading.tcuh5ja5i6.wasm",
        "hash": "sha256-ExthNQkARBGlpQJEhVCOjyOrztXqgqMZSUc+p9w7Sh4=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.Threading.Tasks.wasm",
        "name": "System.Threading.Tasks.nrth5ri6zz.wasm",
        "hash": "sha256-88FvVf19vINHppofMNzg6Iy1TENlsy6NDf1twHOlY9E=",
        "cache": "force-cache"
      },
      {
        "virtualPath": "System.wasm",
        "name": "System.nnqvudo0vd.wasm",
        "hash": "sha256-TbXBiQa38E2TrjWSwZYk9l9sTztp8pQ4MLJyVPBpaEk=",
        "cache": "force-cache"
      }
    ],
    "assembly": [],
    "satelliteResources": {
      "cs": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.q16nouwq5r.wasm",
          "hash": "sha256-orGzm77EIpWzmjLUY2eKAzZzy9quKpuZYBNgaHh3lzg=",
          "cache": "force-cache"
        }
      ],
      "de": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.2jz30jtwg5.wasm",
          "hash": "sha256-Yi2CEX5Cly2n2aT79LAK61Uhgz0qi5D4j1bJJvXzF90=",
          "cache": "force-cache"
        }
      ],
      "es": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.e9ue1cagee.wasm",
          "hash": "sha256-soE0lhcwrz52IBbxKcwurmnOon2e1ni8rA75DnwseIw=",
          "cache": "force-cache"
        }
      ],
      "fr": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.o94dizl1oi.wasm",
          "hash": "sha256-3KQ15QzFIYQBBQhPZ97t01lhjXgJC3apeZQWjrDHeq8=",
          "cache": "force-cache"
        }
      ],
      "it": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.xmxkyh0hr3.wasm",
          "hash": "sha256-SW00BQMqsz48L8Y8u/luupQePyzneOSKwLHfQ23tk6E=",
          "cache": "force-cache"
        }
      ],
      "ja": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.o16m5v6vcg.wasm",
          "hash": "sha256-HOlZk5Sh6MwAkyhVQjEejvjrhQO7uSHC3hDPtZYF7zU=",
          "cache": "force-cache"
        }
      ],
      "ko": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.jbxexav7hg.wasm",
          "hash": "sha256-0Ya1e74TXS/S2KJWzd5yQVrmfO0b5FjDSrn+btGgzr8=",
          "cache": "force-cache"
        }
      ],
      "pl": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.hitypseivh.wasm",
          "hash": "sha256-6dL5nUNEuWsldKZGwr5l/QQPaMjH42tk1gXvp9QwJMs=",
          "cache": "force-cache"
        }
      ],
      "pt-BR": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.rlcfdnkgqi.wasm",
          "hash": "sha256-jxYw5DhNO8ktzMBe4Lqq2g8hG9hhi3XqVqQ9Q7RscJk=",
          "cache": "force-cache"
        }
      ],
      "ru": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.zczknt8x1q.wasm",
          "hash": "sha256-/USnVvJswl3YoCIGDDjCb9/z2BsyHH6SFS7oE2AImRk=",
          "cache": "force-cache"
        }
      ],
      "tr": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.c28plajk40.wasm",
          "hash": "sha256-FMIlt8n0DvBhfvwsAoNvxI2ltgd2z044BOZEL/rbBP4=",
          "cache": "force-cache"
        }
      ],
      "zh-Hans": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.ettmg1mpp7.wasm",
          "hash": "sha256-jmu2MclmUL2E2rP7Bq4SUyhTl6vXgSalsdJ6H+nTfpI=",
          "cache": "force-cache"
        }
      ],
      "zh-Hant": [
        {
          "virtualPath": "System.CommandLine.resources.wasm",
          "name": "System.CommandLine.resources.92dzehn8x8.wasm",
          "hash": "sha256-H7GN+9LiFNDwjaHx1wpMf6qxbvl1OPomTK5Hmx2EEeA=",
          "cache": "force-cache"
        }
      ]
    }
  },
  "debugLevel": 0,
  "linkerEnabled": true,
  "globalizationMode": "sharded",
  "runtimeConfig": {
    "runtimeOptions": {
      "configProperties": {
        "Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability": true,
        "System.ComponentModel.DefaultValueAttribute.IsSupported": false,
        "System.ComponentModel.Design.IDesignerHost.IsSupported": false,
        "System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization": false,
        "System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported": false,
        "System.Data.DataSet.XmlSerializationIsSupported": false,
        "System.Diagnostics.Debugger.IsSupported": false,
        "System.Diagnostics.Metrics.Meter.IsSupported": false,
        "System.Diagnostics.Tracing.EventSource.IsSupported": false,
        "System.Globalization.Invariant": false,
        "System.TimeZoneInfo.Invariant": false,
        "System.Linq.Enumerable.IsSizeOptimized": true,
        "System.Net.Http.EnableActivityPropagation": false,
        "System.Net.Http.WasmEnableStreamingResponse": true,
        "System.Net.SocketsHttpHandler.Http3Support": false,
        "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
        "System.Resources.ResourceManager.AllowCustomResourceTypes": false,
        "System.Resources.UseSystemResourceKeys": true,
        "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported": true,
        "System.Runtime.InteropServices.BuiltInComInterop.IsSupported": false,
        "System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting": false,
        "System.Runtime.InteropServices.EnableCppCLIHostActivation": false,
        "System.Runtime.InteropServices.Marshalling.EnableGeneratedComInterfaceComImportInterop": false,
        "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
        "System.StartupHookProvider.IsSupported": false,
        "System.Text.Encoding.EnableUnsafeUTF7Encoding": false,
        "System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault": false,
        "System.Threading.Thread.EnableAutoreleasePool": false
      }
    }
  }
}/*json-end*/);export{gt as default,ft as dotnet,mt as exit};
