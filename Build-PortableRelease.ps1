[CmdletBinding()]
param(
    [switch]$SkipBuild,

    [string]$OutputDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $repositoryRoot 'PortableReleaseFiles.psd1'
$manifest = Import-PowerShellDataFile -LiteralPath $manifestPath
$releaseDirectory = Join-Path $repositoryRoot 'bin\Release'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\portable'
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
$stagingParent = Join-Path $outputRoot 'staging'
$stagingRoot = Join-Path $stagingParent $manifest.ArchiveBaseName
$zipPath = Join-Path $outputRoot ($manifest.ArchiveBaseName + '.zip')
$checksumPath = Join-Path $outputRoot ($manifest.ArchiveBaseName + '.sha256')
$utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false

function Get-VisualStudioTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw 'Visual Studio Installer vswhere.exe was not found.'
    }

    $installationPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installationPath)) {
        throw 'A Visual Studio installation containing MSBuild was not found.'
    }

    $toolPath = Join-Path $installationPath.Trim() $RelativePath

    if (-not (Test-Path -LiteralPath $toolPath)) {
        throw "Required Visual Studio tool was not found: $RelativePath"
    }

    return $toolPath
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Write-Host "`n== $Description ==" -ForegroundColor Cyan
    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Remove-ReleasePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$AllowedParent
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $resolvedParent = [IO.Path]::GetFullPath($AllowedParent).TrimEnd('\')

    if (-not $resolvedPath.StartsWith(
            $resolvedParent + '\',
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a path outside the portable output root: $resolvedPath"
    }

    $item = Get-Item -LiteralPath $resolvedPath -Force

    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to remove a reparse point: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Assert-ExactNames {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Expected,

        [Parameter(Mandatory = $true)]
        [string[]]$Actual,

        [Parameter(Mandatory = $true)]
        [string]$Scope
    )

    $missing = @($Expected | Where-Object { $_ -notin $Actual })
    $unexpected = @($Actual | Where-Object { $_ -notin $Expected })

    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        throw "$Scope differs from the committed allowlist. Missing: $($missing -join ', '). Unexpected: $($unexpected -join ', ')."
    }
}

function Get-RelativePortablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return $Path.Substring($Root.TrimEnd('\').Length + 1).Replace('\', '/')
}

if (-not $SkipBuild) {
    $msbuild = Get-VisualStudioTool 'MSBuild\Current\Bin\MSBuild.exe'
    $projectPath = Join-Path $repositoryRoot 'VisualInspectionTrainingSystem.csproj'

    Invoke-CheckedCommand $msbuild @(
        $projectPath,
        '/t:Restore',
        '/p:RestorePackagesConfig=true',
        "/p:SolutionDir=$repositoryRoot",
        "/p:RestorePackagesPath=$repositoryRoot\packages",
        '/verbosity:minimal'
    ) 'Restore production packages'

    Invoke-CheckedCommand $msbuild @(
        $projectPath,
        '/t:Rebuild',
        '/p:Configuration=Release',
        '/verbosity:minimal'
    ) 'Rebuild portable Release application'
}

if (-not (Test-Path -LiteralPath $releaseDirectory)) {
    throw 'The Release output directory does not exist. Run a clean Release build first.'
}

$actualRootFiles = @(Get-ChildItem -LiteralPath $releaseDirectory -File |
    Where-Object { $_.Extension -in '.exe', '.dll', '.config' } |
    ForEach-Object { $_.Name })
Assert-ExactNames $manifest.RootRuntimeFiles $actualRootFiles 'Release root runtime files'

foreach ($architecture in @('x86', 'x64')) {
    $expectedNames = @($manifest.NativeRuntimeFiles |
        Where-Object { $_ -like ($architecture + '\*') } |
        ForEach-Object { Split-Path -Leaf $_ })
    $architectureDirectory = Join-Path $releaseDirectory $architecture
    $actualNames = @(Get-ChildItem -LiteralPath $architectureDirectory -File |
        ForEach-Object { $_.Name })
    Assert-ExactNames $expectedNames $actualNames "$architecture native runtime files"
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Remove-ReleasePath $stagingParent $outputRoot
Remove-ReleasePath $zipPath $outputRoot
Remove-ReleasePath $checksumPath $outputRoot
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

foreach ($fileName in $manifest.RootRuntimeFiles) {
    Copy-Item -LiteralPath (Join-Path $releaseDirectory $fileName) -Destination $stagingRoot
}

foreach ($relativePath in $manifest.NativeRuntimeFiles) {
    $destination = Join-Path $stagingRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $releaseDirectory $relativePath) -Destination $destination
}

foreach ($relativePath in $manifest.DocumentationFiles) {
    $source = Join-Path $repositoryRoot $relativePath

    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required portable documentation is missing: $relativePath"
    }

    Copy-Item -LiteralPath $source -Destination (Join-Path $stagingRoot (Split-Path -Leaf $relativePath))
}

$folderMessages = @{
    QuizImages = 'Place authorized BMP training images in this folder before starting a quiz.'
    Logs = 'Application log files are created in this folder at runtime.'
    Exports = 'Generated CSV, XLSX, and PDF exports are written to this folder.'
    Reports = 'Application-generated report output is written to this folder.'
}

foreach ($folderName in $manifest.PortableFolders) {
    $folderPath = Join-Path $stagingRoot $folderName
    New-Item -ItemType Directory -Path $folderPath -Force | Out-Null
    [IO.File]::WriteAllText(
        (Join-Path $folderPath 'README.txt'),
        $folderMessages[$folderName] + [Environment]::NewLine,
        $utf8)
}

$payloadFiles = @(Get-ChildItem -LiteralPath $stagingRoot -File -Recurse |
    Sort-Object FullName)
$hashLines = @($payloadFiles | ForEach-Object {
    $relativePath = Get-RelativePortablePath $stagingRoot $_.FullName
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    "$hash *$relativePath"
})
[IO.File]::WriteAllLines(
    (Join-Path $stagingRoot 'SHA256SUMS.txt'),
    $hashLines,
    $utf8)

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $stagingRoot,
    $zipPath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
[IO.File]::WriteAllText(
    $checksumPath,
    "$zipHash *$(Split-Path -Leaf $zipPath)$([Environment]::NewLine)",
    $utf8)

Write-Host "`nPortable Release created successfully." -ForegroundColor Green
Write-Host "ZIP: $zipPath"
Write-Host "Checksum: $checksumPath"
Write-Host "Staging: $stagingRoot"
