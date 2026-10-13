# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
$number = '(0|[1-9][0-9]*)'
$pattern = "^$number\.$number\.$number(?:-(?:alpha\.[a-z][a-z0-9-]*|beta|rc)\.[1-9][0-9]*\.[1-9][0-9]*)?$"
if ($Version -cnotmatch $pattern) { throw 'Version must be major.minor.patch or an alpha.branch.build.attempt, beta.build.attempt or rc.build.attempt prerelease.' }
$baseVersion = ($Version -split '-', 2)[0]
$parts = $baseVersion.Split('.')
for ($index = 0; $index -lt $parts.Length; $index++) {
    # AssemblyVersion reserves 65535; FileVersion permits it in the patch slot.
    $maximum = if ($index -lt 2) { 65534 } else { 65535 }
    if ($parts[$index].Length -gt 5 -or [int]$parts[$index] -gt $maximum) { throw 'Windows/assembly version component exceeds its numeric limit.' }
}
$channel = if ($Version.Contains('-')) { ($Version -split '-', 2)[1].Split('.')[0] } else { 'stable' }
$preview = $channel -ne 'stable'
[pscustomobject]@{
    Version = $Version
    BaseVersion = $baseVersion
    Channel = $channel
    IsPreview = $preview
    NumericVersion = "$baseVersion.0"
    AssemblyVersion = "$($parts[0]).$($parts[1]).0.0"
    ProductName = $(if ($preview) { 'PhoneControl Preview' } else { 'PhoneControl' })
    AppId = $(if ($preview) { '{0F3B2360-8C37-4C30-8FA5-2A697FDC8836}' } else { '{CF1762E2-784A-49DA-AF70-A6753131BE40}' })
}
