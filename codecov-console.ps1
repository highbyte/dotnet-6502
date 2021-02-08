rm ./coveragereport -Force -Recurse
rm ./Highbyte.DotNet6502.Tests/TestResults -Force -Recurse
dotnet test --filter TestType!=Integration --collect:"XPlat Code Coverage"
reportgenerator -reports:./**/TestResults/**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:TextSummary
cat ./coveragereport/Summary.txt
