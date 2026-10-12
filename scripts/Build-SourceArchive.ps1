# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param([Parameter(Mandatory)][string]$OutputFile, [Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
$identity = & (Join-Path $PSScriptRoot 'Get-ReleaseIdentity.ps1') -Version $Version
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
& python (Join-Path $PSScriptRoot 'Check-PublicSource.py')
if ($LASTEXITCODE -ne 0) { throw 'Public source privacy check failed.' }
$outputPath = [IO.Path]::GetFullPath($OutputFile)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
# Capture edited files and new source files, rather than archiving an older HEAD.
# Explicit source roots/formats exclude build output, keys and user settings.
$candidates = & git -C $projectRoot -c core.quotePath=false ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate repository source.' }
$rootFiles = @('.gitignore','.gitattributes','AGENTS.md','global.json','version.props','LICENSE','README.md','THIRD-PARTY-NOTICES.md','VALIDATION.md','SOURCE.md','PUBLIC-RELEASE.md','Install-Dependencies.ps1','Launch-PhoneControl.ps1')
$sourceFiles = @($candidates | Sort-Object -Unique | Where-Object {
    $_ -in $rootFiles -or $_ -eq 'server/LICENSE.scrcpy' -or
    ($_ -match '^(src|server|scripts|installer|assets|docs|\.github)/' -and
        $_ -match '\.(cs|csproj|xaml|manifest|json|java|ps1|xml|iss|cpp|h|rc|py|md|html|js|css|png|ico|jpg|svg|jar|yml|yaml)$')
} | Where-Object { Test-Path -LiteralPath (Join-Path $projectRoot $_) -PathType Leaf })
foreach ($required in @('LICENSE','SOURCE.md','global.json','version.props','src/PhoneControl/PhoneControl.csproj','src/PhoneControl/MainWindow.cs','scripts/Publish.ps1','scripts/Build-Installer.ps1','scripts/Build-SourceArchive.ps1','assets/phonecontrol-control.jar','server/PointersState.java','server/LICENSE.scrcpy')) {
    if ($required -notin $sourceFiles) { throw "Corresponding source is missing $required" }
}
Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open($outputPath, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
$entries = [Collections.Generic.List[object]]::new()
try {
    foreach ($relative in $sourceFiles) {
        $path = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
        if (-not $path.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escaped the repository.' }
        $entry = $zip.CreateEntry($relative.Replace('\','/'), [IO.Compression.CompressionLevel]::Optimal)
        $bytes = [IO.File]::ReadAllBytes($path)
        $entryStream = $entry.Open()
        try { $entryStream.Write($bytes, 0, $bytes.Length) } finally { $entryStream.Dispose() }
        $entries.Add([ordered]@{ path=$relative; sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant(); bytes=$bytes.Length })
    }
    $commit = & git -C $projectRoot rev-parse --verify HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { $commit = $null } # A recipient's freshly initialized source folder has no HEAD.
    $metadata = [ordered]@{ version=$Version; license='AGPL-3.0-only'; copyrightOwner='jojogaming93'; sourceCommit=$commit; sourceDirty=[bool](& git -C $projectRoot status --porcelain); files=@($entries.ToArray()) } | ConvertTo-Json -Depth 5
    $manifestEntry = $zip.CreateEntry('SOURCE-MANIFEST.json')
    $writer = [IO.StreamWriter]::new($manifestEntry.Open(), [Text.UTF8Encoding]::new($false))
    try { $writer.Write($metadata) } finally { $writer.Dispose() }
} finally { $zip.Dispose(); $stream.Dispose() }
Write-Output "Corresponding source: $outputPath ($($sourceFiles.Count) files)"
