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

$englishLanguagePack = Join-Path $repoRoot 'languages\en-US.json'
$chineseLanguagePack = Join-Path $repoRoot 'languages\zh-CN.json'
if (-not (Test-Path -LiteralPath $englishLanguagePack) -or -not (Test-Path -LiteralPath $chineseLanguagePack)) {
    $failures.Add('Both en-US and zh-CN language packs are required.')
}
else {
    $englishKeys = @((Get-Content -Raw -LiteralPath $englishLanguagePack | ConvertFrom-Json -AsHashtable).Keys | Sort-Object)
    $chineseKeys = @((Get-Content -Raw -LiteralPath $chineseLanguagePack | ConvertFrom-Json -AsHashtable).Keys | Sort-Object)
    if (Compare-Object -ReferenceObject $englishKeys -DifferenceObject $chineseKeys) {
        $failures.Add('The en-US and zh-CN language packs do not contain the same keys.')
    }
    else {
        $englishPack = Get-Content -Raw -LiteralPath $englishLanguagePack | ConvertFrom-Json -AsHashtable
        $chinesePack = Get-Content -Raw -LiteralPath $chineseLanguagePack | ConvertFrom-Json -AsHashtable
        foreach ($key in $englishKeys) {
            $englishPlaceholders = @([regex]::Matches($englishPack[$key], '\{\d+\}').Value | Sort-Object -Unique)
            $chinesePlaceholders = @([regex]::Matches($chinesePack[$key], '\{\d+\}').Value | Sort-Object -Unique)
            if (Compare-Object -ReferenceObject $englishPlaceholders -DifferenceObject $chinesePlaceholders) {
                $failures.Add("Language-pack placeholders do not match for key '$key'.")
            }
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
        $releaseName = [IO.Path]::GetFileName($ReleaseZip)
        if ($releaseName -notmatch '-(self-contained|framework-dependent)\.zip$') {
            $failures.Add("Release ZIP name does not identify its deployment mode: $releaseName")
        }
        $isSelfContained = $releaseName -match '-self-contained\.zip$'
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($ReleaseZip)
        try {
            $names = @($archive.Entries.FullName)
            $required = @(
                'UacMaintenanceTray.exe',
                'docs/README.md',
                'docs/README.zh-CN.md',
                'docs/LOCALIZATION.md',
                'docs/RELEASE-NOTES.md',
                'licenses/PROJECT-LICENSE.txt',
                'assets/uac-maintenance-tray.svg',
                'languages/en-US.json',
                'languages/zh-CN.json',
                'checksums/FILE-MANIFEST.sha256'
            )
            foreach ($requiredFile in $required) {
                if (-not ($names | Where-Object { $_.Replace('\', '/').EndsWith("/$requiredFile") })) {
                    $failures.Add("Release ZIP is missing $requiredFile")
                }
            }
            if ($isSelfContained -and -not ($names | Where-Object { $_ -match '/licenses/Microsoft\..+' })) {
                $failures.Add('Release ZIP has no runtime notices.')
            }
            $rootPrefix = ($names[0].Replace('\', '/') -split '/')[0]
            $rootFiles = @($archive.Entries | Where-Object {
                -not $_.FullName.EndsWith('/') -and
                $_.FullName.Replace('\', '/').Substring($rootPrefix.Length + 1) -notmatch '/'
            })
            if ($rootFiles.Count -ne 1 -or -not $rootFiles[0].Name.Equals('UacMaintenanceTray.exe', [StringComparison]::OrdinalIgnoreCase)) {
                $failures.Add('Release root must contain only UacMaintenanceTray.exe; all supporting files belong in named folders.')
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
