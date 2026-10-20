param([string]$ProxyUrl)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$lock = Get-Content -LiteralPath (Join-Path $repoRoot 'config/toolchain.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$target = Join-Path $repoRoot $lock.install_root
$exe = Join-Path $target 'dotnet.exe'
if (Test-Path -LiteralPath $exe) {
    $version = & $exe --version
    if ($LASTEXITCODE -ne 0 -or $version -ne $lock.sdk_version) { throw 'Existing SDK does not match toolchain lock.' }
    Write-Output "Project SDK already available: $version"
    exit 0
}
if ((Test-Path -LiteralPath $target) -and (Get-ChildItem -LiteralPath $target -Force | Select-Object -First 1)) { throw 'Partial SDK directory exists. Inspect it before retrying.' }
$downloads = Join-Path $repoRoot 'local/downloads'
New-Item -ItemType Directory -Path $downloads -Force | Out-Null
$archive = Join-Path $downloads "dotnet-sdk-$($lock.sdk_version).zip"
$downloadArgs = @('--fail', '--location', '--connect-timeout', '15', '--max-time', '900', '--output', $archive)
if ($ProxyUrl) { $downloadArgs += @('--proxy', $ProxyUrl) }
$downloadArgs += $lock.url
& curl.exe @downloadArgs
if ($LASTEXITCODE -ne 0) { throw "SDK download failed: $LASTEXITCODE" }
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash.ToLowerInvariant()
if ($digest -ne $lock.sha512) { throw 'SDK SHA512 mismatch; archive will not be extracted.' }
Expand-Archive -LiteralPath $archive -DestinationPath $target
& $exe --version
if ($LASTEXITCODE -ne 0) { throw 'SDK startup failed.' }
Write-Output 'Project SDK ready. System PATH and global tool configuration were not changed.'
