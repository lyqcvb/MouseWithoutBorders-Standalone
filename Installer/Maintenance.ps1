# Copyright (c) Microsoft Corporation. Licensed under the MIT license.
[CmdletBinding()]
param(
    [ValidateSet('Prepare', 'Configure', 'Uninstall')]
    [string] $Operation,
    [string] $InstallDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ServiceExecutable([string] $CommandLine) {
    if ([string]::IsNullOrWhiteSpace($CommandLine)) { return $null }
    $text = $CommandLine.Trim()
    if ($text.StartsWith('"')) {
        $end = $text.IndexOf('"', 1)
        if ($end -le 1) { return $null }
        return $text.Substring(1, $end - 1)
    }
    return ($text -split '\s+', 2)[0]
}

function Test-SamePath([string] $Left, [string] $Right) {
    if ([string]::IsNullOrWhiteSpace($Left) -or [string]::IsNullOrWhiteSpace($Right)) { return $false }
    try {
        return [string]::Equals([IO.Path]::GetFullPath($Left).TrimEnd('\'),
            [IO.Path]::GetFullPath($Right).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)
    } catch { return $false }
}

function Invoke-InstallationMaintenance {
    if (-not $Operation -or -not [IO.Path]::IsPathRooted($InstallDir)) {
        throw 'Operation and an absolute InstallDir are required.'
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Installation maintenance requires administrator permission.'
    }

    $mainExe = Join-Path $InstallDir 'PowerToys.MouseWithoutBorders.exe'
    $serviceExe = Join-Path $InstallDir 'PowerToys.MouseWithoutBordersService.exe'
    $ruleName = 'MouseWithoutBorders-Standalone-TCP-In'

    if ($Operation -eq 'Prepare' -or $Operation -eq 'Uninstall') {
        $service = Get-CimInstance Win32_Service -Filter "Name='PowerToys.MWB.Service'"
        if ($service -and (Test-SamePath (Get-ServiceExecutable $service.PathName) $serviceExe)) {
            # Never stop or delete the service registered by PowerToys or another directory.
            if ($service.State -ne 'Stopped') {
                Stop-Service -Name 'PowerToys.MWB.Service'
                (Get-Service 'PowerToys.MWB.Service').WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
            }
            if ($Operation -eq 'Uninstall') {
                $process = Start-Process -FilePath $serviceExe -ArgumentList 'uninstall' -Wait -PassThru
                if ($process.ExitCode -ne 0) { throw "Service removal failed: $($process.ExitCode)" }
            }
        }

        # SYSTEM desktop instances hold the installed files too. Match the full image path;
        # matching the name alone would stop Garage/PowerToys or a developer's build as well.
        $images = @('PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBordersHelper.exe',
            'PowerToys.MouseWithoutBordersService.exe')
        foreach ($image in $images) {
            $expected = Join-Path $InstallDir $image
            foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name='$image'")) {
                if (Test-SamePath $process.ExecutablePath $expected) {
                    $result = Invoke-CimMethod -InputObject $process -MethodName Terminate -Arguments @{ Reason = 0 }
                    if ($result.ReturnValue -ne 0) { throw "Could not stop $expected ($($result.ReturnValue))" }
                }
            }
        }
    }

    if ($Operation -eq 'Configure' -or $Operation -eq 'Uninstall') {
        # Work only in the local persistent store; leave organization-managed rules alone.
        $rules = @(Get-NetFirewallRule -PolicyStore PersistentStore | Where-Object Name -eq $ruleName)
        foreach ($rule in $rules) {
            $filter = $rule | Get-NetFirewallApplicationFilter
            if (-not (Test-SamePath $filter.Program $mainExe)) {
                if ($Operation -eq 'Configure') { throw 'The installer firewall rule belongs to another directory.' }
                continue
            }
            $rule | Remove-NetFirewallRule
        }
        if ($Operation -eq 'Configure') {
            # Port is configurable in settings.json. Scope to the image rather than fixed ports.
            New-NetFirewallRule -PolicyStore PersistentStore -Name $ruleName `
                -DisplayName 'Mouse Without Borders Standalone (TCP, local network)' `
                -Direction Inbound -Action Allow -Enabled True -Program $mainExe `
                -Protocol TCP -Profile Private -RemoteAddress LocalSubnet | Out-Null
        }
    }
}

# Dot-sourcing loads the pure path functions for tests without touching the machine.
if ($MyInvocation.InvocationName -ne '.') {
    $log = Join-Path ([IO.Path]::GetTempPath()) 'installation-maintenance.log'
    try {
        Invoke-InstallationMaintenance
        Add-Content -LiteralPath $log -Value "$(Get-Date -Format o) $Operation succeeded: $InstallDir"
        exit 0
    } catch {
        Add-Content -LiteralPath $log -Value "$(Get-Date -Format o) $Operation failed: $($_.Exception.Message)"
        Write-Error $_ -ErrorAction Continue
        exit 1
    }
}
