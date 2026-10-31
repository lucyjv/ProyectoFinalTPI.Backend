param([string]$Filter)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $runDirectory = Join-Path 'TestResults' ([Guid]::NewGuid().ToString())
    $testArguments = @('test', 'ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj', '--settings', 'coverage.runsettings', '--collect:XPlat Code Coverage', '--results-directory', $runDirectory)
    if ($Filter) { $testArguments += @('--filter', $Filter) }
    & dotnet @testArguments
    $testExitCode = $LASTEXITCODE
    $report = Get-ChildItem $runDirectory -Filter coverage.cobertura.xml -Recurse | Select-Object -First 1
    if (-not $report) { throw 'No se generó el archivo de cobertura. Revisá la salida de los tests.' }
    & dotnet reportgenerator "-reports:$($report.FullName)" '-targetdir:coverage-report' '-reporttypes:Html;TextSummary'
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el informe HTML.' }
    Get-Content 'coverage-report/Summary.txt'
    Write-Host "Informe: $(Join-Path $PSScriptRoot 'coverage-report/index.html')"
    exit $testExitCode
}
finally { Pop-Location }
