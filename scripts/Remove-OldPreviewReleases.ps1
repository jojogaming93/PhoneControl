# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][ValidateSet('alpha','beta','rc','stable')][string]$Channel,
    [string]$SourceBranch,
    [Parameter(Mandatory)][string]$KeepTag
)
$ErrorActionPreference = 'Stop'
if ($Repository -notmatch '^[\w.-]+/[\w.-]+$') { throw 'Repository must be owner/name.' }
if ($Channel -in @('rc','stable')) { return }
if (($Channel -eq 'beta' -and $SourceBranch -cne 'main') -or
    ($Channel -eq 'alpha' -and $SourceBranch -cnotmatch '^(feature|codex)/.+')) { throw 'Retention channel must match its branch.' }
$limit = if ($Channel -eq 'alpha') { 10 } else { 20 }
$json = & gh api --paginate --slurp "repos/$Repository/releases"
if ($LASTEXITCODE -ne 0) { throw 'Could not list releases for retention.' }
$releases = @($json | ConvertFrom-Json) | ForEach-Object { $_ }
$owned = @($releases | Where-Object {
    if ($_.draft -or -not $_.prerelease -or $_.author.login -cne 'github-actions[bot]') { return $false }
    if ($_.body -cnotmatch '<!-- PhoneControl release metadata: (\{[^\r\n]*\}) -->') { return $false }
    try {
        $record = $Matches[1] | ConvertFrom-Json
        $identity = & (Join-Path $PSScriptRoot 'Get-ReleaseIdentity.ps1') -Version $record.version
        $parts = $record.version.Split('.')
        $expected = & (Join-Path $PSScriptRoot 'Resolve-ReleaseVersion.ps1') -EventName push -RefType branch -RefName $record.branch -InputVersion $identity.BaseVersion -RunId $parts[-2] -RunAttempt $parts[-1]
        return $record.schema -eq 1 -and $record.publisher -ceq 'phonecontrol-actions-v1' -and
            $record.channel -ceq $Channel -and $identity.Channel -ceq $Channel -and
            $record.branch -ceq $SourceBranch -and $record.sourceCommit -cmatch '^[0-9a-f]{40}$' -and
            $expected.Version -ceq $record.version -and $_.tag_name -ceq "v$($record.version)"
    } catch { return $false }
} | Sort-Object -Property @{Expression='published_at';Descending=$true}, @{Expression='id';Descending=$true})
# Pin the just-published tag and keep the newest remaining versions in this branch.
if (@($owned | Where-Object tag_name -CEQ $KeepTag).Count -ne 1) { throw 'The just-published preview is not in the owned release list; no cleanup performed.' }
$remove = @($owned | Where-Object tag_name -CNE $KeepTag | Select-Object -Skip ($limit - 1))
foreach ($release in $remove) {
    & gh release delete $release.tag_name --repo $Repository --yes --cleanup-tag
    if ($LASTEXITCODE -ne 0) { throw "Preview retention failed for $($release.tag_name)." }
    Write-Output "Removed old $Channel installer: $($release.tag_name)"
}
