Remove-Item ./coveragereport -Force -Recurse -ErrorAction SilentlyContinue
Remove-Item ./Highbyte.DotNet6502.Tests/TestResults -Force -Recurse -ErrorAction SilentlyContinue
dotnet test Highbyte.DotNet6502.Tests --filter TestType!=Integration --collect:"XPlat Code Coverage"
reportgenerator -reports:./**/TestResults/**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html
./coveragereport/index.html
