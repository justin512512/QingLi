[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DotNetPath,
    [switch]$PortableOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "publish\$Runtime"
$layoutDir = Join-Path $artifactsDir "msix-layout"
$recoveryPublishDir = Join-Path $artifactsDir "publish-recovery\$Runtime"

function Resolve-DotNet {
    param([string]$Candidate)

    if ($Candidate) {
        if (-not (Test-Path -LiteralPath $Candidate)) {
            throw "The requested dotnet executable was not found: $Candidate"
        }

        return (Resolve-Path -LiteralPath $Candidate).Path
    }

    $current = Get-Item -LiteralPath $repoRoot
    while ($null -ne $current) {
        $localDotNet = Join-Path $current.FullName ".dotnet\dotnet.exe"
        if (Test-Path -LiteralPath $localDotNet) {
            return (Resolve-Path -LiteralPath $localDotNet).Path
        }

        $current = $current.Parent
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "dotnet was not found. Install .NET 8 SDK or pass -DotNetPath."
}

function Get-ProjectVersion {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    [xml]$props = Get-Content -LiteralPath $propsPath
    $version = $props.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Could not read <Version> from Directory.Build.props."
    }

    return $version
}

function Find-MakeAppx {
    $command = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        return $null
    }

    $candidates = Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\makeappx\.exe$" } |
        Sort-Object FullName -Descending

    $candidate = $candidates | Select-Object -First 1 -ExpandProperty FullName
    return $candidate
}

function New-PackagePng {
    param(
        [string]$Path,
        [int]$Width,
        [int]$Height
    )

    Add-Type -AssemblyName PresentationCore
    Add-Type -AssemblyName WindowsBase

    $bitmap = New-Object System.Windows.Media.Imaging.WriteableBitmap $Width, $Height, 96, 96, ([System.Windows.Media.PixelFormats]::Bgra32), $null
    $stride = $Width * 4
    $pixels = New-Object byte[] ($stride * $Height)

    for ($i = 0; $i -lt $pixels.Length; $i += 4) {
        $pixels[$i] = 0x8F
        $pixels[$i + 1] = 0x6F
        $pixels[$i + 2] = 0x24
        $pixels[$i + 3] = 0xFF
    }

    $bitmap.WritePixels((New-Object System.Windows.Int32Rect 0, 0, $Width, $Height), $pixels, $stride, 0)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))

    $stream = [System.IO.File]::Create($Path)
    try {
        $encoder.Save($stream)
    }
    finally {
        $stream.Dispose()
    }
}

$dotnet = Resolve-DotNet -Candidate $DotNetPath
$version = Get-ProjectVersion
$portableZip = Join-Path $artifactsDir "QingLi-$version-$Runtime-portable.zip"
$msixPath = Join-Path $artifactsDir "QingLi-$version-x64.msix"

Write-Host "Using dotnet: $dotnet"
Write-Host "Version: $version"

Push-Location $repoRoot
try {
    & $dotnet test "QingLi.sln" -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    & $dotnet publish "src\QingLi.Windows\QingLi.Windows.csproj" -c $Configuration -r $Runtime --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (Test-Path -LiteralPath $recoveryPublishDir) {
        Remove-Item -LiteralPath $recoveryPublishDir -Recurse -Force
    }
    & $dotnet publish "src\QingLi.Recovery\QingLi.Recovery.csproj" -c $Configuration -r $Runtime --self-contained true -o $recoveryPublishDir
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    Copy-Item -Path (Join-Path $recoveryPublishDir "QingLi.Recovery*") -Destination $publishDir -Force

    $requiredFiles = @(
        "QingLi.Windows.exe",
        "QingLi.Recovery.exe",
        "Assets\History\history-today.zh-CN.json",
        "Assets\Holidays\cn-2026.json"
    )
    foreach ($requiredFile in $requiredFiles) {
        $requiredPath = Join-Path $publishDir $requiredFile
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Published output is missing required file: $requiredFile"
        }
    }

    if (Test-Path -LiteralPath $portableZip) {
        Remove-Item -LiteralPath $portableZip -Force
    }

    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZip -Force
    Write-Host "Portable artifact: $portableZip"

    if ($PortableOnly) {
        return
    }

    $makeAppx = Find-MakeAppx
    if (-not $makeAppx) {
        throw "MSIX packaging requires makeappx.exe, but it was not found. Install the Windows SDK and include 'MSIX Packaging Tools', then rerun scripts/package.ps1. The portable artifact was created at $portableZip."
    }

    if (Test-Path -LiteralPath $layoutDir) {
        Remove-Item -LiteralPath $layoutDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $layoutDir -Recurse -Force
    Copy-Item -LiteralPath "src\QingLi.Package\Package.appxmanifest" -Destination (Join-Path $layoutDir "AppxManifest.xml") -Force

    $assetDir = Join-Path $layoutDir "Assets"
    New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
    New-PackagePng -Path (Join-Path $assetDir "Square44x44Logo.png") -Width 44 -Height 44
    New-PackagePng -Path (Join-Path $assetDir "Square150x150Logo.png") -Width 150 -Height 150

    if (Test-Path -LiteralPath $msixPath) {
        Remove-Item -LiteralPath $msixPath -Force
    }

    & $makeAppx pack /d $layoutDir /p $msixPath /o
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host "MSIX artifact: $msixPath"
}
finally {
    Pop-Location
}
