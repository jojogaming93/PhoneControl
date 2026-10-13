# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param(
    [Parameter(Mandatory)][ValidateSet('push','workflow_dispatch')][string]$EventName,
    [Parameter(Mandatory)][ValidateSet('branch','tag')][string]$RefType,
    [Parameter(Mandatory)][string]$RefName,
    [string]$InputVersion,
    [string]$RunId,
    [string]$RunAttempt
)
$ErrorActionPreference = 'Stop'
$identityScript = Join-Path $PSScriptRoot 'Get-ReleaseIdentity.ps1'
$sourceBranch = ''
if ($RefType -eq 'tag') {
    if ($EventName -ne 'push') { throw 'Stable releases require an explicitly pushed version tag.' }
    $identity = & $identityScript -Version ($RefName -creplace '^v', '')
    if ($identity.Channel -ne 'stable') { throw 'Only stable version tags trigger releases; branch pushes produce prereleases.' }
    $version = $identity.Version
    $tag = $RefName
} else {
    if ($RunId -cnotmatch '^[1-9][0-9]*$' -or $RunAttempt -cnotmatch '^[1-9][0-9]*$') { throw 'A numeric CI run ID and attempt are required for previews.' }
    [xml]$configuration = Get-Content (Join-Path (Split-Path -Parent $PSScriptRoot) 'version.props') -Raw
    $baseVersion = [string]$configuration.Project.PropertyGroup.VersionPrefix
    if ($RefName.StartsWith('release/')) {
        $baseVersion = $RefName.Substring('release/'.Length)
        $channel = 'rc'
    } elseif ($RefName -ceq 'main') {
        $channel = 'beta'
    } elseif ($RefName.StartsWith('feature/') -or $RefName.StartsWith('codex/')) {
        # Keep similar/truncated branch names distinct, including case and punctuation.
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($RefName))).Substring(0, 12).ToLowerInvariant()
        $slug = ($RefName.Substring($RefName.IndexOf('/') + 1).ToLowerInvariant() -replace '[^a-z0-9-]', '-').Trim('-')
        if (-not $slug -or $slug -cmatch '^[0-9]') { $slug = "feature-$slug" }
        if ($slug.Length -gt 40) { $slug = $slug.Substring(0, 40).TrimEnd('-') }
        $channel = "alpha.$slug-$hash"
    } else {
        throw 'Preview releases require main, feature/*, codex/* or release/version.'
    }
    if ($InputVersion) {
        if ($RefName.StartsWith('release/') -and $InputVersion -cne $baseVersion) { throw 'Manual version must match the release branch.' }
        $baseVersion = $InputVersion
    }
    $baseIdentity = & $identityScript -Version $baseVersion
    if ($baseIdentity.Channel -ne 'stable') { throw 'The target release version must be a plain major.minor.patch.' }
    $version = "$baseVersion-$channel.$RunId.$RunAttempt"
    $identity = & $identityScript -Version $version
    $tag = "v$version"
    $sourceBranch = $RefName
}
[pscustomobject]@{
    Version = $version
    Tag = $tag
    Channel = $identity.Channel
    BaseVersion = $identity.BaseVersion
    SourceBranch = $sourceBranch
    Retention = $(if ($identity.Channel -eq 'alpha') { 10 } elseif ($identity.Channel -eq 'beta') { 20 } else { 0 })
}
