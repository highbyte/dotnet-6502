if(Test-Path "./bin/Publish/") { Remove-Item "./bin/Publish/" -r -force }

dotnet publish -c Release -o "./bin/Publish/"
# Workaround for publishing errors on .NET 9 on Windows: disable AOT compilation and trimming
#dotnet publish -c Release -o "./bin/Publish/" -p:RunAOTCompilation=false -p:PublishTrimmed=false

dotnet serve --port 5001 -o:/?audioEnabled=true --directory "./bin/Publish/wwwroot/"