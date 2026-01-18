# Silk.NET desktop app

## General prerequisites
Requires a GPU with OpenGL drivers.

## Compatibility Matrix

| OS / Architecture | x64 | arm64 |
|-------------------|-----|-------|
| **Windows**       | ✅ Works | ❌ Not working  |
| **macOS**         | ➖ N/A | ✅ Works |
| **Linux**         | ⚠️ Works* | ❌ Not working |

*May require additional packages (see below)

## Notes
### Windows x64
Tested on Windows 11 (x64). No extra configuration.

### Windows arm64
Tested on Windows 11 (arm64) running in VM on a M1 Mac. Not working.

Exception below. Not investigated, but maybe the Glfw library that Silk.NET uses is missing Windows arm64 native libraries?

```
Failed to create Silk.NET window: Couldn't find a suitable window platform. (GlfwPlatform - not applicable) https://dotnet.github.io/Silk.NET/docs/hlu/troubleshooting.html
Stack trace:    at Silk.NET.Windowing.Window.Create(WindowOptions options)
   at Program.<Main>$(String[] args) in C:\Users\highbyte\source\repos\dotnet-6502\src\apps\Highbyte.DotNet6502.App.SilkNetNative\Program.cs:line 94
```

### Mac arm64
Tested on MacBook Air M1, MacOS 26. No extra configuration.

### Linux x64
Should work.

If running in VM it requires a HyperVisor that supports GPUs.

#### Linux via WSLg under Windows
Tested on Ubuntu 22.04.5 (x64). 

Installing Mesa and Wayland packages was required when running on WSLg (Ubuntu 22.04.5) on Windows 11 (x64).

```bash
sudo apt install mesa-utils
sudo apt install libwayland-client0 libwayland-cursor0 libwayland-egl1 libxkbcommon0
```

### Linux arm64
Tested on Ubuntu 25.10. NOT working.

Several exceptions like below. Seems related to the ImGui native library. Is ImGui missing support for arm64 platform?

```
Exception in SilkNetHostApp.OnLoad(): Unable to load shared library 'cimgui' or one of its dependencies. In order to help diagnose loading problems, consider using a tool like strace. If you're using glibc, consider setting the LD_DEBUG environment variable: 
/home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SilkNetNative/publish/linux-arm64/cimgui.so: cannot open shared object file: No such file or directory
/home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SilkNetNative/publish/linux-arm64/libcimgui.so: cannot open shared object file: No such file or directory
/home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SilkNetNative/publish/linux-arm64/cimgui: cannot open shared object file: No such file or directory
/home/highbyte/source/repos/dotnet-6502/src/apps/Highbyte.DotNet6502.App.SilkNetNative/publish/linux-arm64/libcimgui: cannot open shared object file: No such file or directory

Stack trace:    at ImGuiNET.ImGuiNative.igCreateContext(ImFontAtlas* shared_font_atlas)
   at ImGuiNET.ImGuiNative.igCreateContext(ImFontAtlas* shared_font_atlas)
   at ImGuiNET.ImGui.CreateContext()
   at Silk.NET.OpenGL.Extensions.ImGui.ImGuiController.Init(GL gl, IView view, IInputContext input)
   at Silk.NET.OpenGL.Extensions.ImGui.ImGuiController..ctor(GL gl, IView view, IInputContext input, Nullable`1 imGuiFontConfig, Action onConfigureIO)
   at Silk.NET.OpenGL.Extensions.ImGui.ImGuiController..ctor(GL gl, IView view, IInputContext input)
   at Highbyte.DotNet6502.App.SilkNetNative.SilkNetHostApp.InitImGui()
   at Highbyte.DotNet6502.App.SilkNetNative.SilkNetHostApp.OnLoad()
```
