param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $projectRoot 'build.ps1') -Test }
$executable = Join-Path $projectRoot 'WindowBall.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build WindowBall.exe first.' }
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid product version: $version" }
$notesSource = Join-Path $projectRoot "docs\releases\v$version.md"
if (-not (Test-Path -LiteralPath $notesSource)) { throw "Release notes missing: $notesSource" }
$releaseRoot = Join-Path $projectRoot "dist\v$version"
$stageRoot = Join-Path $releaseRoot "WindowBall-v$version-win-portable"
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
$files = @('WindowBall.exe', 'WindowBall.exe.config', 'Запустить.cmd', 'LICENSE', 'CHANGELOG.md')
$archiveFiles = @()
foreach ($name in $files) {
    $destination = Join-Path $stageRoot $name
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $destination -Force
    $archiveFiles += $destination
}
$readme = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'docs\PORTABLE.txt')).Replace('{{VERSION}}', $version)
$readmePath = Join-Path $stageRoot 'README.txt'
[System.IO.File]::WriteAllText($readmePath, $readme, [System.Text.UTF8Encoding]::new($true))
$archiveFiles += $readmePath
$archive = Join-Path $releaseRoot "WindowBall-v$version-win-portable.zip"
Compress-Archive -LiteralPath $archiveFiles -DestinationPath $archive -CompressionLevel Optimal -Force
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $releaseRoot 'SHA256SUMS.txt'), "$checksum  $([System.IO.Path]::GetFileName($archive))`n", [System.Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $notesSource -Destination (Join-Path $releaseRoot 'RELEASE_NOTES.md') -Force
Write-Output "Release files: $releaseRoot"
Get-ChildItem -LiteralPath $releaseRoot -File | Select-Object Name,Length
