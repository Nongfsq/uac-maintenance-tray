[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',
    [ValidateSet('Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\UacMaintenanceTray\UacMaintenanceTray.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'
$publishRoot = Join-Path $artifacts 'publish'
$stageRoot = Join-Path $artifacts 'stage'
$releaseRoot = Join-Path $artifacts 'release'

foreach ($path in @($publishRoot, $stageRoot, $releaseRoot)) {
    $resolvedParent = [IO.Path]::GetFullPath((Split-Path -Parent $path))
    if (-not $resolvedParent.StartsWith([IO.Path]::GetFullPath($repoRoot), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the repository: $path"
    }
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

function Copy-CommonReleaseFiles {
    param(
        [Parameter(Mandatory)] [string]$Stage,
        [Parameter(Mandatory)] [string]$PublishedExecutable
    )

    New-Item -ItemType Directory -Path $Stage -Force | Out-Null
    Copy-Item -LiteralPath $PublishedExecutable -Destination $Stage

    $docsDir = Join-Path $Stage 'docs'
    New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $docsDir
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.zh-CN.md') -Destination $docsDir
    Copy-Item -LiteralPath (Join-Path $repoRoot 'SECURITY.md') -Destination $docsDir
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\SECURITY-MODEL.md') -Destination $docsDir

    $licensesDir = Join-Path $Stage 'licenses'
    New-Item -ItemType Directory -Path $licensesDir -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $licensesDir 'PROJECT-LICENSE.txt')

    $assetsDir = Join-Path $Stage 'assets'
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'assets\uac-maintenance-tray.svg') -Destination $assetsDir
}

function Copy-RuntimeNotices {
    param([Parameter(Mandatory)] [string]$Stage)

    $packageRootLine = dotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0 -or -not ($packageRootLine -match '^global-packages:\s*(.+)$')) {
        throw 'Could not locate the NuGet global packages directory.'
    }
    $packageRoot = $Matches[1].Trim()
    $runtimeConfigPath = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\UacMaintenanceTray\bin') -Recurse -File -Filter 'UacMaintenanceTray.runtimeconfig.json' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $runtimeConfigPath) { throw 'Could not locate the generated runtime configuration.' }
    $runtimeConfig = Get-Content -Raw -LiteralPath $runtimeConfigPath | ConvertFrom-Json
    $runtimeVersion = @($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App')[0].version
    if (-not $runtimeVersion) { throw 'Could not read the bundled runtime version.' }

    $runtimePackages = @(
        "Microsoft.NETCore.App.Runtime.win-x64/$runtimeVersion",
        "Microsoft.WindowsDesktop.App.Runtime.win-x64/$runtimeVersion"
    )
    $licensesDir = Join-Path $Stage 'licenses'
    foreach ($package in $runtimePackages) {
        $parts = $package -split '/', 2
        $packagePath = Join-Path $packageRoot (Join-Path $parts[0].ToLowerInvariant() $parts[1])
        $noticeFiles = Get-ChildItem -LiteralPath $packagePath -Recurse -File | Where-Object {
            $_.Name -match '(?i)^(license|third-party-notices)(\.txt)?$'
        }
        if (-not $noticeFiles) { throw "No runtime license found in $packagePath" }
        foreach ($notice in $noticeFiles) {
            $safePackage = $parts[0] -replace '[^A-Za-z0-9.-]', '-'
            Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $licensesDir "$safePackage-$($notice.Name)")
        }
    }
}

function Complete-ReleaseVariant {
    param(
        [Parameter(Mandatory)] [string]$Stage,
        [Parameter(Mandatory)] [string]$FolderName,
        [Parameter(Mandatory)] [string]$Variant
    )

    $checksumsDir = Join-Path $Stage 'checksums'
    New-Item -ItemType Directory -Path $checksumsDir -Force | Out-Null
    $manifestPath = Join-Path $checksumsDir 'FILE-MANIFEST.sha256'
    $manifestLines = Get-ChildItem -LiteralPath $Stage -Recurse -File |
        Where-Object FullName -ne $manifestPath |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($Stage, $_.FullName).Replace('\', '/')
            "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant(), $relative
        }
    [IO.File]::WriteAllLines($manifestPath, $manifestLines, [Text.UTF8Encoding]::new($false))

    $diagnosticsPath = Join-Path $artifacts "$Variant-diagnostics.json"
    $diagnosticsProcess = Start-Process -FilePath (Join-Path $Stage 'UacMaintenanceTray.exe') `
        -ArgumentList @('--diagnostics', '--file', $diagnosticsPath) -WindowStyle Hidden -Wait -PassThru
    if ($diagnosticsProcess.ExitCode -notin @(0, 3) -or -not (Test-Path -LiteralPath $diagnosticsPath)) {
        throw "$Variant diagnostics smoke test failed with exit code $($diagnosticsProcess.ExitCode)."
    }

    $zipPath = Join-Path $releaseRoot "$FolderName.zip"
    Compress-Archive -LiteralPath $Stage -DestinationPath $zipPath -CompressionLevel Optimal
    & (Join-Path $PSScriptRoot 'Test-PublicRepo.ps1') -SkipBuild -ReleaseZip $zipPath
    if ($LASTEXITCODE -ne 0) { throw "$Variant release validation failed." }
    return $zipPath
}

$variants = @(
    [pscustomobject]@{ Name = 'self-contained'; SelfContained = $true; RuntimeNotices = $true },
    [pscustomobject]@{ Name = 'framework-dependent'; SelfContained = $false; RuntimeNotices = $false }
)
$releaseArchives = [Collections.Generic.List[string]]::new()

foreach ($variant in $variants) {
    $folderName = "UacMaintenanceTray-v$Version-win-x64-$($variant.Name)"
    $publish = Join-Path $publishRoot $variant.Name
    $stage = Join-Path $stageRoot $folderName
    New-Item -ItemType Directory -Path $publish -Force | Out-Null

    $publishArguments = @(
        'publish', $project,
        '-c', $Configuration,
        '-r', 'win-x64',
        '--self-contained', $variant.SelfContained.ToString().ToLowerInvariant(),
        '-o', $publish,
        "-p:Version=$Version",
        '-p:PublishSingleFile=true',
        '-p:DebugType=None',
        '-p:DebugSymbols=false'
    )
    if ($variant.SelfContained) {
        $publishArguments += '-p:EnableCompressionInSingleFile=true'
        $publishArguments += '-p:IncludeNativeLibrariesForSelfExtract=true'
    }
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { throw "$($variant.Name) publish failed." }

    $publishedExecutable = Join-Path $publish 'UacMaintenanceTray.exe'
    if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
        throw "$($variant.Name) publish did not produce UacMaintenanceTray.exe."
    }
    Copy-CommonReleaseFiles -Stage $stage -PublishedExecutable $publishedExecutable
    if ($variant.RuntimeNotices) { Copy-RuntimeNotices -Stage $stage }
    $releaseArchives.Add((Complete-ReleaseVariant -Stage $stage -FolderName $folderName -Variant $variant.Name))
}

$sumLines = foreach ($archive in $releaseArchives) {
    "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($archive)
}
$sumPath = Join-Path $releaseRoot "UacMaintenanceTray-v$Version-SHA256SUMS.txt"
[IO.File]::WriteAllLines($sumPath, $sumLines, [Text.UTF8Encoding]::new($false))

Write-Host 'Release assets:'
$releaseArchives | ForEach-Object { Write-Host "  $_" }
Write-Host "  $sumPath"
