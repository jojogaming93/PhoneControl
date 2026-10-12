# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param(
    [Parameter(Mandatory)][ValidateSet('push','workflow_dispatch')][string]$EventName,
    [Parameter(Mandatory)][ValidateSet('branch','tag')][string]$RefType,
    [Parameter(Mandatory)][string]$RefName,
    [string]$InputVersion
)
$ErrorActionPreference = 'Stop'
$releaseBranch = ''
if ($RefType -eq 'branch' -and $RefName.StartsWith('release/')) {
    $version = $RefName.Substring('release/'.Length)
    $releaseBranch = $RefName
    if ($InputVersion -and $InputVersion -cne $version) { throw 'Manual version must match the release branch.' }
    $tag = "v$version"
} elseif ($EventName -eq 'push' -and $RefType -eq 'tag') {
    $tag = $RefName
    $version = $tag -creplace '^v', ''
} elseif ($EventName -eq 'workflow_dispatch') {
    $version = $InputVersion
    $tag = "v$version"
} else {
    throw 'Releases require a release/version branch, a version tag, or a manual version.'
}
# Canonical numeric versions avoid ambiguous leading-zero branches and tags.
if ($version -cnotmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw 'Release version must be major.minor.patch without leading zeros.'
}
[pscustomobject]@{ Version = $version; Tag = $tag; ReleaseBranch = $releaseBranch }
