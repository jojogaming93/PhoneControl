# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [string]$SourceBranch
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$' -or ($Tag -replace '^v', '') -ne $Version) { throw 'Release tag must match the installer version.' }
if ($Commit -notmatch '^[0-9a-f]{40}$') { throw 'A full source commit SHA is required.' }
if ($Repository -notmatch '^[\w.-]+/[\w.-]+$') { throw 'Repository must be owner/name.' }
if ($SourceBranch) {
    if ($SourceBranch -cne "release/$Version") { throw 'Release branch must match the installer version.' }
    $branchRef = @(& git ls-remote --heads "https://github.com/$Repository.git" "refs/heads/$SourceBranch")
    if ($LASTEXITCODE -ne 0 -or $branchRef.Count -ne 1) { throw 'Could not verify the release branch.' }
    if (($branchRef[0] -split '\s+')[0] -ne $Commit) {
        Write-Output 'Skipping superseded release build: the branch has a newer source commit.'
        return
    }
}
$files = @(
    "PhoneControl-Setup-$Version-win-x64.exe",
    "PhoneControl-Setup-$Version-win-x64.exe.sha256",
    "PhoneControl-$Version-source.zip",
    "PhoneControl-$Version-manifest.json"
) | ForEach-Object {
    $path = Join-Path $ArtifactDirectory $_
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release file missing: $_" }
    (Resolve-Path -LiteralPath $path).Path
}
$manifest = Get-Content -LiteralPath $files[3] -Raw | ConvertFrom-Json
if ($manifest.version -ne $Version -or $manifest.sourceCommit -ne $Commit -or $manifest.sourceDirty -ne $false) { throw 'Release manifest must match the version/source commit and have a clean source tree.' }
$installerHash = (Get-FileHash -LiteralPath $files[0] -Algorithm SHA256).Hash.ToLowerInvariant()
if ($installerHash -ne $manifest.installerSha256 -or
    (Get-Content -LiteralPath $files[1] -Raw).Trim() -ne "$installerHash  $([IO.Path]::GetFileName($files[0]))" -or
    (Get-FileHash -LiteralPath $files[2] -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.sourceArchiveSha256) {
    throw 'Release installer/source checksum verification failed.'
}
# Read tags through Git so an absent tag is distinguishable from an API/network failure.
$remoteTag = @(& git ls-remote --tags "https://github.com/$Repository.git" "refs/tags/$Tag" "refs/tags/$Tag^{}")
if ($LASTEXITCODE -ne 0) { throw 'Could not check the remote release tag.' }
if ($remoteTag.Count -gt 0) {
    $tagCommit = ($remoteTag[-1] -split '\s+')[0]
    if ($tagCommit -ne $Commit) { throw 'Release tag points to a different source commit. Use a new version.' }
}
$releasesJson = & gh api --paginate --slurp "repos/$Repository/releases"
if ($LASTEXITCODE -ne 0) { throw 'Could not list GitHub releases.' }
$releases = @($releasesJson | ConvertFrom-Json) | ForEach-Object { $_ }
$matchingVersion = @($releases | Where-Object { ($_.tag_name -replace '^v', '') -eq $Version })
if ($matchingVersion | Where-Object { -not $_.draft }) { throw 'This version already has a published release. Use a new version; published assets are not replaced.' }
if ($matchingVersion | Where-Object { $_.tag_name -cne $Tag }) { throw 'This version already has a draft under a different tag spelling. Reuse that tag or choose a new version.' }
$existing = $matchingVersion | Where-Object tag_name -eq $Tag
$notesPath = Join-Path $ArtifactDirectory 'release-notes.md'
@"
PhoneControl $Version for Intel/AMD x64 Windows 11 24H2 or newer.

Download ``PhoneControl-Setup-$Version-win-x64.exe`` to install. The installer bundles the required runtimes and USB tools. Saved profiles survive updates and uninstall.

The corresponding editable source ZIP, SHA-256 checksum and build manifest are attached. PhoneControl is licensed under AGPL-3.0-only; bundled components retain their own notices.

Source commit: $Commit
"@ | Set-Content -LiteralPath $notesPath -Encoding utf8
if ($existing) {
    & gh release upload $Tag @files --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Release asset upload failed.' }
    & gh release edit $Tag --repo $Repository --target $Commit --title "PhoneControl $Version" --notes-file $notesPath --draft --prerelease --latest=false
} else {
    & gh release create $Tag @files --repo $Repository --target $Commit --title "PhoneControl $Version" --notes-file $notesPath --draft --prerelease --latest=false
}
if ($LASTEXITCODE -ne 0) { throw 'GitHub draft release creation/update failed.' }
Write-Output "Draft release with installer: https://github.com/$Repository/releases"
