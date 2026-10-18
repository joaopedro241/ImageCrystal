# ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
# ImageCrystal release tooling, copyright (C) 2026 xenon.
# SPDX-License-Identifier: GPL-3.0-or-later
param([switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$repoPath = $PSScriptRoot
Push-Location $repoPath
try {
    $version = (Get-Content -LiteralPath (Join-Path $repoPath 'VERSION') -Raw).Trim()
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain major.minor.patch.' }
    $dirty = @(git status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) { throw 'Could not read Git status.' }
    if ($dirty.Count -gt 0) { throw 'Commit the reviewed source before making a release package.' }
    $sourceCommit = (git rev-parse HEAD).Trim()
    $releaseDir = Join-Path $repoPath 'artifacts\releases'
    $name = "ImageCrystal-$version-win-x64"
    $portableDir = Join-Path $releaseDir $name
    $binaryZip = Join-Path $releaseDir "$name.zip"
    $sourceZip = Join-Path $releaseDir "ImageCrystal-$version-source.zip"
    foreach ($destination in @($portableDir, $binaryZip, $sourceZip)) {
        if (Test-Path -LiteralPath $destination) { throw "Release output already exists: $destination" }
    }
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
    & (Join-Path $repoPath 'Build-Test.ps1') -SkipRestore:$SkipRestore -OutputDirectory $portableDir

    $executable = Join-Path $portableDir 'ImageCrystal.exe'
    $binaryVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
    if (([version]$binaryVersion).ToString(3) -ne $version) { throw "Binary $binaryVersion does not match VERSION $version." }
    $privateFiles = @(Get-ChildItem -LiteralPath $portableDir -Recurse -File | Where-Object {
        $_.Name -in @('igconfig.json','igconfig.admin.json','SmokeTests.dll','Probe.dll','FormatProbe.dll','LoadingBenchmark.dll') -or $_.Extension -eq '.log'
    })
    if ($privateFiles.Count) { throw 'Release output contains personal configuration, logs or test harnesses.' }
    $licenseNames = @('GPL-3.0.txt','ImageGlass-upstream.txt','Magick.NET-NOTICE.txt','DotNet-LICENSE.txt','DotNet-ThirdPartyNotices.txt')
    foreach ($licenseName in $licenseNames) {
        if (!(Test-Path -LiteralPath (Join-Path $portableDir "License\$licenseName"))) { throw "Missing notice: $licenseName" }
    }
    @{
        product = 'ImageCrystal'; version = $version; platform = 'win-x64'
        sourceCommit = $sourceCommit; plannedReleaseTag = "v$version"
        upstreamTag = '9.6.1.807'; upstreamCommit = '1650ca9f3b8209d4a69a353f804ff6f7c42fc9de'
        sourceArchive = [IO.Path]::GetFileName($sourceZip)
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $portableDir 'RELEASE-MANIFEST.json') -Encoding utf8
    @"
ImageCrystal $version (Windows x64 portable)

Extract the complete archive to a writable folder and run ImageCrystal.exe.
No installation or separate .NET runtime is required. Graphical Settings,
About and the WebView2 SVG viewer require Microsoft WebView2.

Source commit: $sourceCommit
Upstream base: ImageGlass 9.6.1.807
Source and updates: https://github.com/joaopedro241/ImageCrystal

See LICENSE.txt, NOTICE.ImageCrystal.txt and THIRD-PARTY-NOTICES.md.
"@ | Set-Content -LiteralPath (Join-Path $portableDir 'PORTABLE-README.txt') -Encoding utf8
    git archive --format=zip "--prefix=ImageCrystal-$version-source/" "--output=$sourceZip" HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Source archive failed.' }
    Compress-Archive -LiteralPath $portableDir -DestinationPath $binaryZip -CompressionLevel Optimal
    $checksums = foreach ($archive in @($binaryZip, $sourceZip)) {
        $hash = Get-FileHash -LiteralPath $archive -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), [IO.Path]::GetFileName($archive)
    }
    $checksums | Set-Content -LiteralPath (Join-Path $releaseDir 'SHA256SUMS.txt') -Encoding ascii
    if (@(git status --porcelain --untracked-files=normal).Count) { throw 'The build changed source files; review and rebuild from a clean commit.' }
    Write-Host "Release prepared from $sourceCommit (not published): $releaseDir"
} finally { Pop-Location }
