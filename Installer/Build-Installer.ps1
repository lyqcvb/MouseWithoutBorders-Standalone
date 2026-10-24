# Copyright (c) Microsoft Corporation. Licensed under the MIT license.
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $Version = '1.0.0',
    [string] $IsccPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $repo 'artifacts\installer\payload'
$output = Join-Path $repo 'artifacts\installer'

function Invoke-Checked([string] $File, [string[]] $Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$File failed with exit code $LASTEXITCODE" }
}

if (-not $IsccPath) {
    $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compiler) { $IsccPath = $compiler.Source }
    else {
        foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, "$env:LOCALAPPDATA\Programs")) {
            foreach ($major in @('7', '6')) {
                $candidate = Join-Path $base "Inno Setup $major\ISCC.exe"
                if (Test-Path -LiteralPath $candidate) { $IsccPath = $candidate; break }
            }
            if ($IsccPath) { break }
        }
    }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Install Inno Setup 6.4 or later, or pass -IsccPath with the path to ISCC.exe.'
}

Push-Location $repo
try {
    Invoke-Checked 'dotnet' @('build', 'MouseWithoutBorders.slnx', '-c', 'Release')
    Invoke-Checked 'dotnet' @('test', 'MouseWithoutBorders.UnitTests/MouseWithoutBorders.UnitTests.csproj', '-c', 'Release', '--no-build')
    Invoke-Checked 'powershell.exe' @('-NoProfile', '-File', "$PSScriptRoot\Test-Installer.ps1")

    # Only this dedicated staging directory is replaced; developer publish outputs are untouched.
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Invoke-Checked 'dotnet' @('publish', 'App/MouseWithoutBorders.csproj', '-c', 'Release', '-r', 'win-x64',
        '--self-contained', 'true', '-o', $stage, "-p:Version=$Version")
    foreach ($image in @('PowerToys.MouseWithoutBorders', 'PowerToys.MouseWithoutBordersHelper', 'PowerToys.MouseWithoutBordersService')) {
        foreach ($suffix in @('.exe', '.dll', '.deps.json', '.runtimeconfig.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $stage "$image$suffix"))) { throw "Missing payload: $image$suffix" }
        }
        $runtime = Get-Content (Join-Path $stage "$image.runtimeconfig.json") -Raw | ConvertFrom-Json
        if (-not $runtime.runtimeOptions.PSObject.Properties['includedFrameworks']) {
            throw "$image is not self-contained."
        }
    }
    if (-not (Test-Path (Join-Path $stage 'hostfxr.dll'))) { throw 'The .NET runtime is missing.' }

    Invoke-Checked 'powershell.exe' @('-NoProfile', '-File', "$PSScriptRoot\Collect-Licenses.ps1", '-PublishDir', $stage)
    Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $stage
    Invoke-Checked $IsccPath @('/Qp', "/DPublishDir=$stage", "/DOutputDir=$output", "/DAppVersion=$Version", "$PSScriptRoot\MouseWithoutBorders.iss")

    $setup = Join-Path $output "MouseWithoutBorders-Standalone-$Version-win-x64-Setup.exe"
    if (-not (Test-Path -LiteralPath $setup)) { throw 'The compiler did not produce an installer.' }
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$setup.sha256" -Encoding ASCII -Value "$hash  $([IO.Path]::GetFileName($setup))"
    Write-Host "Installer: $setup"
    Write-Host "SHA256: $hash"
} finally { Pop-Location }
