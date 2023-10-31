## How to run SilkNetNative app on Mac M1
Currently issue with ImGui library on M1 Macs (ARM64).
https://github.com/mellinoe/ImGui.NET/issues/350
Fix:
Go to bin directory: 
cd ./bin/Debug/net70

Copy runtime file from osx-universal to arm64:
cp runtimes/osx-universal/native/libcimgui.dylib runtimes/osx-arm64/native

Then it should work to start it
./Highbyte.DotNet6502.App.SilkNetNative
