<#
.SYNOPSIS
    AozoraEpub3 MSI インストーラーをビルドします。
.DESCRIPTION
    1. dotnet publish で GUI アプリを発行
    2. AppFiles.wxs を自動生成
    3. wix build で MSI を作成
.PARAMETER Version
    バージョン番号 (例: 0.0.1)
.PARAMETER Configuration
    ビルド構成 (Release / Debug)
.PARAMETER SkipPublish
    dotnet publish をスキップして既存の publish/ を使用
.PARAMETER WixExe
    wix コマンドのパス (デフォルト: PATH から検索)
.EXAMPLE
    ./installer/build.ps1 -Version 0.0.1
    ./installer/build.ps1 -Version 0.0.1 -SkipPublish
#>
param(
    [string]$Version       = "0.0.1",
    [string]$Configuration = "Release",
    [switch]$SkipPublish,
    [string]$WixExe        = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot    = (Resolve-Path "$PSScriptRoot/..").Path
$publishDir  = "$repoRoot/publish"
$installerDir = $PSScriptRoot
$msiName     = "AozoraEpub3-$Version-win-x64.msi"
$msiOut      = "$repoRoot/$msiName"

# ── 1. wix 実行ファイルを特定 ──────────────────────────────────────────────
if ($WixExe -eq "") {
    if (Get-Command wix -ErrorAction SilentlyContinue) {
        $WixExe = "wix"
    }
    else {
        # NuGet キャッシュから探す
        $cached = Get-ChildItem "$env:USERPROFILE\.nuget\packages\wixtoolset.sdk" `
            -Recurse -Filter "wix.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "x64" } |
            Select-Object -First 1
        if ($cached) {
            $WixExe = $cached.FullName
        }
        else {
            Write-Error "wix コマンドが見つかりません。`ndotnet tool install -g wix でインストールしてください。"
            exit 1
        }
    }
}
Write-Host "wix: $WixExe"
& $WixExe --version

# ── 2. WiX 拡張をグローバルキャッシュに追加 ───────────────────────────────
Write-Host "`n[1/4] WiX 拡張を確認中..."
& $WixExe extension add WixToolset.UI.wixext/5.0.2   --global 2>$null
& $WixExe extension add WixToolset.Util.wixext/5.0.2 --global 2>$null

# ── 3. dotnet publish ──────────────────────────────────────────────────────
if (-not $SkipPublish) {
    Write-Host "`n[2/4] dotnet publish ($Configuration, win-x64, self-contained)..."
    & dotnet publish "$repoRoot/src/AozoraEpub3.Gui" `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:Version=$Version `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} else {
    Write-Host "`n[2/4] dotnet publish をスキップ (既存の publish/ を使用)"
}

# ── 4. AppFiles.wxs を生成 ────────────────────────────────────────────────
Write-Host "`n[3/4] AppFiles.wxs を生成中..."
& "$installerDir/generate-appfiles.ps1" -PublishDir $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# ── 5. MSI をビルド ────────────────────────────────────────────────────────
Write-Host "`n[4/4] MSI をビルド中..."
& $WixExe build `
    "$installerDir/Package.wxs" `
    "$installerDir/AppFiles.wxs" `
    -d "Version=$Version" `
    -ext WixToolset.UI.wixext `
    -arch x64 `
    -b "$installerDir/" `
    -o $msiOut
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n✅ MSI ビルド完了: $msiOut"
