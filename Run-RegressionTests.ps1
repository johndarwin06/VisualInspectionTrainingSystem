[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release', 'All')]
    [string]$Configuration = 'All',

    [ValidateSet('Safe', 'Required', 'Preflight', 'Cleanup')]
    [string]$DatabaseMode = 'Safe',

    [switch]$ContinuousIntegration,

    [switch]$SkipRestore,

    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repositoryRoot 'VisualInpsectionTrainingSystem.slnx'
$productionProject = Join-Path $repositoryRoot 'VisualInspectionTrainingSystem.csproj'
$testProject = Join-Path $repositoryRoot 'VisualInspectionTrainingSystem.Tests\VisualInspectionTrainingSystem.Tests.csproj'
$testAssemblyName = 'VisualInspectionTrainingSystem.Tests.dll'
$testTimeoutMilliseconds = 300000

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

function Invoke-BoundedVsTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath,

        [Parameter(Mandatory = $true)]
        [string]$AdapterPath,

        [Parameter(Mandatory = $true)]
        [string]$Platform,

        [Parameter(Mandatory = $true)]
        [string]$Filter,

        [Parameter(Mandatory = $true)]
        [string]$Description,

        [switch]$RejectSkipped
    )

    Write-Host "`n== $Description ==" -ForegroundColor Cyan

    $arguments = @(
        $AssemblyPath,
        "/TestAdapterPath:$AdapterPath",
        "/Platform:$Platform",
        "/TestCaseFilter:$Filter",
        '/Logger:console;Verbosity=minimal',
        '/InIsolation'
    )

    $escapedArguments = $arguments | ForEach-Object {
        '"' + $_.Replace('"', '\"') + '"'
    }
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $script:VsTestPath
    $startInfo.Arguments = $escapedArguments -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    if (-not $process.Start()) {
        $process.Dispose()
        throw "$Description could not start the Visual Studio test host."
    }

    $outputTask = $process.StandardOutput.ReadToEndAsync()
    $errorTask = $process.StandardError.ReadToEndAsync()

    try {
        if (-not $process.WaitForExit($testTimeoutMilliseconds)) {
            $process.Kill()
            $process.WaitForExit()
            throw "$Description exceeded the five-minute bounded test timeout."
        }

        $standardOutput = $outputTask.GetAwaiter().GetResult()
        $standardError = $errorTask.GetAwaiter().GetResult()

        if (-not [string]::IsNullOrWhiteSpace($standardOutput)) {
            Write-Host $standardOutput.TrimEnd()
        }

        if (-not [string]::IsNullOrWhiteSpace($standardError)) {
            Write-Warning $standardError.TrimEnd()
        }

        if ($process.ExitCode -ne 0) {
            throw "$Description failed with exit code $($process.ExitCode)."
        }

        if ($RejectSkipped -and
            $standardOutput -match 'Skipped:\s+([1-9][0-9]*)') {
            throw "$Description skipped database tests in a required database mode."
        }
    }
    finally {
        $process.Dispose()
    }
}

$msbuildPath = Get-VisualStudioTool 'MSBuild\Current\Bin\MSBuild.exe'
$script:VsTestPath = Get-VisualStudioTool 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'

if ($ContinuousIntegration -and $DatabaseMode -ne 'Safe') {
    throw 'ContinuousIntegration may only use the fail-closed Safe database mode.'
}

function Get-TestDatabaseEnvironmentSetting {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable(
        $Name,
        [EnvironmentVariableTarget]::Process)

    if (-not [string]::IsNullOrWhiteSpace($value)) {
        return $value
    }

    return [Environment]::GetEnvironmentVariable(
        $Name,
        [EnvironmentVariableTarget]::User)
}

$requiresDatabase = $DatabaseMode -ne 'Safe'

if ($requiresDatabase) {
    $connectionVariable = 'VITS_TEST_MYSQL_CONNECTION_STRING'
    $schemaVariable = 'VITS_TEST_MYSQL_SCHEMA'
    $connectionConfigured = -not [string]::IsNullOrWhiteSpace(
        (Get-TestDatabaseEnvironmentSetting $connectionVariable))
    $schemaConfigured = -not [string]::IsNullOrWhiteSpace(
        (Get-TestDatabaseEnvironmentSetting $schemaVariable))

    if (-not $connectionConfigured -or -not $schemaConfigured) {
        throw "DatabaseMode $DatabaseMode requires both $connectionVariable and $schemaVariable. No database test was started."
    }

    Write-Host "Database mode: $DatabaseMode (isolated configuration present; values hidden)." -ForegroundColor Yellow
}

$packageRoot = Join-Path $repositoryRoot 'packages'
$adapterPath = Join-Path $packageRoot 'nunit3testadapter\5.2.0\build\net462'

if (-not $SkipRestore) {
    Invoke-CheckedCommand $msbuildPath @(
        $productionProject,
        '/t:Restore',
        '/p:RestorePackagesConfig=true',
        "/p:SolutionDir=$repositoryRoot",
        "/p:RestorePackagesPath=$repositoryRoot\packages",
        '/verbosity:minimal'
    ) 'Restore production packages.config dependencies'

    Invoke-CheckedCommand $msbuildPath @(
        $testProject,
        '/t:Restore',
        "/p:RestorePackagesPath=$packageRoot",
        '/verbosity:minimal'
    ) 'Restore test PackageReference dependencies'
}

if (-not (Test-Path -LiteralPath $adapterPath)) {
    throw 'NUnit3TestAdapter 5.2.0 was not restored to the NuGet package cache.'
}

$configurations = if ($Configuration -eq 'All') {
    @('Debug', 'Release')
}
else {
    @($Configuration)
}

if (-not $SkipBuild) {
    foreach ($currentConfiguration in $configurations) {
        Invoke-CheckedCommand $msbuildPath @(
            $solutionPath,
            '/t:Rebuild',
            "/p:Configuration=$currentConfiguration",
            '/p:Platform=Any CPU',
            '/verbosity:minimal'
        ) "$currentConfiguration AnyCPU rebuild"
    }
}

$categories = switch ($DatabaseMode) {
    'Required' { @('Database') }
    'Preflight' { @('DatabasePreflight') }
    'Cleanup' { @('DatabaseCleanup') }
    default {
        if ($ContinuousIntegration) {
            @('Unit', 'Integration', 'Export', 'NativeDeployment')
        }
        else {
            @('Unit', 'Integration', 'WPF', 'Database', 'Export', 'NativeDeployment')
        }
    }
}

foreach ($currentConfiguration in $configurations) {
    $assemblyPath = Join-Path $repositoryRoot "VisualInspectionTrainingSystem.Tests\bin\$currentConfiguration\$testAssemblyName"

    if (-not (Test-Path -LiteralPath $assemblyPath)) {
        throw "The $currentConfiguration test assembly was not found."
    }

    foreach ($category in $categories) {
        Invoke-BoundedVsTest `
            $assemblyPath `
            $adapterPath `
            'x86' `
            "TestCategory=$category" `
            "$currentConfiguration $category tests (x86)" `
            -RejectSkipped:$requiresDatabase
    }

    if ($DatabaseMode -eq 'Safe') {
        Invoke-BoundedVsTest `
            $assemblyPath `
            $adapterPath `
            'x64' `
            'TestCategory=NativeDeployment' `
            "$currentConfiguration native deployment tests (x64)"
    }
}

Write-Host "`nRegression test run completed successfully." -ForegroundColor Green
