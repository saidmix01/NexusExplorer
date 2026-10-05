<#
.SYNOPSIS
  Builds the Nexus File Manager MSIX package(s) for the Microsoft Store.

.DESCRIPTION
  1. Publishes the app self-contained for each architecture (no single-file, no trimming:
     the app relies on COM interop that trimming breaks).
  2. Stages a package layout: publish output + packaging/Assets + AppxManifest.xml
     (generated from packaging/Package.appxmanifest).
  3. Indexes the scaled/target-size assets into resources.pri (makepri).
  4. Packs one .msix per architecture (makeappx), and a .msixbundle when building several.
  5. Optionally signs with a local self-signed test certificate (for sideload testing only;
     packages uploaded to the Store must NOT be signed - the Store signs them).

.EXAMPLE
  # Local test build with a throwaway identity:
  ./packaging/build-msix.ps1

.EXAMPLE
  # Store build: use the exact values from Partner Center > Product identity.
  ./packaging/build-msix.ps1 -IdentityName "12345YourName.NexusExplorer" `
      -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplayName "Your Name"
#>
[CmdletBinding()]
param(
    [string[]] $Architectures = @('x64', 'arm64'),
    # Per-product: Partner Center > Nexus File Manager > Product identity > Package/Identity/Name.
    # PROVISIONAL guess — replace with the exact value Partner Center shows after reserving the name.
    [string]   $IdentityName = 'SAIDMIX.NexusFileManager',
    # Account-wide values (same for every app in the SAIDMIX developer account).
    [string]   $Publisher = 'CN=7F54C210-7D42-4E3E-AEB0-BA23C7B9DAAC',
    # Written with [char] so Windows PowerShell 5.1 reads the "ñ" correctly without a BOM.
    [string]   $PublisherDisplayName = "Said Andres Avenda$([char]0x00F1)o",
    # Four-part version. Defaults to <Version> in Directory.Build.props + ".0".
    [string]   $Version,
    # Sign with a self-signed cert whose subject equals -Publisher (sideload testing only).
    [switch]   $SignForTesting,
    [string]   $OutputDir
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$packaging = $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repo 'artifacts\msix' }
$work = Join-Path $repo 'artifacts\msix-work'

# --- Windows SDK tools -------------------------------------------------------
function Find-SdkTool([string] $name) {
    $root = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $hit = Get-ChildItem $root -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$name" } |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $hit) { throw "$name not found. Install the Windows 10/11 SDK." }
    return $hit
}
$makeappx = Find-SdkTool 'makeappx.exe'
$makepri = Find-SdkTool 'makepri.exe'
$signtool = Find-SdkTool 'signtool.exe'

function Invoke-Tool([string] $exe, [string[]] $toolArgs) {
    # The SDK tools print one line per file; only show their output when they fail.
    $log = & $exe @toolArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $log | Out-Host
        throw "$(Split-Path -Leaf $exe) failed with exit code $LASTEXITCODE"
    }
}

# --- Version -------------------------------------------------------------------
if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $repo 'Directory.Build.props')
    $v = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    $Version = "$v.0"
}
if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') {
    throw "Version '$Version' must have four parts and end in .0 (Store requirement), e.g. 1.0.0.0"
}

Write-Host "Nexus File Manager MSIX $Version  [$($Architectures -join ', ')]" -ForegroundColor Cyan
Write-Host "  Identity : $IdentityName"
Write-Host "  Publisher: $Publisher ($PublisherDisplayName)"

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $OutputDir, $work | Out-Null

$template = Get-Content (Join-Path $packaging 'Package.appxmanifest') -Raw
$msixFiles = @()

foreach ($arch in $Architectures) {
    Write-Host "`n=== $arch ===" -ForegroundColor Cyan
    $layout = Join-Path $work "$arch\layout"

    # 1. Publish (self-contained folder).
    Invoke-Tool 'dotnet' @(
        'publish', (Join-Path $repo 'src\NexusExplorer.App\NexusExplorer.App.csproj'),
        '-c', 'Release', '-r', "win-$arch", '--self-contained', 'true',
        '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:DebugType=none',
        '-o', $layout, '--nologo', '-v', 'quiet')

    # 2. Assets + manifest.
    Copy-Item (Join-Path $packaging 'Assets') (Join-Path $layout 'Assets') -Recurse -Force
    $manifest = $template.
        Replace('$IDENTITY_NAME$', $IdentityName).
        Replace('$PUBLISHER$', [Security.SecurityElement]::Escape($Publisher)).
        Replace('$PUBLISHER_NAME$', [Security.SecurityElement]::Escape($PublisherDisplayName)).
        Replace('$VERSION$', $Version).
        Replace('$ARCH$', $arch)
    [IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding($false)))

    # 3. resources.pri: maps "Assets\Square44x44Logo.png" to the scale/target-size variants.
    $priConfig = Join-Path $work "$arch\priconfig.xml"
    Invoke-Tool $makepri @('createconfig', '/cf', $priConfig, '/dq', 'en-US', '/pv', '10.0.0', '/o')
    # The default config splits scale/language variants into resources.scale-*.pri files meant
    # for separate resource packages. We ship one package per architecture, so keep every
    # variant in the main resources.pri (otherwise 125-400% tiles never resolve).
    [xml]$cfg = Get-Content $priConfig
    $cfg.SelectNodes('//packaging') | ForEach-Object { [void]$_.ParentNode.RemoveChild($_) }
    $cfg.Save($priConfig)
    Invoke-Tool $makepri @('new', '/pr', $layout, '/cf', $priConfig,
        '/mn', (Join-Path $layout 'AppxManifest.xml'), '/of', (Join-Path $layout 'resources.pri'), '/o')

    # 4. Pack.
    $msix = Join-Path $OutputDir "NexusExplorer_$($Version)_$arch.msix"
    Invoke-Tool $makeappx @('pack', '/d', $layout, '/p', $msix, '/o')
    $msixFiles += $msix
}

# --- Bundle (one upload for all architectures) -------------------------------
$artifacts = @($msixFiles)
if ($msixFiles.Count -gt 1) {
    $bundleDir = Join-Path $work 'bundle'
    New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
    $msixFiles | ForEach-Object { Copy-Item $_ $bundleDir }
    $bundle = Join-Path $OutputDir "NexusExplorer_$Version.msixbundle"
    Invoke-Tool $makeappx @('bundle', '/d', $bundleDir, '/p', $bundle, '/bv', $Version, '/o')
    $artifacts += $bundle
}

# --- Optional test signing -------------------------------------------------------
if ($SignForTesting) {
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Publisher -and $_.HasPrivateKey } |
        Select-Object -First 1
    if (-not $cert) {
        Write-Host "Creating self-signed test certificate '$Publisher'"
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
            -FriendlyName 'Nexus File Manager MSIX test signing' -CertStoreLocation Cert:\CurrentUser\My `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }
    $cer = Join-Path $OutputDir 'NexusExplorer-test.cer'
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
    foreach ($a in $artifacts) {
        Invoke-Tool $signtool @('sign', '/fd', 'SHA256', '/sha1', $cert.Thumbprint, $a)
    }
    Write-Host "`nSigned for testing. To install, trust the cert once (admin PowerShell):" -ForegroundColor Yellow
    Write-Host "  Import-Certificate -FilePath '$cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "`nOutput:" -ForegroundColor Green
$artifacts | ForEach-Object { Write-Host ("  {0}  ({1:N1} MB)" -f $_, ((Get-Item $_).Length / 1MB)) }
