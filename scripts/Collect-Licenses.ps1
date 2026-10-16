param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$npmPaths = & npm ls --prefix (Join-Path $releaseRoot 'frontend') --omit=dev --all --parseable
if ($LASTEXITCODE) { throw 'Could not enumerate frontend runtime dependencies.' }
foreach ($packagePath in $npmPaths | Select-Object -Unique) {
    if ($packagePath -notmatch 'node_modules') { continue }
    $metadata = Get-Content -LiteralPath (Join-Path $packagePath 'package.json') -Raw | ConvertFrom-Json
    $label = ($metadata.name -replace '[^A-Za-z0-9.-]', '_') + '-' + $metadata.version
    $licenseFiles = Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE)' }
    if (-not $licenseFiles) { throw "Missing license text for $label" }
    foreach ($licenseFile in $licenseFiles) { Copy-Item -LiteralPath $licenseFile.FullName -Destination (Join-Path $Destination "$label-$($licenseFile.Name)") }
}
$assets = Get-Content -LiteralPath (Join-Path $releaseRoot 'src\ELIXR.PlayerList.Desktop\obj\project.assets.json') -Raw | ConvertFrom-Json
# Self-contained framework packs are downloadDependencies, not ordinary libraries.
foreach ($pack in $assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object { $_.name -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.(Runtime|Host)\.win-x64$' }) {
    $version = ($pack.version.Trim('[', ']') -split ',')[0].Trim()
    $directory = Join-Path $assets.project.restore.packagesPath ($pack.name.ToLowerInvariant() + '/' + $version)
    $licenseFiles = Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)' }
    if (-not $licenseFiles) { throw "Missing framework license for $($pack.name)" }
    foreach ($licenseFile in $licenseFiles) { Copy-Item -LiteralPath $licenseFile.FullName -Destination (Join-Path $Destination "$($pack.name)-$version-$($licenseFile.Name)") }
}
foreach ($package in $assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' }) {
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $directory = Join-Path $folder $package.Value.path
        if (-not (Test-Path -LiteralPath $directory)) { continue }
        $label = $package.Name -replace '[^A-Za-z0-9.-]', '_'
        $licenseFiles = Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD-PARTY-NOTICES|ThirdPartyNotices)' }
        if (-not $licenseFiles) { throw "Missing license text for $label" }
        $index = 0
        foreach ($licenseFile in $licenseFiles) { $index++; Copy-Item -LiteralPath $licenseFile.FullName -Destination (Join-Path $Destination "$label-$index-$($licenseFile.Name)") }
        break
    }
}
