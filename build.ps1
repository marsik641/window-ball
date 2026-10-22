param([switch]$Test, [switch]$SmokeTest)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework 4.8 compiler not found.' }
$sources = @('Program.cs', 'Native.cs', 'BallArt.cs', 'BallPhysics.cs', 'Settings.cs', 'SelfTests.cs', 'Properties\AssemblyInfo.cs') | ForEach-Object { Join-Path $projectRoot $_ }
$output = Join-Path $projectRoot 'WindowBall.exe'
$common = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', '/codepage:65001', '/warnaserror+', '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Runtime.Serialization.dll', "/win32manifest:$(Join-Path $projectRoot 'app.manifest')", "/out:$output")
$iconPath = Join-Path $projectRoot 'ball.ico'
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
    $testPath = Join-Path $projectRoot 'test-results.txt'
    $testProcess = Start-Process -FilePath $output -ArgumentList @('--self-test', ('"' + $testPath + '"')) -PassThru -WindowStyle Hidden
    if (-not $testProcess.WaitForExit(30000)) { Stop-Process -Id $testProcess.Id; throw 'Tests timed out.' }
    Get-Content -LiteralPath $testPath
    if ($testProcess.ExitCode -ne 0) { throw 'Tests failed.' }
}
if ($SmokeTest) {
    $smokePath = Join-Path $projectRoot 'smoke-results.txt'
    $smokeProcess = Start-Process -FilePath $output -ArgumentList @('--smoke-test', ('"' + $smokePath + '"')) -PassThru -WindowStyle Hidden
    if (-not $smokeProcess.WaitForExit(30000)) { Stop-Process -Id $smokeProcess.Id; throw 'UI smoke tests timed out.' }
    Get-Content -LiteralPath $smokePath
    if ($smokeProcess.ExitCode -ne 0) { throw 'UI smoke tests failed.' }
}
