import { dotnet } from './_framework/dotnet.js'
import { WebAudioWavePlayer } from './WebAudioWavePlayer.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

// Register WebAudioWavePlayer module for JSImport
await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);
dotnetRuntime.setModuleImports("WebAudioWavePlayer", WebAudioWavePlayer);

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
