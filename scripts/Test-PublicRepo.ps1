[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [string]$ReleaseZip
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\UacMaintenanceTray\UacMaintenanceTray.csproj'
$failures = [Collections.Generic.List[string]]::new()

$excluded = @('bin', 'obj', 'artifacts', '.git', 'Logs', 'logs')
$files = Get-ChildItem -LiteralPath $repoRoot -Recurse -File | Where-Object {
    $relativeParts = [IO.Path]::GetRelativePath($repoRoot, $_.FullName) -split '[\\/]'
    -not ($relativeParts | Where-Object { $_ -in $excluded })
}

$forbiddenNames = '(?i)(^|\.)((env)|(pfx)|(p12)|(pem)|(key)|(reg)|(user)|(suo))$'
foreach ($file in $files) {
    if ($file.Name -match $forbiddenNames) {
        $failures.Add("Forbidden file name: $($file.FullName)")
    }
}

$textExtensions = @('.cs', '.csproj', '.ps1', '.md', '.json', '.yml', '.yaml', '.xml', '.svg', '.txt')
$privacyPatterns = @(
    'C:\\Users\\',
    'E:\\DevTools',
    'V:\\Caches',
    'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY',
    '(?i)(github|ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{20,}',
    '(?i)password\s*[:=]\s*[^\s]+'
)
foreach ($file in $files | Where-Object Extension -in $textExtensions) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($pattern in $privacyPatterns) {
        if ($content -match $pattern) {
            $failures.Add("Sensitive or machine-local content matches '$pattern': $($file.FullName)")
        }
    }
}

$markdownFiles = $files | Where-Object Extension -eq '.md'
foreach ($file in $markdownFiles) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($match in [regex]::Matches($content, '\[[^\]]+\]\((?!https?://|#|mailto:)([^)]+)\)')) {
        $target = [Uri]::UnescapeDataString($match.Groups[1].Value.Split('#')[0])
        if ($target -and -not (Test-Path -LiteralPath (Join-Path $file.DirectoryName $target))) {
            $failures.Add("Broken relative link '$target' in $($file.FullName)")
        }
    }
}

if (-not $SkipBuild) {
    dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { $failures.Add('dotnet build failed.') }
}

if ($ReleaseZip) {
    if (-not (Test-Path -LiteralPath $ReleaseZip)) {
        $failures.Add("Release ZIP does not exist: $ReleaseZip")
    }
    else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($ReleaseZip)
        try {
            $names = @($archive.Entries.FullName)
            $required = @('UacMaintenanceTray.exe', 'README.md', 'README.zh-CN.md', 'LICENSE', 'assets/uac-maintenance-tray.svg', 'FILE-MANIFEST.sha256')
            foreach ($requiredFile in $required) {
                if (-not ($names | Where-Object { $_.Replace('\', '/').EndsWith("/$requiredFile") })) {
                    $failures.Add("Release ZIP is missing $requiredFile")
                }
            }
            if (-not ($names | Where-Object { $_ -match '/runtime-notices/.+' })) {
                $failures.Add('Release ZIP has no runtime notices.')
            }
        }
        finally {
            $archive.Dispose()
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Public repository validation passed ($($files.Count) authored files checked)."
