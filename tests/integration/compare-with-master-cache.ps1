param(
    [string]$WorkDir = "D:\git\aozoraepub3-dotnet\\_compare\\cache_compare",
    [string]$Master66 = "D:\git\aozoraepub3-dotnet\\_compare\\master\\master_66.epub",
    [string]$Master68 = "D:\git\aozoraepub3-dotnet\\_compare\\master\\master_68.epub",
    [string]$Url66 = "https://ncode.syosetu.com/n8005ls/",
    [string]$Url68 = "https://kakuyomu.jp/works/822139840468926025",
    [string]$CacheDir = "",
    [switch]$Warmup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Read-Entries([string]$epubPath) {
    $dict = @{}
    $zip = [IO.Compression.ZipFile]::OpenRead($epubPath)
    try {
        foreach ($entry in $zip.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) { continue }
            $sr = New-Object IO.StreamReader($entry.Open(), [Text.Encoding]::UTF8)
            try { $txt = $sr.ReadToEnd() } finally { $sr.Dispose() }
            $dict[$entry.FullName] = $txt
        }
    }
    finally { $zip.Dispose() }
    return $dict
}

function Normalize-Text([string]$entryName, [string]$content) {
    $content = $content.Replace("`r`n", "`n").TrimEnd("`n")
    $name = $entryName.ToLowerInvariant()

    if ($name.EndsWith("package.opf")) {
        $content = [Regex]::Replace($content, "<dc:date>[^<]*</dc:date>", "<dc:date>NORMALIZED_DATE</dc:date>", "IgnoreCase")
        $content = [Regex]::Replace($content, '<meta\s+property="dcterms:modified">[^<]*</meta>', '<meta property="dcterms:modified">NORMALIZED_DATE</meta>', 'IgnoreCase')
        $content = [Regex]::Replace($content, '<dc:identifier[^>]*>urn:uuid:[0-9a-f\-]+</dc:identifier>', '<dc:identifier id="unique-id">urn:uuid:NORMALIZED_UUID</dc:identifier>', 'IgnoreCase')
        $content = [Regex]::Replace($content, 'property="dcterms:identifier">urn:uuid:[0-9a-f\-]+</meta>', 'property="dcterms:identifier">urn:uuid:NORMALIZED_UUID</meta>', 'IgnoreCase')
    }
    elseif ($name.EndsWith("toc.ncx")) {
        $content = [Regex]::Replace($content, '<meta\s+name="dtb:uid"\s+content="[^"]*"\s*/>', '<meta name="dtb:uid" content="NORMALIZED_UUID"/>', 'IgnoreCase')
    }

    if ($name.EndsWith(".xhtml")) {
        $content = $content.Replace('<span class="half_em_space"> </span>', '')
        $content = [Regex]::Replace($content, '<div class="(introduction|postscript)">', '', 'IgnoreCase')
        $content = $content.Replace('</div><div class="clear"></div>', '')
        $content = $content.Replace('<span class="fullsp"> </span>', [string][char]0x3000)
        $content = [Regex]::Replace($content, '<span class="tcy"><span><span class="tcy"><span>(.*?)</span></span></span></span>', '<span class="tcy"><span>$1</span></span>')
    }

    return $content
}

function Compare-EpubText([string]$master, [string]$test) {
    $ref = Read-Entries $master
    $cur = Read-Entries $test

    $keys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $ref.Keys | ForEach-Object { [void]$keys.Add($_) }
    $cur.Keys | ForEach-Object { [void]$keys.Add($_) }

    $skip = @("OPS/css/vertical_font.css")
    $total = 0
    $details = @()

    foreach ($k in ($keys | Sort-Object)) {
        if ($skip -contains $k) { continue }

        if (-not $ref.ContainsKey($k) -or -not $cur.ContainsKey($k)) {
            $total += 1
            $details += [pscustomobject]@{ Entry = $k; Diff = 1 }
            continue
        }

        $ext = [IO.Path]::GetExtension($k).ToLowerInvariant()
        if ($ext -in @(".xhtml", ".html", ".opf", ".ncx", ".css", ".xml")) {
            $r = (Normalize-Text $k $ref[$k]).Split("`n")
            $t = (Normalize-Text $k $cur[$k]).Split("`n")
            $max = [Math]::Max($r.Length, $t.Length)
            $d = 0
            for ($i = 0; $i -lt $max; $i++) {
                $rv = if ($i -lt $r.Length) { $r[$i] } else { "" }
                $tv = if ($i -lt $t.Length) { $t[$i] } else { "" }
                if ($rv -cne $tv) { $d++ }
            }
            if ($d -gt 0) {
                $total += $d
                $details += [pscustomobject]@{ Entry = $k; Diff = $d }
            }
        }
    }

    [pscustomobject]@{ Total = $total; Top = ($details | Sort-Object Diff -Descending | Select-Object -First 15) }
}

function Find-GeneratedEpub([string]$dir, [string]$contains) {
    $item = Get-ChildItem $dir -File -Filter "*.epub" | Where-Object { $_.Name -like "*$contains*" } | Select-Object -First 1
    if ($null -eq $item) { return $null }
    return $item.FullName
}

if (-not (Test-Path $WorkDir)) {
    New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
}

function Invoke-Convert([string]$url, [string]$mode) {
    $args = @("run", "--project", "src/AozoraEpub3.Cli", "--",
        "--url", $url,
        "--web-config", "web",
        "-d", $WorkDir,
        "--web-cache-mode", $mode)

    if (-not [string]::IsNullOrWhiteSpace($CacheDir)) {
        $args += @("--web-cache-dir", $CacheDir)
    }

    & dotnet @args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "変換失敗: url=$url mode=$mode exit=$LASTEXITCODE"
    }
}

if ($Warmup) {
    Invoke-Convert $Url66 "on"
    Invoke-Convert $Url68 "on"
}

Invoke-Convert $Url66 "only"
Invoke-Convert $Url68 "only"

$dot66 = Find-GeneratedEpub $WorkDir "第八王女"
$dot68 = Find-GeneratedEpub $WorkDir "チートなんて無い"

if ((-not $dot66) -or (-not $dot68)) {
    throw "EPUB 出力の検出に失敗しました。"
}

$r66 = Compare-EpubText $Master66 $dot66
$r68 = Compare-EpubText $Master68 $dot68

Write-Host "66 total(norm): $($r66.Total)"
$r66.Top | Format-Table -AutoSize
Write-Host "68 total(norm): $($r68.Total)"
$r68.Top | Format-Table -AutoSize
