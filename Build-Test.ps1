param([switch]$SkipRestore, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$repoPath = $PSScriptRoot
$outputPath = Join-Path $repoPath 'artifacts\ImageCrystal-test'
$webPath = Join-Path $repoPath 'Source\Components\ImageGlass.Settings\WebUI'
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuildPath = & $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuildPath) { throw 'Visual Studio MSBuild with .NET desktop development is required.' }
function CheckExit([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)." } }
Push-Location $repoPath
try {
    if (!$SkipRestore) {
        dotnet restore Source/ImageGlass.slnx -r win-x64 -p:Platform=x64 --packages .packages
        CheckExit 'NuGet restore'
    }
    Push-Location $webPath
    try {
        if (!$SkipRestore) {
            # Honour the Windows trust store, including locally trusted proxy certificates.
            $previousNodeOptions = $env:NODE_OPTIONS
            try {
                $env:NODE_OPTIONS = "$previousNodeOptions --use-system-ca".Trim()
                npm ci --cache (Join-Path $repoPath '.npm-cache') --no-audit --no-fund
                CheckExit 'npm restore'
            } finally { $env:NODE_OPTIONS = $previousNodeOptions }
        }
        npm run build
        CheckExit 'Web UI build'
    } finally { Pop-Location }
    & $msbuildPath Source/ImageGlass.slnx /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true "/p:PublishDir=$outputPath\" /m /v:minimal /nologo
    CheckExit 'Portable build'
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoPath 'Setup\Assets\Themes') -Destination $outputPath -Recurse -Force
    $webOutputPath = Join-Path $outputPath 'WebUI'
    New-Item -ItemType Directory -Path $webOutputPath -Force | Out-Null
    Copy-Item -Path (Join-Path $webPath 'dist\*') -Destination $webOutputPath -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'Setup\Assets\default.webp') -Destination $outputPath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'LICENSE') -Destination (Join-Path $outputPath 'LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'TESTING.md') -Destination $outputPath -Force
    Copy-Item -LiteralPath (Join-Path $repoPath 'NOTICE.ImageCrystal.txt') -Destination $outputPath -Force
    # Only default settings are rewritten. Existing portable user preferences are preserved.
    @{
        _Metadata = @{ Description = 'ImageCrystal portable test defaults'; Version = 9.4 }
        QuickSetupVersion = 9
        NeighbourPreloadCount = 0
        EnableRealTimeFileUpdate = $false
        ShowGallery = $false
        ColorProfile = 'CurrentMonitorProfile'
        ImageInterpolationScaleUp = 'HighQualityBicubic'
        ImageInterpolationScaleDown = 'HighQualityBicubic'
        EnableMultiInstances = $true
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'igconfig.default.json') -Encoding utf8
    if (!$SkipTests) {
        dotnet build tests/SmokeTests/SmokeTests.csproj -c Release -p:AppBuild="$outputPath"
        CheckExit 'Smoke test build'
        $testBuildPath = Join-Path $repoPath 'tests\SmokeTests\bin\Release\net10.0-windows10.0.17763.0'
        $testDllPath = Join-Path $outputPath 'SmokeTests.dll'
        Copy-Item -LiteralPath (Join-Path $testBuildPath 'SmokeTests.dll') -Destination $testDllPath -Force
        try {
            dotnet exec --depsfile (Join-Path $outputPath 'ImageCrystal.deps.json') --runtimeconfig (Join-Path $testBuildPath 'SmokeTests.runtimeconfig.json') $testDllPath (Join-Path $repoPath 'artifacts\smoke-fixtures')
            CheckExit 'Smoke tests'
        } finally { Remove-Item -LiteralPath $testDllPath -Force }
    }
    Write-Host "Ready: $outputPath\ImageCrystal.exe"
} finally { Pop-Location }
