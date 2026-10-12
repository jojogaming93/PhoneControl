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
$identity = & (Join-Path $PSScriptRoot 'Get-ReleaseIdentity.ps1') -Version $Version
if (($Tag -creplace '^v', '') -cne $Version) { throw 'Release tag must match the installer version.' }
if ($Commit -cnotmatch '^[0-9a-f]{40}$') { throw 'A full source commit SHA is required.' }
if ($Repository -notmatch '^[\w.-]+/[\w.-]+$') { throw 'Repository must be owner/name.' }
if ($identity.IsPreview) {
    if (-not $SourceBranch) { throw 'Preview releases require their source branch.' }
    $parts = $Version.Split('.')
    $resolved = & (Join-Path $PSScriptRoot 'Resolve-ReleaseVersion.ps1') -EventName push -RefType branch -RefName $SourceBranch -InputVersion $identity.BaseVersion -RunId $parts[-2] -RunAttempt $parts[-1]
    if ($resolved.Version -cne $Version) { throw 'Preview version does not match its source branch.' }
} elseif ($SourceBranch) { throw 'Stable releases require a version tag, not a branch.' }
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
if ($manifest.version -cne $Version -or $manifest.sourceCommit -cne $Commit -or $manifest.sourceDirty -ne $false -or
    $manifest.channel -cne $identity.Channel -or $manifest.sourceBranch -cne $SourceBranch -or
    $manifest.numericVersion -cne $identity.NumericVersion -or $manifest.appId -cne $identity.AppId) {
    throw 'Release manifest must match the version, channel, source commit, branch and clean source tree.'
}
$installerHash = (Get-FileHash -LiteralPath $files[0] -Algorithm SHA256).Hash.ToLowerInvariant()
if ($installerHash -ne $manifest.installerSha256 -or
    (Get-Content -LiteralPath $files[1] -Raw).Trim() -ne "$installerHash  $([IO.Path]::GetFileName($files[0]))" -or
    (Get-FileHash -LiteralPath $files[2] -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.sourceArchiveSha256) {
    throw 'Release installer/source checksum verification failed.'
}
$remoteTag = @(& git ls-remote --tags "https://github.com/$Repository.git" "refs/tags/$Tag" "refs/tags/$Tag^{}")
if ($LASTEXITCODE -ne 0) { throw 'Could not check the remote release tag.' }
if ($remoteTag.Count -gt 0 -and ($remoteTag[-1] -split '\s+')[0] -cne $Commit) { throw 'Release tag points to a different source commit. Use a new version.' }
if (-not $identity.IsPreview -and $remoteTag.Count -eq 0) { throw 'Stable releases require an existing version tag.' }
$releasesJson = & gh api --paginate --slurp "repos/$Repository/releases"
if ($LASTEXITCODE -ne 0) { throw 'Could not list GitHub releases.' }
$releases = @($releasesJson | ConvertFrom-Json) | ForEach-Object { $_ }
$matching = @($releases | Where-Object { ($_.tag_name -creplace '^v', '') -ceq $Version })
if ($matching | Where-Object { -not $_.draft }) { throw 'This version already has a published release. Published assets are immutable; rerun with a new CI attempt or version.' }
if ($matching | Where-Object { $_.tag_name -cne $Tag }) { throw 'This version already has a draft under a different tag spelling.' }
$existing = $matching | Where-Object tag_name -CEQ $Tag
if ($existing -and $existing.target_commitish -cne $Commit) { throw 'Existing draft targets a different source commit.' }
$metadata = [ordered]@{
    schema=1; publisher='phonecontrol-actions-v1'; version=$Version; channel=$identity.Channel
    branch=$SourceBranch; sourceCommit=$Commit
} | ConvertTo-Json -Compress
$notesPath = Join-Path $ArtifactDirectory 'release-notes.md'
$channelNote = if ($identity.IsPreview) { "Experimental $($identity.Channel) build. Installs as PhoneControl Preview with separate settings. Stable installations remain separate." } else { 'Stable release of PhoneControl.' }
@"
PhoneControl $Version for Intel/AMD x64 Windows 11 24H2 or newer.

$channelNote

Download ``PhoneControl-Setup-$Version-win-x64.exe`` to install. Required runtimes and USB tools are bundled. Saved profiles survive updates and uninstall.

The corresponding editable source ZIP, SHA-256 checksum and build manifest are attached. PhoneControl is licensed under AGPL-3.0-only; bundled components retain their own notices.

Source commit: $Commit
Source branch: $SourceBranch

<!-- PhoneControl release metadata: $metadata -->
"@ | Set-Content -LiteralPath $notesPath -Encoding utf8
if ($existing) {
    & gh release upload $Tag @files --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Release asset upload failed.' }
    & gh release edit $Tag --repo $Repository --notes-file $notesPath --title "PhoneControl $Version"
    if ($LASTEXITCODE -ne 0) { throw 'Draft metadata update failed.' }
} else {
    $arguments = @('release','create',$Tag) + $files + @('--repo',$Repository,'--target',$Commit,'--title',"PhoneControl $Version",'--notes-file',$notesPath,'--draft','--latest=false')
    if ($identity.IsPreview) { $arguments += '--prerelease' } else { $arguments += '--verify-tag' }
    & gh @arguments
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release creation failed.' }
}
# Create privately first so partial uploads never appear as a downloadable release.
$previewFlag = if ($identity.IsPreview) { '--prerelease=true' } else { '--prerelease=false' }
$latestFlag = if ($identity.IsPreview) { '--latest=false' } else { '--latest=true' }
& gh release edit $Tag --repo $Repository --draft=false $previewFlag $latestFlag
if ($LASTEXITCODE -ne 0) { throw 'GitHub release publication failed; a draft remains for retry.' }
Write-Output "Published installer: https://github.com/$Repository/releases/tag/$Tag"
