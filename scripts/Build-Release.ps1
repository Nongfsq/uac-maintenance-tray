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
$publish = Join-Path $artifacts 'publish'
$stageRoot = Join-Path $artifacts 'stage'
$releaseRoot = Join-Path $artifacts 'release'
$folderName = "UacMaintenanceTray-v$Version-win-x64"
$stage = Join-Path $stageRoot $folderName

foreach ($path in @($publish, $stageRoot, $releaseRoot)) {
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

dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $publish `
    -p:Version=$Version -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item -LiteralPath $publish -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.zh-CN.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $stage
New-Item -ItemType Directory -Path (Join-Path $stage 'assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets\uac-maintenance-tray.svg') -Destination (Join-Path $stage 'assets')

$packageRootLine = dotnet nuget locals global-packages --list
if ($LASTEXITCODE -ne 0 -or -not ($packageRootLine -match '^global-packages:\s*(.+)$')) {
    throw 'Could not locate the NuGet global packages directory.'
}
$packageRoot = $Matches[1].Trim()
$runtimeConfig = Get-Content -Raw -LiteralPath (Join-Path $publish 'UacMaintenanceTray.runtimeconfig.json') | ConvertFrom-Json
$runtimeVersion = @($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App')[0].version
if (-not $runtimeVersion) { throw 'Could not read the bundled runtime version.' }
$runtimePackages = @(
    "Microsoft.NETCore.App.Runtime.win-x64/$runtimeVersion",
    "Microsoft.WindowsDesktop.App.Runtime.win-x64/$runtimeVersion"
)

$noticeDir = Join-Path $stage 'runtime-notices'
New-Item -ItemType Directory -Path $noticeDir -Force | Out-Null
foreach ($package in $runtimePackages) {
    $parts = $package -split '/', 2
    $packagePath = Join-Path $packageRoot (Join-Path $parts[0].ToLowerInvariant() $parts[1])
    $noticeFiles = Get-ChildItem -LiteralPath $packagePath -Recurse -File | Where-Object {
        $_.Name -match '(?i)^(license|third-party-notices)(\.txt)?$'
    }
    if (-not $noticeFiles) { throw "No runtime license found in $packagePath" }
    foreach ($notice in $noticeFiles) {
        $safePackage = $parts[0] -replace '[^A-Za-z0-9.-]', '-'
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $noticeDir "$safePackage-$($notice.Name)")
    }
}

$manifestPath = Join-Path $stage 'FILE-MANIFEST.sha256'
$manifestLines = Get-ChildItem -LiteralPath $stage -Recurse -File |
    Where-Object FullName -ne $manifestPath |
    Sort-Object FullName |
    ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\', '/')
        "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant(), $relative
    }
[IO.File]::WriteAllLines($manifestPath, $manifestLines, [Text.UTF8Encoding]::new($false))

$zipPath = Join-Path $releaseRoot "$folderName.zip"
Compress-Archive -LiteralPath $stage -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
$checksumPath = "$zipPath.sha256.txt"
[IO.File]::WriteAllText($checksumPath, "$zipHash  $([IO.Path]::GetFileName($zipPath))`n", [Text.UTF8Encoding]::new($false))

& (Join-Path $PSScriptRoot 'Test-PublicRepo.ps1') -SkipBuild -ReleaseZip $zipPath
if ($LASTEXITCODE -ne 0) { throw 'Public repository validation failed.' }

Write-Host "Release archive: $zipPath"
Write-Host "SHA-256: $zipHash"
