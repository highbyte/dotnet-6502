$publishDir = "./bin/Publish/"
$path = "/?audioEnabled=true"
if(Test-Path $publishDir) { del $publishDir -r -force }
dotnet publish -c Release -o $publishDir
dotnet serve -o:$path --directory "$($publishDir)wwwroot\" 