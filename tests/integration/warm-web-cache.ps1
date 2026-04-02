param(
    [string]$WorkDir = "D:\git\aozoraepub3-dotnet\\_compare\\cache_warm",
    [string]$UrlsFile = "D:\git\aozoraepub3-dotnet\\tests\\integration\\web-sample-urls.txt",
    [string]$CacheDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $UrlsFile)) {
    throw "URL一覧ファイルが見つかりません: $UrlsFile"
}

if (-not (Test-Path $WorkDir)) {
    New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
}

$urls = Get-Content -Path $UrlsFile -Encoding UTF8 |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith("#") } |
    Select-Object -Unique

if ($urls.Count -eq 0) {
    throw "有効なURLがありません: $UrlsFile"
}

$ok = 0
$ng = 0
foreach ($url in $urls) {
    Write-Host "[Warm] $url"

    $args = @("run", "--project", "src/AozoraEpub3.Cli", "--",
        "--url", $url,
        "--web-config", "web",
        "-d", $WorkDir,
        "--web-cache-mode", "on")

    if (-not [string]::IsNullOrWhiteSpace($CacheDir)) {
        $args += @("--web-cache-dir", $CacheDir)
    }

    & dotnet @args | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $ok++
    }
    else {
        $ng++
        Write-Warning "変換失敗: $url (exit=$LASTEXITCODE)"
    }
}

Write-Host "warm complete: success=$ok fail=$ng"
if ($ng -gt 0) {
    exit 1
}
