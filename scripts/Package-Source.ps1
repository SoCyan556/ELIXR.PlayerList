param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.4')
$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path $PSScriptRoot -Parent
Push-Location $releaseRoot
try {
    & git diff --quiet HEAD
    if ($LASTEXITCODE) { throw 'Commit all release changes before packaging source.' }
    $untracked = & git ls-files --others --exclude-standard
    if ($untracked) { throw 'Review and commit untracked files before packaging source.' }
    $release = Join-Path $releaseRoot 'artifacts\release'
    New-Item -ItemType Directory -Path $release -Force | Out-Null
    & git archive --format=zip "--prefix=ELIXR.PlayerList-$Version/" "--output=$release\ELIXR-PlayerList-$Version-Source.zip" HEAD
    if ($LASTEXITCODE) { throw 'Source archive failed.' }
    $files = @("ELIXR-PlayerList-$Version-Setup-x64.exe", "ELIXR-PlayerList-$Version-Portable-x64.zip", "ELIXR-PlayerList-$Version-Source.zip")
    $lines = foreach ($name in $files) { $hash = Get-FileHash -LiteralPath (Join-Path $release $name) -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $name" }
    [IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'), $lines, [Text.UTF8Encoding]::new($false))
} finally { Pop-Location }
