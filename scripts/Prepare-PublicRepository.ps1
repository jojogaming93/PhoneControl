# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts/public/PhoneControl' }
$publicRoot = [IO.Path]::GetFullPath($OutputDirectory)
if ($publicRoot -eq [IO.Path]::GetFullPath($projectRoot) -or (Test-Path -LiteralPath $publicRoot)) {
    throw 'Choose a new public checkout directory; existing files are never overwritten.'
}
& python (Join-Path $PSScriptRoot 'Check-PublicSource.py')
if ($LASTEXITCODE -ne 0) { throw 'Source privacy check failed.' }
$files = & git -C $projectRoot -c core.quotePath=false ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate audited source.' }
New-Item -ItemType Directory -Path $publicRoot -Force | Out-Null
foreach ($relative in $files) {
    $source = Join-Path $projectRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
    $target = [IO.Path]::GetFullPath((Join-Path $publicRoot $relative))
    if (-not $target.StartsWith($publicRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Source path escaped the public checkout.'
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
function PublicGit([string[]]$Arguments) {
    & git -C $publicRoot @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Public git operation failed: $($Arguments[0])" }
}
PublicGit -Arguments @('-c','init.templateDir=','init','--initial-branch=main')
PublicGit -Arguments @('config','user.name','jojogaming93')
PublicGit -Arguments @('config','user.email','noreply@example.invalid')
PublicGit -Arguments @('config','commit.gpgsign','false')
PublicGit -Arguments @('config','core.hooksPath','.git/hooks')
PublicGit -Arguments @('remote','add','origin','https://github.com/jojogaming93/PhoneControl.git')
PublicGit -Arguments @('add','--all')
$identityVariables = @('GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL')
$previousIdentity = @{}
foreach ($name in $identityVariables) { $previousIdentity[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    foreach ($name in @('GIT_AUTHOR_NAME','GIT_COMMITTER_NAME')) { [Environment]::SetEnvironmentVariable($name, 'jojogaming93', 'Process') }
    foreach ($name in @('GIT_AUTHOR_EMAIL','GIT_COMMITTER_EMAIL')) { [Environment]::SetEnvironmentVariable($name, 'noreply@example.invalid', 'Process') }
    PublicGit -Arguments @('commit','-m','Initial public PhoneControl source under AGPLv3')
} finally {
    foreach ($name in $identityVariables) { [Environment]::SetEnvironmentVariable($name, $previousIdentity[$name], 'Process') }
}
& python (Join-Path $publicRoot 'scripts/Check-PublicSource.py') --history
if ($LASTEXITCODE -ne 0) { throw 'Public history privacy check failed.' }
$archive = "$publicRoot-source.zip"
PublicGit -Arguments @('archive','--format=zip',('--output=' + $archive),'HEAD')
Write-Output "Public checkout: $publicRoot"
Write-Output "Source-only upload ZIP: $archive"
Write-Output 'Prepared locally; nothing has been pushed.'
