# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only

param(
    [string]$AndroidSdk = "$env:LOCALAPPDATA\Android\Sdk",
    [string]$Jdk = $env:JAVA_HOME,
    [string]$Platform = 'android-37.0',
    [string]$BuildTools = '36.0.0'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$buildRoot = Join-Path $taskRoot 'artifacts\control-server-build'
New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
if (-not $Jdk) {
    # Oracle's javapath shims do not identify the JDK directory. Ask the JVM.
    $javaProperties = & (Get-Command java.exe).Source -XshowSettings:properties -version 2>&1
    $homeProperty = $javaProperties | Where-Object { "$_" -match '^\s*java\.home\s*=' } | Select-Object -First 1
    if (-not $homeProperty) { throw 'Cannot determine the JDK directory. Supply -Jdk.' }
    $Jdk = ("$homeProperty" -split '=', 2)[1].Trim()
}
$javac = Join-Path $Jdk 'bin\javac.exe'
$java = Join-Path $Jdk 'bin\java.exe'
$jar = Join-Path $Jdk 'bin\jar.exe'
$platformRoot = Join-Path $AndroidSdk "platforms\$Platform"
$toolRoot = Join-Path $AndroidSdk "build-tools\$BuildTools"
foreach ($required in @($javac, $java, $jar, "$platformRoot\android.jar", "$toolRoot\aidl.exe", "$toolRoot\d8.bat")) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing server build dependency: $required. Supply -Jdk, -AndroidSdk, -Platform and -BuildTools as needed." }
}
$archive = Join-Path $buildRoot 'scrcpy-4.1.zip'
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri 'https://github.com/Genymobile/scrcpy/archive/refs/tags/v4.1.zip' -OutFile $archive
}
$expected = '1525C92183C4D1F7D4D1A702CFDDC5A452DA4F49222B0C327A7E8B1E0AF14833'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'Upstream scrcpy 4.1 source archive hash mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $buildRoot -Force
$serverRoot = Join-Path $buildRoot 'scrcpy-4.1\server'
Copy-Item -LiteralPath (Join-Path $taskRoot 'server\PointersState.java') -Destination "$serverRoot\src\main\java\com\genymobile\scrcpy\control\PointersState.java" -Force
$generated = Join-Path $buildRoot 'generated'
$classes = Join-Path $buildRoot 'classes'
$dex = Join-Path $buildRoot 'dex'
$testClasses = Join-Path $buildRoot 'test-classes'
New-Item -ItemType Directory -Force -Path "$generated\com\genymobile\scrcpy", $classes, $dex, $testClasses | Out-Null
Set-Content -LiteralPath "$generated\com\genymobile\scrcpy\BuildConfig.java" -Value 'package com.genymobile.scrcpy; public final class BuildConfig { public static final boolean DEBUG=false; public static final String VERSION_NAME="4.1"; }'
Get-ChildItem -LiteralPath "$serverRoot\src\main\aidl" -Recurse -Filter '*.aidl' | ForEach-Object {
    & "$toolRoot\aidl.exe" "-p$platformRoot\framework.aidl" "-I$serverRoot\src\main\aidl" "-o$generated" $_.FullName
    if ($LASTEXITCODE -ne 0) { throw 'AIDL compilation failed.' }
}
$sources = Get-ChildItem "$serverRoot\src\main\java", $generated -Recurse -Filter '*.java' | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' }
$sourceList = Join-Path $buildRoot 'sources.txt'
Set-Content -LiteralPath $sourceList -Value $sources
& $javac --release 17 -encoding UTF-8 -classpath "$platformRoot\android.jar" -d $classes "@$sourceList"
if ($LASTEXITCODE -ne 0) { throw 'Java compilation failed.' }
& $javac --release 17 -encoding UTF-8 -d $testClasses "$taskRoot\server\test\android\view\MotionEvent.java" "$taskRoot\server\test\PointerStateTest.java" "$taskRoot\server\PointersState.java" "$serverRoot\src\main\java\com\genymobile\scrcpy\control\Pointer.java" "$serverRoot\src\main\java\com\genymobile\scrcpy\model\Point.java"
if ($LASTEXITCODE -ne 0) { throw 'Pointer test compilation failed.' }
& $java -classpath $testClasses PointerStateTest
if ($LASTEXITCODE -ne 0) { throw 'Pointer regression test failed.' }
$classJar = Join-Path $buildRoot 'classes.jar'
& $jar cf $classJar -C $classes .
if ($LASTEXITCODE -ne 0) { throw 'Class archive creation failed.' }
$previousJavaHome = $env:JAVA_HOME
try {
    $env:JAVA_HOME = $Jdk
    & "$toolRoot\d8.bat" --release --min-api 21 --lib "$platformRoot\android.jar" --output $dex $classJar
    if ($LASTEXITCODE -ne 0) { throw 'Android dex compilation failed.' }
} finally { $env:JAVA_HOME = $previousJavaHome }
$output = Join-Path $taskRoot 'assets\phonecontrol-control.jar'
& $jar cf $output -C $dex classes.dex
if ($LASTEXITCODE -ne 0) { throw 'Control server archive creation failed.' }
Write-Output "Control server rebuilt: $output"
