[CmdletBinding()]
param(
    [string]$ZipPath = '',

    [string]$ChecksumPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $repositoryRoot 'PortableReleaseFiles.psd1')
$defaultOutput = Join-Path $repositoryRoot 'artifacts\portable'

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = Join-Path $defaultOutput ($manifest.ArchiveBaseName + '.zip')
}

if ([string]::IsNullOrWhiteSpace($ChecksumPath)) {
    $ChecksumPath = Join-Path $defaultOutput ($manifest.ArchiveBaseName + '.sha256')
}

$zipFullPath = [IO.Path]::GetFullPath($ZipPath)
$checksumFullPath = [IO.Path]::GetFullPath($ChecksumPath)
$validationRoot = Join-Path ([IO.Path]::GetTempPath()) ('VITS-v1-Portable-' + [Guid]::NewGuid().ToString('N'))
$firstExtract = Join-Path $validationRoot 'first-location'
$secondExtract = Join-Path $validationRoot 'second-location'

function Get-RelativePortablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return $Path.Substring($Root.TrimEnd('\').Length + 1).Replace('\', '/')
}

function Get-ExpectedPortableFiles {
    $expected = New-Object Collections.Generic.List[string]

    foreach ($path in $manifest.RootRuntimeFiles) {
        $expected.Add($path.Replace('\', '/'))
    }

    foreach ($path in $manifest.NativeRuntimeFiles) {
        $expected.Add($path.Replace('\', '/'))
    }

    foreach ($path in $manifest.DocumentationFiles) {
        $expected.Add((Split-Path -Leaf $path))
    }

    foreach ($folder in $manifest.PortableFolders) {
        $expected.Add(($folder + '/README.txt'))
    }

    $expected.Add('SHA256SUMS.txt')
    return @($expected | Sort-Object -Unique)
}

function Assert-ExactFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $expected = Get-ExpectedPortableFiles
    $actual = @(Get-ChildItem -LiteralPath $Root -File -Recurse |
        ForEach-Object { Get-RelativePortablePath $Root $_.FullName } |
        Sort-Object -Unique)
    $missing = @($expected | Where-Object { $_ -notin $actual })
    $unexpected = @($actual | Where-Object { $_ -notin $expected })

    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        throw "Portable files differ from the allowlist. Missing: $($missing -join ', '). Unexpected: $($unexpected -join ', ')."
    }
}

function Assert-InternalHashes {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $manifestFile = Join-Path $Root 'SHA256SUMS.txt'
    $lines = @(Get-Content -LiteralPath $manifestFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $expectedHashedFiles = @((Get-ExpectedPortableFiles) | Where-Object { $_ -ne 'SHA256SUMS.txt' })

    if ($lines.Count -ne $expectedHashedFiles.Count) {
        throw 'The internal SHA-256 manifest does not cover every payload file exactly once.'
    }

    $seen = New-Object Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)

    foreach ($line in $lines) {
        if ($line -notmatch '^([A-Fa-f0-9]{64}) \*(.+)$') {
            throw "Invalid internal checksum line: $line"
        }

        $expectedHash = $Matches[1].ToUpperInvariant()
        $relativePath = $Matches[2].Replace('\', '/')

        if (-not $seen.Add($relativePath)) {
            throw "Duplicate internal checksum path: $relativePath"
        }

        if ($relativePath -notin $expectedHashedFiles) {
            throw "The internal checksum references an unapproved file: $relativePath"
        }

        $filePath = Join-Path $Root $relativePath.Replace('/', '\')
        $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToUpperInvariant()

        if ($actualHash -ne $expectedHash) {
            throw "SHA-256 mismatch for $relativePath"
        }
    }
}

function Assert-FrameworkAndVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $executable = Join-Path $Root 'VisualInpsectionTrainingSystem.exe'
    $configuration = Join-Path $Root 'VisualInpsectionTrainingSystem.exe.config'
    [xml]$configXml = Get-Content -LiteralPath $configuration -Raw
    $supportedRuntime = $configXml.configuration.startup.supportedRuntime

    if ($supportedRuntime.sku -ne '.NETFramework,Version=v4.6.2') {
        throw "Portable executable configuration targets an unexpected framework: $($supportedRuntime.sku)"
    }

    $versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)

    if ($versionInfo.FileVersion -ne $manifest.AssemblyVersion) {
        throw "Unexpected file version: $($versionInfo.FileVersion)"
    }

    if ($versionInfo.ProductVersion -ne $manifest.Version) {
        throw "Unexpected product version: $($versionInfo.ProductVersion)"
    }

    $assemblyMetadata = [Text.Encoding]::UTF8.GetString(
        [IO.File]::ReadAllBytes($executable))

    if (-not $assemblyMetadata.Contains('.NETFramework,Version=v4.6.2')) {
        throw 'The executable metadata does not target .NET Framework 4.6.2.'
    }
}

function Assert-SafeContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $forbiddenExtensions = @('.pdb', '.xml', '.cs', '.csproj', '.sln', '.slnx', '.ps1', '.trx', '.log', '.csv', '.xlsx', '.pdf', '.bmp')
    $forbiddenSegments = @('.git', 'packages', 'bin', 'obj', 'TestResults', 'arm64')

    foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse) {
        $relativePath = Get-RelativePortablePath $Root $file.FullName

        if ($file.Extension -in $forbiddenExtensions) {
            throw "Forbidden file extension in portable package: $relativePath"
        }

        foreach ($segment in $forbiddenSegments) {
            if ($relativePath.Split('/') -contains $segment) {
                throw "Forbidden path segment in portable package: $relativePath"
            }
        }

        if ($file.Name -eq 'DatabaseSettings.local.config' -or
            $file.Name -like '*Tests.dll') {
            throw "Forbidden portable file: $relativePath"
        }
    }

    $examplePath = Join-Path $Root 'DatabaseSettings.example.config'
    [xml]$example = Get-Content -LiteralPath $examplePath -Raw

    if ($example.applicationSettings.mysql.username -ne 'YOUR_DATABASE_USER' -or
        $example.applicationSettings.mysql.password -ne 'YOUR_DATABASE_PASSWORD') {
        throw 'The example configuration does not contain the approved credential placeholders.'
    }

    $paths = $example.applicationSettings.paths

    if ($paths.quizImageFolder -ne '.\QuizImages' -or
        $paths.logFolder -ne '.\Logs' -or
        $paths.exportFolder -ne '.\Exports' -or
        $paths.reportFolder -ne '.\Reports') {
        throw 'The example configuration does not preserve relative portable paths.'
    }

    $textFiles = Get-ChildItem -LiteralPath $Root -File -Recurse |
        Where-Object { $_.Extension -in '.md', '.txt', '.config' }
    $forbiddenTextPatterns = @(
        'C:\\Users\\',
        'Visual Studio\\My Project',
        'gho_[A-Za-z0-9_]+',
        '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
    )

    foreach ($textFile in $textFiles) {
        $content = Get-Content -LiteralPath $textFile.FullName -Raw

        foreach ($pattern in $forbiddenTextPatterns) {
            if ($content -match $pattern) {
                throw "Sensitive or development-only text was found in $($textFile.Name)."
            }
        }
    }
}

function Assert-PortableLocation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    Assert-ExactFiles $Root
    Assert-InternalHashes $Root
    Assert-FrameworkAndVersion $Root
    Assert-SafeContent $Root
}

