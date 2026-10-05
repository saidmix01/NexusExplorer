<#
.SYNOPSIS
  Installs the locally built MSIX and runs the Windows App Certification Kit (WACK) on it.

.DESCRIPTION
  Run this from an ADMINISTRATOR PowerShell after building with:
      ./packaging/build-msix.ps1 -Architectures x64 -SignForTesting

  It will:
    1. Trust the test certificate (LocalMachine\TrustedPeople) so Windows accepts the package.
    2. Install the x64 package and launch the app so you can check it by hand.
    3. Run the WACK certification tests and print the result + report path.

  This only validates the package. The .msixbundle you upload to Partner Center must be the
  UNSIGNED build (Store signs it): rebuild without -SignForTesting before submitting.
#>
[CmdletBinding()]
param(
    [string] $Package,
    [switch] $SkipInstall,
    [switch] $SkipWack
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo 'artifacts\msix'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run this script from an administrator PowerShell.' }

if (-not $Package) {
    $Package = Get-ChildItem $out -Filter '*_x64.msix' | Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Package -or -not (Test-Path $Package)) {
    throw "No package found. Build first: ./packaging/build-msix.ps1 -Architectures x64 -SignForTesting"
}
Write-Host "Package: $Package" -ForegroundColor Cyan

# --- 1. Install ---------------------------------------------------------------
if (-not $SkipInstall) {
    $sig = Get-AuthenticodeSignature $Package
    if ($sig.Status -eq 'NotSigned') {
        throw 'The package is unsigned. Rebuild with -SignForTesting to install it locally.'
    }
    $cer = Join-Path $out 'NexusExplorer-test.cer'
    if (Test-Path $cer) {
        Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
        Write-Host 'Test certificate trusted.'
    }

    # Remove a previous install of the same package family first.
    $name = ([xml](& {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($Package)
        try { (New-Object IO.StreamReader(($zip.Entries | Where-Object FullName -eq 'AppxManifest.xml').Open())).ReadToEnd() }
        finally { $zip.Dispose() }
    })).Package.Identity.Name
    Get-AppxPackage -Name $name | Remove-AppxPackage -ErrorAction SilentlyContinue

    Add-AppxPackage -Path $Package
    $installed = Get-AppxPackage -Name $name
    Write-Host "Installed $($installed.PackageFullName)" -ForegroundColor Green

    # Launch through the shell like the Start menu does.
    Start-Process "shell:AppsFolder\$($installed.PackageFamilyName)!NexusExplorer"
    Write-Host 'App launched. Check the Start menu tile, taskbar icon and that the app works.'
}

# --- 2. Windows App Certification Kit ----------------------------------------------
if (-not $SkipWack) {
    $appcert = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\App Certification Kit\appcert.exe'
    if (-not (Test-Path $appcert)) { throw "WACK not found at $appcert (install the Windows SDK)." }

    $report = Join-Path $out 'wack-report.xml'
    Remove-Item $report -ErrorAction SilentlyContinue
    Write-Host "`nRunning WACK (takes several minutes, the app will open and close)..." -ForegroundColor Cyan
    & $appcert reset | Out-Null
    & $appcert test -appxpackagepath $Package -reportoutputpath $report

    if (Test-Path $report) {
        [xml]$xml = Get-Content $report
        $overall = $xml.REPORT.OVERALL_RESULT
        $color = if ($overall -eq 'PASS') { 'Green' } else { 'Yellow' }
        Write-Host "`nWACK overall result: $overall" -ForegroundColor $color
        foreach ($test in $xml.SelectNodes('//TEST')) {
            $result = $test.SelectSingleNode('RESULT').InnerText.Trim()
            if ($result -ne 'PASS') { Write-Host ("  [{0}] {1}" -f $result, $test.NAME) }
        }
        Write-Host "Full report: $report"
    }
}
