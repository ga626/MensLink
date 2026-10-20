$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetPath = Join-Path $repoRoot 'local/toolchains/dotnet/dotnet.exe'
$cli = Join-Path $repoRoot 'apps/MensLink.Cli/bin/Debug/net10.0/menslink.dll'
$demoRoot = Join-Path $repoRoot ('local/demo/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
New-Item -ItemType Directory -Path $demoRoot -Force | Out-Null
$dataRoot = Join-Path $demoRoot 'data'
$fixtureRoot = Join-Path $repoRoot 'tests/fixtures'
$fixture = Join-Path $fixtureRoot 'knowledge.md'
function Invoke-CliJson([string[]]$Parameters) {
    $raw = & $dotnetPath $cli @Parameters
    if ($LASTEXITCODE -ne 0) { throw "CLI failed: $LASTEXITCODE $raw" }
    return ($raw | ConvertFrom-Json)
}
$library = Invoke-CliJson -Parameters @('init', '--data-root', $dataRoot)
$preview = Invoke-CliJson -Parameters @('preview', '--data-root', $dataRoot, '--root', $fixtureRoot, '--file', $fixture)
$manifest = Join-Path $demoRoot 'preview.json'
$preview | ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding UTF8
$import = Invoke-CliJson -Parameters @('import', '--data-root', $dataRoot, '--manifest', $manifest, '--allow-agent')
$search = Invoke-CliJson -Parameters @('search', '--data-root', $dataRoot, '--query', 'E_POLICY_403')
if ($search.hits.Count -ne 1) { throw 'Expected one evidence hit.' }
$hit = $search.hits[0]
$fetch = Invoke-CliJson -Parameters @('fetch', '--data-root', $dataRoot, '--source-id', $hit.source_id, '--revision-id', $hit.revision_id, '--chunk-id', $hit.chunk_id)
$receipt = [ordered]@{ schema = 'menslink-demo/v1'; result = 'passed'; library_id = $library.library_id; import = $import; search = $search; fetch = $fetch }
$receipt | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $demoRoot 'receipt.json') -Encoding UTF8
Write-Output "Demo passed: $demoRoot"
Write-Output "Library: $($library.library_id)"
Write-Output "Citation: $($hit.citation)"
Write-Output "Read host: scripts/dev.ps1 cli serve --data-root $dataRoot"
exit 0
