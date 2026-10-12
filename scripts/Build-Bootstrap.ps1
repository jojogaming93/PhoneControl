# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

param([Parameter(Mandatory)][string]$Payload, [Parameter(Mandatory)][string]$Output, [Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
$identity = & (Join-Path $PSScriptRoot 'Get-ReleaseIdentity.ps1') -Version $Version
$root = Split-Path -Parent $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) { throw 'Build machine needs the Visual Studio C++ x64 build tools (users do not).' }
$toolset = Get-ChildItem -LiteralPath (Join-Path $visualStudio 'VC\Tools\MSVC') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$kitRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots').KitsRoot10
$sdk = Get-ChildItem -LiteralPath (Join-Path $kitRoot 'Include') -Directory | Where-Object { Test-Path (Join-Path $kitRoot ('bin\' + $_.Name + '\x64\rc.exe')) } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $sdk) { throw 'Build machine needs the Windows SDK.' }
$work = Join-Path $root ('artifacts\bootstrap-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$escapedPayload = [IO.Path]::GetFullPath($Payload).Replace('\','\\').Replace('"','\"')
$escapedManifest = (Join-Path $root 'installer\bootstrap.manifest').Replace('\','\\')
$escapedIcon = (Join-Path $root 'assets\branding\phonecontrol.ico').Replace('\','\\')
$numbers = $identity.NumericVersion.Replace('.',',')
$resource = @"
#include <windows.h>
101 RCDATA "$escapedPayload"
1 RT_MANIFEST "$escapedManifest"
1 ICON "$escapedIcon"
1 VERSIONINFO
FILEVERSION $numbers
PRODUCTVERSION $numbers
FILEOS VOS_NT_WINDOWS32
FILETYPE VFT_APP
BEGIN
  BLOCK "StringFileInfo"
  BEGIN
    BLOCK "040904b0"
    BEGIN
      VALUE "CompanyName", "jojogaming93\0"
      VALUE "LegalCopyright", "Copyright (c) 2026 jojogaming93\0"
      VALUE "FileDescription", "$($identity.ProductName) Windows installer\0"
      VALUE "FileVersion", "$Version\0"
      VALUE "ProductName", "$($identity.ProductName)\0"
      VALUE "ProductVersion", "$Version\0"
    END
  END
  BLOCK "VarFileInfo"
  BEGIN
    VALUE "Translation", 0x0409, 1200
  END
END
"@
$resourcePath = Join-Path $work 'bootstrap.rc'
Set-Content -LiteralPath $resourcePath -Value $resource -Encoding Unicode
$oldInclude = $env:INCLUDE; $oldLib = $env:LIB; $oldPath = $env:PATH
try {
    $env:INCLUDE = (Join-Path $toolset.FullName 'include') + ';' + (($sdk.Name | ForEach-Object { Join-Path $kitRoot "Include\$_\ucrt"; Join-Path $kitRoot "Include\$_\shared"; Join-Path $kitRoot "Include\$_\um" }) -join ';')
    $env:LIB = (Join-Path $toolset.FullName 'lib\x64') + ';' + (Join-Path $kitRoot "Lib\$($sdk.Name)\ucrt\x64") + ';' + (Join-Path $kitRoot "Lib\$($sdk.Name)\um\x64")
    $compilerDirectory = Join-Path $toolset.FullName 'bin\Hostx64\x64'
    $env:PATH = $compilerDirectory + ';' + (Join-Path $kitRoot "bin\$($sdk.Name)\x64") + ';' + $oldPath
    $res = Join-Path $work 'bootstrap.res'
    & (Join-Path $kitRoot "bin\$($sdk.Name)\x64\rc.exe") /nologo "/fo$res" $resourcePath
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap resource compilation failed.' }
    & (Join-Path $compilerDirectory 'cl.exe') /nologo /O2 /MT /EHsc /guard:cf /std:c++17 (Join-Path $root 'installer\bootstrap.cpp') $res "/Fo$(Join-Path $work 'bootstrap.obj')" "/Fe$Output" /link /SUBSYSTEM:WINDOWS /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA /guard:cf /MANIFEST:NO kernel32.lib user32.lib shell32.lib ole32.lib
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap compilation failed.' }
} finally { $env:INCLUDE = $oldInclude; $env:LIB = $oldLib; $env:PATH = $oldPath }
