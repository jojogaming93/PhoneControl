# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

$ErrorActionPreference = 'Stop'
$appPath = Join-Path $PSScriptRoot 'artifacts\PhoneControl\PhoneControl.exe'
if (-not (Test-Path -LiteralPath $appPath)) {
    & (Join-Path $PSScriptRoot 'scripts\Publish.ps1')
}
Start-Process -FilePath $appPath -WorkingDirectory (Split-Path -Parent $appPath) -WindowStyle Hidden
