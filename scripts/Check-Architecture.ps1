# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& dotnet run --project (Join-Path $PSScriptRoot 'ArchitectureCheck/ArchitectureCheck.csproj') -c Release -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Source architecture validation failed.' }
