param(
    [ValidateSet('restore', 'build', 'test', 'mcp-test', 'demo', 'cli')][string]$Action = 'test',
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$CliArguments
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetPath = Join-Path $repoRoot 'local/toolchains/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) { throw 'Missing project SDK. Run scripts/bootstrap.ps1 first.' }
$saved = @{}
$settings = @{
    DOTNET_ROOT = (Split-Path -Parent $dotnetPath)
    DOTNET_CLI_HOME = (Join-Path $repoRoot 'local/dotnet-home')
    NUGET_PACKAGES = (Join-Path $repoRoot 'local/nuget')
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
}
foreach ($key in $settings.Keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process'); [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process') }
Push-Location $repoRoot
try {
    switch ($Action) {
        'restore' { & $dotnetPath restore MensLink.slnx --locked-mode; $code = $LASTEXITCODE }
        'build' { & $dotnetPath build MensLink.slnx --no-restore; $code = $LASTEXITCODE }
        'test' { & $dotnetPath run --no-build --project tests/MensLink.Checks; $code = $LASTEXITCODE }
        'mcp-test' { & $dotnetPath run --no-build --project tests/MensLink.McpChecks; $code = $LASTEXITCODE }
        'demo' { & (Join-Path $PSScriptRoot 'demo.ps1'); $code = $LASTEXITCODE }
        'cli' { & $dotnetPath (Join-Path $repoRoot 'apps/MensLink.Cli/bin/Debug/net10.0/menslink.dll') @CliArguments; $code = $LASTEXITCODE }
    }
    if ($code -ne 0) { throw "MensLink $Action failed with exit code $code" }
}
finally {
    Pop-Location
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process') }
}
