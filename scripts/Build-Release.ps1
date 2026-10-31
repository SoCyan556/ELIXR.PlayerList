param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.4',
    [string]$IsccPath,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path $PSScriptRoot -Parent
Push-Location $releaseRoot
try {
    if (-not $IsccPath) {
        $IsccPath = @(
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
        ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if (-not $IsccPath) { throw 'Install Inno Setup 6.3+ or provide -IsccPath.' }
    & npm ci --prefix frontend --no-audit --no-fund
    if ($LASTEXITCODE) { throw 'npm ci failed.' }
    if (-not $SkipTests) {
        & dotnet test tests/ELIXR.PlayerList.Core.Tests -c Release --nologo
        if ($LASTEXITCODE) { throw '.NET tests failed.' }
        & npm test --prefix frontend
        if ($LASTEXITCODE) { throw 'Frontend tests failed.' }
    }
    $publish = Join-Path $releaseRoot 'artifacts\publish-win-x64'
    if (Test-Path -LiteralPath $publish) { throw 'artifacts/publish-win-x64 already exists. Move the previous build aside before creating a clean release.' }
    & dotnet publish src/ELIXR.PlayerList.Desktop -c Release -r win-x64 --self-contained true -p:Version=$Version -o $publish --nologo
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    Copy-Item -LiteralPath LICENSE,THIRD-PARTY-NOTICES.md,PRIVACY.md -Destination $publish
    & (Join-Path $PSScriptRoot 'Collect-Licenses.ps1') -Destination (Join-Path $publish 'ThirdPartyLicenses')
    $dependencies = Join-Path $releaseRoot 'artifacts\dependencies'
    New-Item -ItemType Directory -Path $dependencies -Force | Out-Null
    $runtimeInstaller = Join-Path $dependencies 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'
    if (-not (Test-Path -LiteralPath $runtimeInstaller)) {
        # Microsoft's full x64 Evergreen payload, NOT the small online bootstrapper.
        Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124701' -OutFile $runtimeInstaller
    }
    if ((Get-Item -LiteralPath $runtimeInstaller).Length -lt 50MB) { throw 'Expected the full offline WebView2 installer, not a bootstrapper.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $runtimeInstaller
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'The offline WebView2 installer must have a valid Microsoft Authenticode signature.'
    }
    & $IsccPath /Q "/DAppVersion=$Version" installer/ELIXR.PlayerList.iss
    if ($LASTEXITCODE) { throw 'Installer build failed.' }
    $release = Join-Path $releaseRoot 'artifacts\release'
    Compress-Archive -Path "$publish\*" -DestinationPath "$release\ELIXR-PlayerList-$Version-Portable-x64.zip" -Force
    Write-Host "Installer and portable package: $release"
    Write-Host 'After committing the audited source, run Package-Source.ps1, then publish the three artifacts and SHA256SUMS.txt to a GitHub release.'
} finally { Pop-Location }
