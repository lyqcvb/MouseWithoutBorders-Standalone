# Copyright (c) Microsoft Corporation. Licensed under the MIT license.
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $PublishDir)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $PublishDir 'Licenses'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$entries = @{}
$mit = Get-Content (Join-Path $repo 'LICENSE') -Raw
$manifest = New-Object 'System.Collections.Generic.List[string]'
$manifest.Add('Third-party dependencies of this standalone build. Versions are read from restored NuGet assets.')
$manifest.Add('Build-time packages may also be listed. Original license and notice files are preserved below.')

foreach ($project in @('MouseWithoutBorders', 'MouseWithoutBordersHelper', 'MouseWithoutBordersService', 'MouseWithoutBorders.Shared')) {
    $assets = Get-Content (Join-Path $repo "artifacts\obj\$project\project.assets.json") -Raw | ConvertFrom-Json
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -ne 'package' -or $entries.ContainsKey($library.Name)) { continue }
        $entries[$library.Name] = $true
        $package = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder $library.Value.path
            if (Test-Path -LiteralPath $candidate) { $package = $candidate; break }
        }
        if (-not $package) { throw "NuGet package not found: $($library.Name)" }
        $nuspec = Get-ChildItem -LiteralPath $package -Filter '*.nuspec' | Select-Object -First 1
        [xml] $xml = Get-Content -LiteralPath $nuspec.FullName
        $metadata = $xml.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
        $license = $metadata.SelectSingleNode('*[local-name()="license"]')
        if (-not $license) { throw "Missing license metadata: $($library.Name)" }
        $target = Join-Path $destination ($library.Name -replace '/', '-')
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Copy-Item -LiteralPath $nuspec.FullName -Destination $target
        if ($license.type -eq 'file') {
            $licensePath = Join-Path $package $license.InnerText
            if (-not (Test-Path -LiteralPath $licensePath)) { throw "Missing license file: $licensePath" }
            Copy-Item -LiteralPath $licensePath -Destination (Join-Path $target 'LICENSE.txt')
        } elseif ($license.InnerText -eq 'MIT') {
            $copyrightNode = $metadata.SelectSingleNode('*[local-name()="copyright"]')
            $authorsNode = $metadata.SelectSingleNode('*[local-name()="authors"]')
            $copyright = if ($copyrightNode -and $copyrightNode.InnerText) { $copyrightNode.InnerText } else { "Copyright: $($authorsNode.InnerText)" }
            Set-Content -LiteralPath (Join-Path $target 'LICENSE.txt') -Encoding UTF8 `
                -Value ($mit.Replace('Copyright (c) Microsoft Corporation. All rights reserved.', $copyright))
        } else { throw "Review this license before distributing: $($library.Name) $($license.InnerText)" }
        # Runtime packs carry notices that are not copied by dotnet publish.
        foreach ($file in @(Get-ChildItem -LiteralPath $package -File | Where-Object Name -Match '^(LICENSE|NOTICE|THIRD.PARTY)')) {
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
        $manifest.Add("$($library.Name): $($license.InnerText)")
    }
}
# Self-contained framework packs can be restored as download dependencies, not libraries.
foreach ($assetsFile in @(Get-ChildItem (Join-Path $repo 'artifacts\obj') -Filter project.assets.json -Recurse)) {
    $assets = Get-Content -LiteralPath $assetsFile.FullName -Raw | ConvertFrom-Json
    foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
        if (-not $framework.Value.PSObject.Properties['downloadDependencies']) { continue }
        foreach ($dependency in $framework.Value.downloadDependencies) {
            if ($dependency.name -notmatch '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64$') { continue }
            $version = $dependency.version.Trim('[', ']').Split(',')[0].Trim()
            $key = "$($dependency.name)/$version"
            if ($entries.ContainsKey($key)) { continue }
            $entries[$key] = $true
            $found = $false
            foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
                $package = Join-Path $folder "$($dependency.name.ToLowerInvariant())\$version"
                if (-not (Test-Path $package)) { continue }
                $target = Join-Path $destination ($key -replace '/', '-')
                New-Item -ItemType Directory -Path $target -Force | Out-Null
                $files = @(Get-ChildItem -LiteralPath $package -File | Where-Object Name -Match '^(LICENSE|NOTICE|THIRD.PARTY)')
                if ($files.Count -eq 0) { throw "Runtime notices missing: $key" }
                $files | Copy-Item -Destination $target
                $manifest.Add("${key}: bundled runtime license and third-party notices")
                $found = $true
                break
            }
            if (-not $found) { throw "Runtime pack not found: $key" }
        }
    }
}
# The Windows SDK projection is bundled too, but its pack exposes only licenseUrl.
$sdkFound = $false
foreach ($assetsFile in @(Get-ChildItem (Join-Path $repo 'artifacts\obj') -Filter project.assets.json -Recurse)) {
    $assets = Get-Content -LiteralPath $assetsFile.FullName -Raw | ConvertFrom-Json
    foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
        if (-not $framework.Value.PSObject.Properties['downloadDependencies']) { continue }
        foreach ($dependency in $framework.Value.downloadDependencies) {
            if ($dependency.name -ne 'Microsoft.Windows.SDK.NET.Ref') { continue }
            $version = $dependency.version.Trim('[', ']').Split(',')[0].Trim()
            $target = Join-Path $destination "Microsoft.Windows.SDK.NET.Ref-$version"
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Licenses\Windows-SDK.rtf') -Destination $target
            $manifest.Add("Microsoft.Windows.SDK.NET.Ref/${version}: Windows SDK license (Windows-SDK.rtf), source https://aka.ms/WinSDKLicenseURL")
            $sdkFound = $true
            break
        }
        if ($sdkFound) { break }
    }
    if ($sdkFound) { break }
}
if (-not $sdkFound) { throw 'Windows SDK projection license version could not be determined.' }
$manifest | Set-Content -LiteralPath (Join-Path $PublishDir 'THIRD-PARTY-NOTICES.txt') -Encoding UTF8
