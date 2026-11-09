<#
Builds FramePlay and its installer (dist\FramePlay-Setup-<version>.exe).

    powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 [-Iscc <path to ISCC.exe>]

Needs the .NET 8 SDK and the two large files described in README > Build:
third_party\libmpv\libmpv-2.dll and assets\soundfonts\Classic_Station.sf2.
Inno Setup 6.7 comes from -Iscc, a standard install, or NuGet (fetched into tools\innosetup).
#>
param([string]$Iscc)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'dist\FramePlay'

foreach ($f in 'third_party\libmpv\libmpv-2.dll', 'assets\soundfonts\Classic_Station.sf2') {
    if (-not (Test-Path (Join-Path $root $f))) { throw "Missing $f (see README > Build)." }
}
[xml]$proj = Get-Content (Join-Path $root 'src\FramePlay\FramePlay.csproj')
$version = @($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]

# 1. Publish: framework-dependent (.NET 8 Desktop Runtime), ReadyToRun for a fast start.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root 'src\FramePlay\FramePlay.csproj') -c Release -r win-x64 --self-contained false `
    -p:PublishReadyToRun=true -o $publish
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

# 2. Find Inno Setup 6.7+.
if (-not $Iscc) {
    $Iscc = @(
        (Join-Path $root 'tools\innosetup\tools\ISCC.exe'),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc) {
    Write-Host 'Fetching Inno Setup 6.7.3 from NuGet...'
    $tools = Join-Path $root 'tools\innosetup'
    New-Item -ItemType Directory -Force $tools | Out-Null
    $pkg = Join-Path $tools 'innosetup.zip'
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/tools.innosetup/6.7.3/tools.innosetup.6.7.3.nupkg' -OutFile $pkg
    Expand-Archive $pkg -DestinationPath $tools -Force
    Remove-Item $pkg
    $Iscc = Join-Path $tools 'tools\ISCC.exe'
}

# 3. Compile the installer.
& $Iscc /Qp "/DSourceDir=$publish" "/DAppVersion=$version" (Join-Path $PSScriptRoot 'FramePlay.iss')
if ($LASTEXITCODE) { throw 'Inno Setup failed' }
Get-Item (Join-Path $root "dist\FramePlay-Setup-$version.exe")
