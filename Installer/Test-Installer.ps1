# Copyright (c) Microsoft Corporation. Licensed under the MIT license.
# Read-only tests: do not start the app, change services, or alter firewall/shortcuts.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $tokens = $null
    $errors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref] $tokens, [ref] $errors)
    if ($errors.Count) { throw "$($script.Name): $($errors -join '; ')" }
}
. (Join-Path $PSScriptRoot 'Maintenance.ps1')

function Assert-True([bool] $Value, [string] $Message) {
    if (-not $Value) { throw $Message }
}
Assert-True ((Get-ServiceExecutable '"C:\Program Files\MWB\service.exe" "C:\Users\x\AppData\Local"') -eq 'C:\Program Files\MWB\service.exe') 'Quoted service paths must preserve spaces.'
Assert-True ((Get-ServiceExecutable 'C:\MWB\service.exe arguments') -eq 'C:\MWB\service.exe') 'Unquoted paths must stop at the argument.'
Assert-True ($null -eq (Get-ServiceExecutable '"unfinished')) 'Malformed quotes must not match.'
Assert-True (Test-SamePath 'C:\Program Files\MWB\service.exe' 'c:\program files\mwb\service.exe') 'Path comparisons must ignore case.'
Assert-True (-not (Test-SamePath 'C:\PowerToys\service.exe' 'C:\MWB\service.exe')) 'Another installation must not match.'
Assert-True (-not (Test-SamePath $null 'C:\MWB\service.exe')) 'Unavailable process paths must not match.'

$setup = Get-Content (Join-Path $PSScriptRoot 'MouseWithoutBorders.iss') -Raw
$maintenance = Get-Content (Join-Path $PSScriptRoot 'Maintenance.ps1') -Raw
Assert-True ($setup.Contains('Tasks: autostart')) 'Startup must be an explicit setup task.'
Assert-True ($setup.Contains('{commonstartup}\Mouse Without Borders Standalone')) 'Autostart must have a unique, installer-owned shortcut.'
Assert-True ($setup.Contains('runasoriginaluser')) 'Post-install launch must use the original user.'
Assert-True ($maintenance.Contains('-Profile Private -RemoteAddress LocalSubnet')) 'Firewall scope must stay private/local subnet.'
Assert-True ($maintenance.Contains('Test-SamePath (Get-ServiceExecutable $service.PathName) $serviceExe')) 'Service removal must check the registered image path.'
Assert-True ($maintenance.Contains('Test-SamePath $process.ExecutablePath $expected')) 'Process cleanup must check the full image path.'
Assert-True (-not $setup.Contains('[UninstallDelete]')) 'Uninstall must not recursively delete user data or untracked files.'
Write-Host 'Installer script checks passed (path ownership, firewall scope, startup and launch wiring).'
