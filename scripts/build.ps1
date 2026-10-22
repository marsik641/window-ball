param([switch]$Test, [switch]$SmokeTest)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$binRoot = Join-Path $projectRoot 'bin'
$reportRoot = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $binRoot, $reportRoot -Force | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework 4.8 compiler not found.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -Recurse -File | Sort-Object FullName | Select-Object -ExpandProperty FullName)
$sources += Join-Path $projectRoot 'tests\SelfTests.cs'
$output = Join-Path $binRoot 'WindowBall.exe'
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\WindowBall.exe.config') -Destination (Join-Path $binRoot 'WindowBall.exe.config') -Force
$common = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', '/codepage:65001', '/warnaserror+', '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Runtime.Serialization.dll', "/win32manifest:$(Join-Path $projectRoot 'packaging\app.manifest')", "/out:$output")
$iconPath = Join-Path $projectRoot 'assets\ball.ico'
if (-not (Test-Path -LiteralPath $iconPath)) {
    & $compilerPath @common @sources
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap compilation failed.' }
    $iconProcess = Start-Process -FilePath $output -ArgumentList @('--make-icon', ('"' + $iconPath + '"')) -Wait -PassThru -WindowStyle Hidden
    if ($iconProcess.ExitCode -ne 0) { throw 'Icon generation failed.' }
}
& $compilerPath @common "/win32icon:$iconPath" @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built: $output"
if ($Test) {
    $testPath = Join-Path $reportRoot 'test-results.txt'
    $testProcess = Start-Process -FilePath $output -ArgumentList @('--self-test', ('"' + $testPath + '"')) -PassThru -WindowStyle Hidden
    if (-not $testProcess.WaitForExit(30000)) { $testProcess.Kill(); throw 'Tests timed out.' }
    Get-Content -LiteralPath $testPath
    if ($testProcess.ExitCode -ne 0) { throw 'Tests failed.' }
}
if ($SmokeTest) {
    $smokePath = Join-Path $reportRoot 'smoke-results.txt'
    $smokeProcess = Start-Process -FilePath $output -ArgumentList @('--smoke-test', ('"' + $smokePath + '"')) -PassThru -WindowStyle Hidden
    if (-not $smokeProcess.WaitForExit(30000)) { $smokeProcess.Kill(); throw 'UI smoke tests timed out.' }
    Get-Content -LiteralPath $smokePath
    if ($smokeProcess.ExitCode -ne 0) { throw 'UI smoke tests failed.' }
}
