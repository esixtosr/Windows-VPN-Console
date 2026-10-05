param([string]$Version = 'v0.1.0', [string]$OutputDirectory = 'artifacts')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Version must be a semantic version prefixed v.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$publish = Join-Path $outputRoot 'publish'
$packageVersion = $Version.Substring(1)
New-Item -ItemType Directory -Force $publish | Out-Null
dotnet publish src/CNIT455.VPN.App/CNIT455.VPN.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:Version=$packageVersion" -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
New-Item -ItemType Directory -Force (Join-Path $publish 'Docs'),(Join-Path $publish 'Presets') | Out-Null
Copy-Item docs/* (Join-Path $publish 'Docs') -Recurse -Force
Copy-Item Presets/* (Join-Path $publish 'Presets') -Recurse -Force
Copy-Item README.md (Join-Path $publish 'README.txt') -Force
Copy-Item LICENSE,THIRD_PARTY_NOTICES.md,CHANGELOG.md $publish -Force
$noticeDirectory = Join-Path $publish 'Docs/RuntimeNotices'
New-Item -ItemType Directory -Force $noticeDirectory | Out-Null
Copy-Item licenses/* $noticeDirectory -Force
$packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
foreach ($package in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
    $folder = Join-Path $packageRoot $package
    if (Test-Path $folder) {
        Get-ChildItem $folder -Recurse -File | Where-Object { $_.Name -match '(?i)license|third.party.notices' } | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $noticeDirectory ($package + '-' + $_.Directory.Name + '-' + $_.Name)) -Force
        }
    }
}
$zip = Join-Path $outputRoot "CNIT455-VPN-Console-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding ascii
Write-Output $zip
