<#
.SYNOPSIS
    発行ディレクトリを走査して WiX AppFiles.wxs を生成します。
.PARAMETER PublishDir
    dotnet publish の出力ディレクトリ
.PARAMETER OutFile
    生成する WXS ファイルのパス
#>
param(
    [Parameter(Mandatory)][string]$PublishDir,
    [string]$OutFile = ""
)
$ErrorActionPreference = 'Stop'
if ($OutFile -eq "") { $OutFile = Join-Path $PSScriptRoot "AppFiles.wxs" }

function Get-WixId([string]$prefix, [string]$path) {
    $clean = ($path -replace '[^a-zA-Z0-9]', '_' -replace '_{2,}', '_').Trim('_')
    $id = "${prefix}_${clean}"
    if ($id.Length -gt 72) {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($path)
        $hash = [System.Security.Cryptography.MD5]::Create().ComputeHash($bytes)
        $id = "${prefix}_" + ([System.BitConverter]::ToString($hash) -replace '-', '')
    }
    return $id
}

function Xml-Escape([string]$s) {
    $s = $s -replace '&', '&amp;'
    $s = $s -replace '"', '&quot;'
    $s = $s -replace '<', '&lt;'
    $s = $s -replace '>', '&gt;'
    return $s
}

# ディレクトリノードを再帰出力 ($script:lines に追加)
function Write-DirNodes([string]$parentRel, [string[]]$allDirs, [hashtable]$idMap, [int]$level) {
    $pad = "    " + ("  " * $level)
    $parentDepth = if ($parentRel -eq "") { 0 } else { ($parentRel -split '/').Count }
    $children = $allDirs | Where-Object {
        $p = ($_ -split '/')
        if ($parentRel -eq "") {
            $p.Count -eq 1
        } else {
            $p.Count -eq ($parentDepth + 1) -and
            (($p[0..($parentDepth - 1)]) -join '/') -eq $parentRel
        }
    }
    foreach ($c in @($children)) {
        $cid   = $idMap[$c]
        $cname = Xml-Escape (($c -split '/')[-1])
        $cDepth = ($c -split '/').Count
        $grandKids = $allDirs | Where-Object {
            $p = ($_ -split '/')
            $p.Count -eq ($cDepth + 1) -and (($p[0..($cDepth - 1)]) -join '/') -eq $c
        }
        if ($grandKids) {
            $script:lines.Add("${pad}<Directory Id=""$cid"" Name=""$cname"">")
            Write-DirNodes $c $allDirs $idMap ($level + 1)
            $script:lines.Add("${pad}</Directory>")
        } else {
            $script:lines.Add("${pad}<Directory Id=""$cid"" Name=""$cname"" />")
        }
    }
}

# ── メイン ────────────────────────────────────────────────────
$pubPath = (Resolve-Path $PublishDir).Path.TrimEnd('\', '/')
Write-Host "AppFiles.wxs 生成: $pubPath"

$files = @(Get-ChildItem -Path $pubPath -Recurse -File | Sort-Object FullName)
Write-Host "  ファイル数: $($files.Count)"

$subdirSet = [System.Collections.Generic.SortedSet[string]]::new()
foreach ($f in $files) {
    $relDir = ($f.DirectoryName.Substring($pubPath.Length).TrimStart('\', '/')) -replace '\\', '/'
    if ($relDir -ne "") {
        # この dir だけでなく全祖先パスも追加 (例: web/kakuyomu.jp -> "web" も追加)
        $parts = $relDir -split '/'
        for ($i = 1; $i -le $parts.Count; $i++) {
            $subdirSet.Add(($parts[0..($i - 1)] -join '/')) | Out-Null
        }
    }
}
$subdirs = @($subdirSet)

$dirMap = @{}
foreach ($d in $subdirs) { $dirMap[$d] = Get-WixId "dir" $d }

# XML 行リスト (関数から $script:lines で参照)
$script:lines = [System.Collections.Generic.List[string]]::new()
$script:lines.Add('<?xml version="1.0" encoding="utf-8"?>')
$script:lines.Add('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
$script:lines.Add('  <Fragment>')
$script:lines.Add('')

if ($subdirs.Count -gt 0) {
    $script:lines.Add('    <DirectoryRef Id="INSTALLFOLDER">')
    Write-DirNodes "" $subdirs $dirMap 1
    $script:lines.Add('    </DirectoryRef>')
    $script:lines.Add('')
}

$script:lines.Add('    <ComponentGroup Id="AppFiles">')
foreach ($f in $files) {
    $relPath = ($f.FullName.Substring($pubPath.Length).TrimStart('\', '/')) -replace '\\', '/'
    $relDir  = ($f.DirectoryName.Substring($pubPath.Length).TrimStart('\', '/')) -replace '\\', '/'
    $compId  = Get-WixId "c" $relPath
    $fileId  = Get-WixId "f" $relPath
    $dirId   = if ($relDir -eq "") { "INSTALLFOLDER" } else { $dirMap[$relDir] }
    $src     = Xml-Escape $f.FullName
    $script:lines.Add("      <Component Id=""$compId"" Directory=""$dirId"" Guid=""*"">")
    $script:lines.Add("        <File Id=""$fileId"" Source=""$src"" KeyPath=""yes"" />")
    $script:lines.Add("      </Component>")
}
$script:lines.Add('    </ComponentGroup>')
$script:lines.Add('')
$script:lines.Add('  </Fragment>')
$script:lines.Add('</Wix>')

$xml = $script:lines -join [System.Environment]::NewLine
[System.IO.File]::WriteAllText($OutFile, $xml, [System.Text.Encoding]::UTF8)
Write-Host "完了 -> $OutFile"
