# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
# git/gh are mocked: this suite never creates or deletes remote resources.
$ErrorActionPreference = 'Stop'
$resolver = Join-Path $PSScriptRoot 'Resolve-ReleaseVersion.ps1'
$identityScript = Join-Path $PSScriptRoot 'Get-ReleaseIdentity.ps1'
$publisher = Join-Path $PSScriptRoot 'New-GitHubRelease.ps1'
$retention = Join-Path $PSScriptRoot 'Remove-OldPreviewReleases.ps1'
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
function Resolve-Preview([string]$Branch, [string]$Run='1842', [string]$Attempt='1') {
    & $resolver -EventName push -RefType branch -RefName $Branch -RunId $Run -RunAttempt $Attempt
}
$alpha = Resolve-Preview feature/ios
Assert ($alpha.Channel -ceq 'alpha' -and $alpha.Version -cmatch '^1\.1\.0-alpha\.ios-[a-f0-9]{12}\.1842\.1$' -and $alpha.Retention -eq 10) 'Feature pushes must create unique alpha versions.'
$beta = Resolve-Preview main
Assert ($beta.Version -ceq '1.1.0-beta.1842.1' -and $beta.Retention -eq 20) 'Main pushes must create beta versions.'
$rc = Resolve-Preview release/1.2.3
Assert ($rc.Version -ceq '1.2.3-rc.1842.1' -and $rc.Retention -eq 0) 'Release branches must create retained RC versions.'
Assert ((Resolve-Preview codex/work).Channel -ceq 'alpha') 'Codex feature branches must produce alpha versions.'
Assert ((Resolve-Preview feature/ios -Attempt 2).Version -cne $alpha.Version) 'Retries must have distinct versions.'
Assert ((Resolve-Preview feature/ios -Run 38284081168).Version.EndsWith('.38284081168.1')) 'Large CI run IDs must not leak into numeric Windows versions.'
Assert ((Resolve-Preview feature/a/b).Version -cne (Resolve-Preview feature/a-b).Version) 'Similar branch slugs must remain distinct.'
Assert ((Resolve-Preview feature/IOS).Version -cne $alpha.Version) 'Case-sensitive branch names must remain distinct.'
Assert ((Resolve-Preview ('feature/' + ('x' * 100))).Version.Length -lt 120) 'Branch identifiers must be bounded.'
Assert ((Resolve-Preview feature/123).Channel -ceq 'alpha') 'Numeric feature branch names must be valid.'
foreach ($tag in @('1.1.0','v1.1.0')) {
    $stable = & $resolver -EventName push -RefType tag -RefName $tag
    Assert ($stable.Channel -ceq 'stable' -and $stable.Version -ceq '1.1.0' -and $stable.Tag -ceq $tag -and -not $stable.SourceBranch) 'Stable tag resolution failed.'
}
$manual = & $resolver -EventName workflow_dispatch -RefType branch -RefName main -InputVersion 1.2.0 -RunId 1842 -RunAttempt 1
Assert ($manual.Version -ceq '1.2.0-beta.1842.1') 'Manual branch runs must remain previews.'
Reject { & $resolver -EventName workflow_dispatch -RefType tag -RefName v1.1.0 } 'explicitly pushed'
Reject { & $resolver -EventName push -RefType tag -RefName v1.1.0-rc.1.1 } 'Only stable'
Reject { & $resolver -EventName workflow_dispatch -RefType branch -RefName release/1.1.0 -InputVersion 1.2.0 -RunId 1 -RunAttempt 1 } 'must match'
Reject { Resolve-Preview other } 'Preview releases require'
Reject { Resolve-Preview feature/ios -Run 0 } 'numeric CI run'
foreach ($bad in @('01.1.0','1.1','1.1.0/extra','1.1.0;echo','1.1.0-alpha','1.1.0-beta.01.1','1.1.0-rc.1.0','1.1.0-beta.1.1+secret')) {
    Reject { & $identityScript -Version $bad } 'Version must'
}
Reject { & $identityScript -Version 65536.0.0 } 'numeric limit'
Reject { & $identityScript -Version 65535.0.0 } 'numeric limit'
Assert ((& $identityScript -Version 1.1.65535).NumericVersion -ceq '1.1.65535.0') 'The Windows patch slot may use 65535 without changing AssemblyVersion.'
$stableIdentity = & $identityScript -Version 1.1.0
$alphaIdentity = & $identityScript -Version $alpha.Version
Assert ($stableIdentity.NumericVersion -ceq '1.1.0.0' -and $alphaIdentity.NumericVersion -ceq $stableIdentity.NumericVersion) 'Windows resource versions must be numeric and independent of CI IDs.'
Assert ($stableIdentity.AppId -cne $alphaIdentity.AppId -and $alphaIdentity.ProductName -ceq 'PhoneControl Preview') 'Preview installs must have a separate product identity.'
foreach ($version in @($beta.Version,$rc.Version)) {
    $identity = & $identityScript -Version $version
    Assert ($identity.AppId -ceq $alphaIdentity.AppId -and $identity.ProductName -ceq $alphaIdentity.ProductName) 'All preview channels must upgrade the same Preview installation.'
}

