rm ./coveragereport -Force -Recurse -ErrorAction SilentlyContinue
rm ./Tests/Highbyte.DotNet6502.Tests/TestResults -Force -Recurse -ErrorAction SilentlyContinue
dotnet test Tests/Highbyte.DotNet6502.Tests --filter TestType!=Integration --collect:"XPlat Code Coverage"
reportgenerator -reports:./**/TestResults/**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html
./coveragereport/index.html