try {
    if (-not (Test-Path -LiteralPath $zipFullPath)) {
        throw "Portable ZIP was not found: $zipFullPath"
    }

    if (-not (Test-Path -LiteralPath $checksumFullPath)) {
        throw "Portable ZIP checksum was not found: $checksumFullPath"
    }

    $checksumLine = (Get-Content -LiteralPath $checksumFullPath -Raw).Trim()

    if ($checksumLine -notmatch '^([A-Fa-f0-9]{64}) \*(.+)$') {
        throw 'The external ZIP checksum file is invalid.'
    }

    $expectedZipHash = $Matches[1].ToUpperInvariant()
    $expectedZipName = $Matches[2]

    if ($expectedZipName -ne (Split-Path -Leaf $zipFullPath)) {
        throw 'The external checksum names a different ZIP file.'
    }

    $actualZipHash = (Get-FileHash -LiteralPath $zipFullPath -Algorithm SHA256).Hash.ToUpperInvariant()

    if ($actualZipHash -ne $expectedZipHash) {
        throw 'The portable ZIP SHA-256 checksum does not match.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zipFullPath)

    try {
        foreach ($entry in $archive.Entries) {
            if ([IO.Path]::IsPathRooted($entry.FullName) -or
                $entry.FullName.Split('/') -contains '..') {
                throw "Unsafe ZIP entry path: $($entry.FullName)"
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    New-Item -ItemType Directory -Path $firstExtract -Force | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($zipFullPath, $firstExtract)
    Assert-PortableLocation $firstExtract

    New-Item -ItemType Directory -Path $secondExtract -Force | Out-Null
    Copy-Item -Path (Join-Path $firstExtract '*') -Destination $secondExtract -Recurse -Force
    Assert-PortableLocation $secondExtract

    Write-Host 'Portable Release validation completed successfully.' -ForegroundColor Green
    Write-Host "Validated ZIP: $zipFullPath"
    Write-Host "Validated files: $((Get-ExpectedPortableFiles).Count)"
    Write-Host 'Validated writable locations: 2'
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        $resolvedValidationRoot = (Resolve-Path -LiteralPath $validationRoot).Path
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')

        if (-not $resolvedValidationRoot.StartsWith(
                $temporaryRoot + '\',
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove validation output outside the temporary directory: $resolvedValidationRoot"
        }

        Remove-Item -LiteralPath $resolvedValidationRoot -Recurse -Force
    }
}