$testRoot = Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts/release-automation-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$commit = 'a' * 40
$mock = [pscustomobject]@{ GitCode=0; GhCode=0; FailCommand=''; RemoteTag=@(); Releases=@(); Calls=[System.Collections.Generic.List[string]]::new() }
function git { $global:LASTEXITCODE = $mock.GitCode; $mock.RemoteTag }
function gh {
    $command = $args -join ' '
    $mock.Calls.Add($command)
    $global:LASTEXITCODE = if ($mock.FailCommand -and $command.StartsWith($mock.FailCommand)) { 1 } else { $mock.GhCode }
    if ($args[0] -eq 'api') {
        # Exercise pagination: the matching release may be on a later page.
        ConvertTo-Json -InputObject @(@(@{tag_name='v0.0.1';draft=$false}), @($mock.Releases)) -Depth 10 -Compress
    }
}
function New-Fixture([string]$Version, [string]$Branch='') {
    $identity = & $identityScript -Version $Version
    $directory = Join-Path $testRoot $Version
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $installer = Join-Path $directory "PhoneControl-Setup-$Version-win-x64.exe"
    $source = Join-Path $directory "PhoneControl-$Version-source.zip"
    [IO.File]::WriteAllText($installer, 'synthetic installer')
    [IO.File]::WriteAllText($source, 'synthetic source')
    $hash = (Get-FileHash -LiteralPath $installer).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$installer.sha256", "$hash  $([IO.Path]::GetFileName($installer))`n")
    $manifest = @{version=$Version;channel=$identity.Channel;sourceBranch=$Branch;numericVersion=$identity.NumericVersion;appId=$identity.AppId;sourceCommit=$commit;sourceDirty=$false;installerSha256=$hash;sourceArchiveSha256=(Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant()}
    $fixture = [pscustomobject]@{Version=$Version;Tag="v$Version";Branch=$Branch;Directory=$directory;Installer=$installer;Source=$source;ManifestPath=(Join-Path $directory "PhoneControl-$Version-manifest.json");Manifest=$manifest}
    Write-Manifest $fixture
    $fixture
}
function Write-Manifest($Fixture) { $Fixture.Manifest | ConvertTo-Json | Set-Content -LiteralPath $Fixture.ManifestPath }
function Publish-Fixture($Fixture) { & $publisher -Version $Fixture.Version -Tag $Fixture.Tag -Commit $commit -Repository example/PhoneControl -ArtifactDirectory $Fixture.Directory -SourceBranch $Fixture.Branch }
$fixture = New-Fixture $alpha.Version feature/ios
foreach ($candidate in @(
    (New-Fixture $beta.Version main),
    (New-Fixture $rc.Version release/1.2.3)
)) {
    $mock.Calls.Clear()
    Publish-Fixture $candidate | Out-Null
    Assert ([bool]($mock.Calls | Where-Object { $_ -like 'release edit *--draft=false*--prerelease=true*--latest=false*' })) 'Beta and RC builds must publish previews without becoming Latest.'
}
$mock.Calls.Clear()
Publish-Fixture $fixture | Out-Null
Assert ([bool]($mock.Calls | Where-Object { $_ -like 'release create *--draft*' -and $_ -like "*--target $commit*" })) 'Uploads must start as a private draft of the tested commit.'
Assert ([bool]($mock.Calls | Where-Object { $_ -like 'release edit *--draft=false*--prerelease=true*--latest=false*' })) 'Alphas must publish as prereleases without becoming Latest.'
$notes = Get-Content (Join-Path $fixture.Directory 'release-notes.md') -Raw
Assert ($notes.Contains('"branch":"feature/ios"') -and $notes.Contains('"publisher":"phonecontrol-actions-v1"')) 'Release notes must identify channel and source for bounded retention.'
$mock.Calls.Clear()
$mock.Releases = @(@{tag_name=$fixture.Tag;draft=$true;target_commitish=$commit})
Publish-Fixture $fixture | Out-Null
Assert ([bool]($mock.Calls | Where-Object { $_ -like 'release upload *--clobber*' })) 'Failed draft uploads must support retry.'
$mock.Releases = @(@{tag_name=$fixture.Tag;draft=$false})
$mock.Calls.Clear()
Reject { Publish-Fixture $fixture } 'published release'
Assert (-not ($mock.Calls | Where-Object { $_ -like 'release upload *' -or $_ -like 'release edit *' })) 'Published assets must never be overwritten.'
$mock.Releases = @(@{tag_name=$fixture.Tag;draft=$true;target_commitish=('b' * 40)})
Reject { Publish-Fixture $fixture } 'different source commit'
$mock.Releases = @()
$mock.RemoteTag = @(('b' * 40) + "`trefs/tags/$($fixture.Tag)")
Reject { Publish-Fixture $fixture } 'different source commit'
$mock.RemoteTag = @((('b' * 40) + "`trefs/tags/$($fixture.Tag)"), "$commit`trefs/tags/$($fixture.Tag)^{}")
Publish-Fixture $fixture | Out-Null
$script:checks++
$mock.RemoteTag = @()
$fixture.Manifest.sourceDirty = $true
Write-Manifest $fixture
Reject { Publish-Fixture $fixture } 'clean source tree'
$fixture.Manifest.sourceDirty = $false
$fixture.Manifest.appId = $stableIdentity.AppId
Write-Manifest $fixture
Reject { Publish-Fixture $fixture } 'channel'
$fixture.Manifest.appId = $alphaIdentity.AppId
Write-Manifest $fixture
$mock.GitCode = 1
Reject { Publish-Fixture $fixture } 'remote release tag'
$mock.GitCode = 0
$mock.GhCode = 1
Reject { Publish-Fixture $fixture } 'list GitHub releases'
$mock.GhCode = 0
$mock.FailCommand = 'release create'
Reject { Publish-Fixture $fixture } 'release creation failed'
$mock.FailCommand = 'release edit'
Reject { Publish-Fixture $fixture } 'publication failed'
$mock.FailCommand = ''
[IO.File]::WriteAllText($fixture.Source, 'corrupt source')
Reject { Publish-Fixture $fixture } 'checksum verification failed'
Remove-Item -LiteralPath "$($fixture.Installer).sha256"
Reject { Publish-Fixture $fixture } 'Release file missing'
$stableFixture = New-Fixture 1.1.0
Reject { Publish-Fixture $stableFixture } 'existing version tag'
$mock.RemoteTag = @("$commit`trefs/tags/v1.1.0")
$mock.Calls.Clear()
Publish-Fixture $stableFixture | Out-Null
Assert ([bool]($mock.Calls | Where-Object { $_ -like 'release create *--verify-tag*' })) 'Stable publication must verify the explicit tag.'
Assert ([bool]($mock.Calls | Where-Object { $_ -like 'release edit *--draft=false*--prerelease=false*--latest=true*' })) 'Stable tags must publish normal Latest releases.'

function Preview-Record([string]$Branch, [int]$Index) {
    $release = Resolve-Preview $Branch -Run "$Index"
    $record = @{schema=1;publisher='phonecontrol-actions-v1';version=$release.Version;channel=$release.Channel;branch=$Branch;sourceCommit=$commit} | ConvertTo-Json -Compress
    [pscustomobject]@{id=$Index;tag_name=$release.Tag;draft=$false;prerelease=$true;author=@{login='github-actions[bot]'};published_at=([DateTime]::UnixEpoch.AddMinutes($Index).ToString('o'));body="<!-- PhoneControl release metadata: $record -->"}
}
$own = @(1..14 | ForEach-Object { Preview-Record feature/ios $_ })
$other = Preview-Record feature/other 50
$human = Preview-Record feature/ios 51; $human.author.login = 'example'
$draft = Preview-Record feature/ios 52; $draft.draft = $true
$malformed = Preview-Record feature/ios 53; $malformed.body = 'unrelated notes'
$rcRecord = Preview-Record release/1.1.0 54
$stableRecord = [pscustomobject]@{tag_name='v1.1.0';draft=$false;prerelease=$false;author=@{login='github-actions[bot]'}}
$mock.Releases = @($own) + @($other,$human,$draft,$malformed,$rcRecord,$stableRecord)
$mock.Calls.Clear()
& $retention -Repository example/PhoneControl -Channel alpha -SourceBranch feature/ios -KeepTag $own[-1].tag_name
$deleted = @($mock.Calls | Where-Object { $_ -like 'release delete *' })
Assert ($deleted.Count -eq 4) 'Alpha retention must keep ten releases per branch.'
foreach ($release in $own[0..3]) {
    Assert ([bool]($deleted | Where-Object { $_ -like "release delete $($release.tag_name) *--cleanup-tag*" })) 'Only the oldest owned alpha releases and their tags should be deleted.'
}
$mock.Releases = @(1..21 | ForEach-Object { Preview-Record main $_ })
$mock.Calls.Clear()
& $retention -Repository example/PhoneControl -Channel beta -SourceBranch main -KeepTag $mock.Releases[-1].tag_name
Assert (@($mock.Calls | Where-Object { $_ -like 'release delete *' }).Count -eq 1) 'Beta retention must keep twenty releases.'
$mock.Calls.Clear()
& $retention -Repository example/PhoneControl -Channel rc -SourceBranch release/1.1.0 -KeepTag v1.1.0-rc.1.1
& $retention -Repository example/PhoneControl -Channel stable -KeepTag v1.1.0
Assert ($mock.Calls.Count -eq 0) 'All RC and stable releases must be retained.'
Reject { & $retention -Repository example/PhoneControl -Channel beta -SourceBranch feature/ios -KeepTag unused } 'match its branch'
Reject { & $retention -Repository example/PhoneControl -Channel beta -SourceBranch main -KeepTag unused } 'no cleanup performed'
Assert (-not ($mock.Calls | Where-Object { $_ -like 'release delete *' })) 'Missing current preview must never authorize cleanup.'
Write-Output "PASS $script:checks release automation checks (no external mutations)."
