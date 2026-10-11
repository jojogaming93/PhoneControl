# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

param([Parameter(Mandatory)][string]$InputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$inputPath = [IO.Path]::GetFullPath($InputDirectory)
$assembly = Join-Path $inputPath 'PhoneControl.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw "Missing PhoneControl assembly: $assembly" }
$toolDirectory = Join-Path $projectRoot 'tools\obfuscar'
$toolVersion = '2.2.49'
$tool = Join-Path $toolDirectory 'obfuscar.console.exe'
if (-not (Test-Path -LiteralPath $tool)) {
    & dotnet tool install Obfuscar.GlobalTool --version $toolVersion --allow-roll-forward --tool-path $toolDirectory --add-source https://api.nuget.org/v3/index.json
    if ($LASTEXITCODE -ne 0) { throw 'Could not install the pinned Obfuscar build tool.' }
}
$installed = & dotnet tool list --tool-path $toolDirectory
if ($LASTEXITCODE -ne 0 -or ($installed -join "`n") -notmatch '(?m)^obfuscar\.globaltool\s+2\.2\.49\s') { throw 'Unexpected Obfuscar version; expected 2.2.49.' }
# Keep the original, mapping and generated config outside the shipped payload.
$work = Join-Path $projectRoot ('artifacts\obfuscation\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
Copy-Item -LiteralPath $assembly -Destination (Join-Path $work 'PhoneControl.original.dll')
[xml]$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'obfuscar.xml') -Raw
($config.Obfuscator.Var | Where-Object name -eq 'InPath').value = $inputPath
($config.Obfuscator.Var | Where-Object name -eq 'OutPath').value = $work
$config.Obfuscator.Module.file = $assembly
$configPath = Join-Path $work 'obfuscar.xml'
$config.Save($configPath)
& $tool $configPath
if ($LASTEXITCODE -ne 0) { throw 'Obfuscation failed; release aborted.' }
$protected = Join-Path $work 'PhoneControl.dll'
if (-not (Test-Path -LiteralPath $protected)) { throw 'Obfuscator produced no assembly.' }
if ((Get-FileHash -LiteralPath $assembly).Hash -eq (Get-FileHash -LiteralPath $protected).Hash) { throw 'Obfuscator did not change the assembly.' }
Copy-Item -LiteralPath $protected -Destination $assembly -Force
# Symbols describe unobfuscated source and do not belong in a protected release.
$symbols = Join-Path $inputPath 'PhoneControl.pdb'
if (Test-Path -LiteralPath $symbols) { Remove-Item -LiteralPath $symbols -Force }
Write-Output "Obfuscated PhoneControl.dll; private diagnostics: $work"
