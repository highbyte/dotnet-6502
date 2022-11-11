#!/bin/bash

rm -rf ./coveragereport
rm -rf ./Tests/Highbyte.DotNet6502.Tests/TestResults
dotnet test Tests/Highbyte.DotNet6502.Tests --filter TestType!=Integration --collect:"XPlat Code Coverage"
reportgenerator -reports:./**/TestResults/**/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:TextSummary
cat ./coveragereport/Summary.txt

