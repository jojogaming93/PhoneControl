# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
# Local protocol tests: git/gh are mocked; no tags, branches or releases are changed.
$ErrorActionPreference = 'Stop'
$resolver = Join-Path $PSScriptRoot 'Resolve-ReleaseVersion.ps1'
$publisher = Join-Path $PSScriptRoot 'New-GitHubRelease.ps1'
$script:checks = 0
function Assert([bool]$Condition, [string]$Description) {
    if (-not $Condition) { throw $Description }
    $script:checks++
}
function Reject([scriptblock]$Action, [string]$Message) {
    $caught = $null
    try { & $Action | Out-Null } catch { $caught = $_.Exception.Message }
    Assert ($null -ne $caught -and $caught.Contains($Message)) "Expected rejection: $Message; got: $caught"
}
foreach ($version in @('0.0.0','1.0.8','12.34.56')) {
    $release = & $resolver -EventName push -RefType branch -RefName "release/$version"
    Assert ($release.Version -ceq $version -and $release.Tag -ceq "v$version" -and $release.ReleaseBranch -ceq "release/$version") 'Release branch version resolution failed.'
}
foreach ($tag in @('1.0.8','v1.0.8')) {
    $release = & $resolver -EventName push -RefType tag -RefName $tag
    Assert ($release.Tag -ceq $tag -and $release.Version -ceq '1.0.8' -and -not $release.ReleaseBranch) 'Legacy tag resolution failed.'
}
$manual = & $resolver -EventName workflow_dispatch -RefType branch -RefName main -InputVersion '1.0.8'
Assert ($manual.Tag -ceq 'v1.0.8') 'Manual releases must use the canonical v tag.'
Reject { & $resolver -EventName workflow_dispatch -RefType branch -RefName release/1.0.8 -InputVersion '1.0.9' } 'must match'
Reject { & $resolver -EventName push -RefType branch -RefName feature/ios } 'Releases require'
foreach ($bad in @('01.0.8','1.0','1.0.8/extra','1.0.8-rc.1','v1.0.8','1.0.8;echo')) {
    Reject { & $resolver -EventName push -RefType branch -RefName "release/$bad" } 'major.minor.patch'
}

$testRoot = Join-Path (Split-Path -Parent $PSScriptRoot) ("artifacts/release-automation-test-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$version = '1.0.8'
$commit = 'a' * 40
$mock = [pscustomobject]@{ GitCode=0; GhCode=0; RemoteTag=@(); BranchCommit=$commit; Releases=@(); Calls=[System.Collections.Generic.List[string]]::new() }
function git {
    $global:LASTEXITCODE = $mock.GitCode
    if ($args -contains '--heads') { return "$($mock.BranchCommit)`trefs/heads/release/1.0.8" }
    $mock.RemoteTag
}
function gh {
    $global:LASTEXITCODE = $mock.GhCode
    $mock.Calls.Add(($args -join ' '))
    if ($args[0] -eq 'api') {
        # Match gh api --paginate --slurp: an array of page arrays, including empty pages.
        ConvertTo-Json -InputObject @(,@($mock.Releases)) -Depth 5 -Compress
    }
}
$installer = Join-Path $testRoot "PhoneControl-Setup-$version-win-x64.exe"
$source = Join-Path $testRoot "PhoneControl-$version-source.zip"
$manifestFile = Join-Path $testRoot "PhoneControl-$version-manifest.json"
[IO.File]::WriteAllText($installer, 'synthetic installer')
[IO.File]::WriteAllText($source, 'synthetic source')
$hash = (Get-FileHash -LiteralPath $installer).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installer.sha256", "$hash  $([IO.Path]::GetFileName($installer))`n")
$manifest = @{ version=$version; sourceCommit=$commit; sourceDirty=$false; installerSha256=$hash; sourceArchiveSha256=(Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant() }
function Write-Manifest { $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestFile }
function Publish-Test { & $publisher -Version $version -Tag "v$version" -Commit $commit -Repository 'example/PhoneControl' -ArtifactDirectory $testRoot -SourceBranch "release/$version" }
Write-Manifest
Publish-Test | Out-Null
Assert ([bool]($mock.Calls | Where-Object { $_ -like "release create v$version *" -and $_ -like "*--target $commit*" -and $_ -like '*--draft*' })) 'New releases must be drafts targeting the tested commit.'
$mock.Calls.Clear()
$mock.Releases = @(@{tag_name="v$version"; draft=$true; target_commitish=('b' * 40)})
Publish-Test | Out-Null
Assert ([bool]($mock.Calls | Where-Object { $_ -like 'release upload *--clobber*' })) 'Draft assets must refresh.'
Assert ([bool]($mock.Calls | Where-Object { $_ -like "release edit *--target $commit*" })) 'Draft target must follow the tested source.'
$mock.Releases = @(@{tag_name="v$version"; draft=$false})
Reject { Publish-Test } 'published release'
Assert (-not ($mock.Calls | Where-Object { $_ -like 'release create *' })) 'Published releases must never be recreated.'
$mock.Releases = @(@{tag_name=$version; draft=$false})
Reject { Publish-Test } 'published release'
$mock.Releases = @(@{tag_name=$version; draft=$true})
Reject { Publish-Test } 'different tag spelling'
$mock.Releases = @()
$mock.RemoteTag = @(('b' * 40) + "`trefs/tags/v$version")
Reject { Publish-Test } 'different source commit'
# Annotated tags are compared by their peeled commit, not the tag object's SHA.
$mock.RemoteTag = @((('b' * 40) + "`trefs/tags/v$version"), "$commit`trefs/tags/v$version^{}")
Publish-Test | Out-Null
$script:checks++
$mock.RemoteTag = @()
$mock.BranchCommit = 'b' * 40
$mock.Calls.Clear()
Publish-Test | Out-Null
Assert ($mock.Calls.Count -eq 0) 'A superseded branch build must not upload a release.'
$mock.BranchCommit = $commit
$manifest.sourceDirty = $true
Write-Manifest
Reject { Publish-Test } 'clean source tree'
$manifest.sourceDirty = $false
$manifest.sourceCommit = 'b' * 40
Write-Manifest
Reject { Publish-Test } 'version/source commit'
$manifest.sourceCommit = $commit
Write-Manifest
$mock.GitCode = 1
Reject { Publish-Test } 'verify the release branch'
$mock.GitCode = 0
$mock.GhCode = 1
Reject { Publish-Test } 'list GitHub releases'
$mock.GhCode = 0
[IO.File]::WriteAllText($source, 'corrupt source')
Reject { Publish-Test } 'checksum verification failed'
Remove-Item -LiteralPath "$installer.sha256"
Reject { Publish-Test } 'Release file missing'
Write-Output "PASS $script:checks release automation checks (no external mutations)."

