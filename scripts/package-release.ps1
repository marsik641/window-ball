param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Test }
$executable = Join-Path $projectRoot 'bin\WindowBall.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build bin/WindowBall.exe first.' }
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid product version: $version" }
$notesSource = Join-Path $projectRoot "docs\releases\v$version.md"
if (-not (Test-Path -LiteralPath $notesSource)) { throw "Release notes missing: $notesSource" }
$releaseRoot = Join-Path $projectRoot "dist\v$version"
$stageRoot = Join-Path $releaseRoot "WindowBall-v$version-win-portable"
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
$files = [ordered]@{
    'WindowBall.exe' = 'bin\WindowBall.exe'
    'WindowBall.exe.config' = 'bin\WindowBall.exe.config'
    'Запустить.cmd' = 'packaging\Запустить.cmd'
    'LICENSE' = 'LICENSE'
    'CHANGELOG.md' = 'docs\CHANGELOG.md'
}
$archiveFiles = @()
foreach ($file in $files.GetEnumerator()) {
    $destination = Join-Path $stageRoot $file.Key
    Copy-Item -LiteralPath (Join-Path $projectRoot $file.Value) -Destination $destination -Force
    $archiveFiles += $destination
}
$readme = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'packaging\PORTABLE.md')).Replace('{{VERSION}}', $version)
$readmePath = Join-Path $stageRoot 'README.md'
[System.IO.File]::WriteAllText($readmePath, $readme, [System.Text.UTF8Encoding]::new($true))
$archiveFiles += $readmePath
$archive = Join-Path $releaseRoot "WindowBall-v$version-win-portable.zip"
Compress-Archive -LiteralPath $archiveFiles -DestinationPath $archive -CompressionLevel Optimal -Force
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $releaseRoot 'SHA256SUMS.txt'), "$checksum  $([System.IO.Path]::GetFileName($archive))`n", [System.Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $notesSource -Destination (Join-Path $releaseRoot 'RELEASE_NOTES.md') -Force
$releaseReadme = @(
    "# Window Ball v$version",
    '',
    "Файлы для ручной публикации релиза v$version на GitHub.",
    '',
    "- WindowBall-v$version-win-portable.zip — переносимая программа для загрузки в Release.",
    '- SHA256SUMS.txt — контрольная сумма архива.',
    '- RELEASE_NOTES.md — готовый текст описания релиза.',
    "- WindowBall-v$version-win-portable/ — содержимое архива до упаковки.",
    '',
    'Каталог создаёт scripts/package-release.ps1. Программа публикуется вручную.'
) -join "`n"
[System.IO.File]::WriteAllText((Join-Path $releaseRoot 'README.md'), $releaseReadme + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Release files: $releaseRoot"
Get-ChildItem -LiteralPath $releaseRoot -File | Select-Object Name,Length
