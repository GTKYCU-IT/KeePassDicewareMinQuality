<#
.SYNOPSIS
    Builds and tests KeePassDicewareMinQuality, then writes the release DLL and its SHA-256 to artifacts\.

.PARAMETER KeePassPath
    Folder containing KeePass.exe to compile against. Defaults to the standard install location.

.PARAMETER DownloadKeePass
    Download the pinned portable KeePass into .keepass\ and compile against it (used by CI).

.EXAMPLE
    ./build.ps1
.EXAMPLE
    ./build.ps1 -KeePassPath 'D:\Tools\KeePass'
#>
[CmdletBinding()]
param(
    [string] $KeePassPath = 'C:\Program Files\KeePass Password Safe 2',
    [switch] $DownloadKeePass
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# KeePass release the plugin is compiled against in CI. Update version and hash together;
# the zip's hash is published on https://keepass.info/integrity.html.
$KeePassVersion = '2.61.1'
$KeePassZipSha256 = '3952354DB9B117E906F7CD4F9F5591065B95186472370DA47F46F3E246FEA864'

# Upstream Diceware release the tests run against. Update version and hash together;
# the hash is shown next to the asset on https://github.com/cmdwtf/KeePassDiceware/releases.
$DicewareVersion = '2.1.0'
$DicewareDllSha256 = '279E37243A03320B6DC653D32B8D8A9FE51968B19D2969B626F2241B2139E7F8'

$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'

function Invoke-Checked([scriptblock] $Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $Command" }
}

function Get-Pinned([string] $Url, [string] $Path, [string] $Sha256) {
    if ((Test-Path $Path) -and (Get-FileHash $Path -Algorithm SHA256).Hash -eq $Sha256) { return }
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    Invoke-Checked { curl.exe --fail --silent --show-error --location --output $Path $Url }
    $actual = (Get-FileHash $Path -Algorithm SHA256).Hash
    if ($actual -ne $Sha256) {
        Remove-Item $Path
        throw "$Url SHA-256 is $actual, expected $Sha256."
    }
}

if ($DownloadKeePass) {
    $KeePassPath = Join-Path $root '.keepass'
    if (-not (Test-Path (Join-Path $KeePassPath 'KeePass.exe'))) {
        $zip = Join-Path ([IO.Path]::GetTempPath()) "KeePass-$KeePassVersion.zip"
        Get-Pinned "https://sourceforge.net/projects/keepass/files/KeePass%202.x/$KeePassVersion/KeePass-$KeePassVersion.zip/download" $zip $KeePassZipSha256
        Expand-Archive $zip -DestinationPath $KeePassPath -Force
    }
}

$dicewareDll = Join-Path $root '.deps\KeePassDiceware.dll'
Get-Pinned "https://github.com/cmdwtf/KeePassDiceware/releases/download/v$DicewareVersion/KeePassDiceware.dll" $dicewareDll $DicewareDllSha256

# KeePass reads the update-check version from .meta/version.info; it must match the assembly version.
[xml] $project = Get-Content (Join-Path $root 'KeePassDicewareMinQuality.csproj')
$version = @($project.Project.PropertyGroup.VersionPrefix | Where-Object { $_ })[0]
$versionInfo = Get-Content (Join-Path $root '.meta\version.info') | Where-Object { $_ -like 'KeePassDicewareMinQuality:*' }
if ($versionInfo -ne "KeePassDicewareMinQuality:$version") {
    throw ".meta\version.info has '$versionInfo' but KeePassDicewareMinQuality.csproj VersionPrefix is '$version'."
}

$ciBuild = if ($env:GITHUB_ACTIONS -eq 'true') { 'true' } else { 'false' }
Invoke-Checked { dotnet test (Join-Path $root 'KeePassDicewareMinQuality.sln') -c Release -nologo "-p:KeePassPath=$KeePassPath" "-p:DicewareDll=$dicewareDll" "-p:ContinuousIntegrationBuild=$ciBuild" }

New-Item -ItemType Directory -Force $artifacts | Out-Null
$dll = Join-Path $artifacts 'KeePassDicewareMinQuality.dll'
Copy-Item (Join-Path $root 'bin\Release\net452\KeePassDicewareMinQuality.dll') $dll -Force
$hash = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content (Join-Path $artifacts 'KeePassDicewareMinQuality.dll.sha256') "$hash  KeePassDicewareMinQuality.dll" -Encoding ascii

Write-Host "KeePassDicewareMinQuality $version -> $dll"
Write-Host "SHA-256 $hash"
