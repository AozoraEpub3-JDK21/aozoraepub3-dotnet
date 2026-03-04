<#
.SYNOPSIS
    narou.rb + Java AozoraEpub3 でリファレンス EPUB を生成するセットアップスクリプト。

.DESCRIPTION
    以下のテスト対象小説ごとに:
      1. narou download <url>  - narou が .txt + .epub を生成
      2. .txt / .epub を tests/integration/reference/<id>/ にコピー
      3. 青空文庫のみ: 直接 web から .zip をダウンロードし Java AozoraEpub3 で変換
    生成後、dotnet test で JavaComparisonTests が実行可能になる。

.PARAMETER Force
    既にリファレンスが存在する場合でも再ダウンロード・再変換する。

.EXAMPLE
    pwsh -File tests\integration\generate-reference-epubs.ps1
    pwsh -File tests\integration\generate-reference-epubs.ps1 -Force
#>
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── パス設定 ──────────────────────────────────────────────────────────────────
$RepoRoot      = (Resolve-Path "$PSScriptRoot\..\..").Path
$ReferenceDir  = Join-Path $PSScriptRoot "reference"
$NarouWorkDir  = "D:\MyNovel"
$NarouJarDir   = "D:\MyNovel\AozoraEpub3-1.2.0-jdk21"
$NarouJar      = Join-Path $NarouJarDir "AozoraEpub3.jar"
$DotNetProject = Join-Path $RepoRoot "src\AozoraEpub3.Cli"

# narou が小説を保存するルートディレクトリ
$NarouDataSyosetu  = "D:\MyNovel\小説データ\小説家になろう"
$NarouDataKakuyomu = "D:\MyNovel\小説データ\カクヨム"

# Java 呼び出し共通 JVM オプション
$JavaVmArgs = @(
    "-Dfile.encoding=UTF-8",
    "-Dstdout.encoding=UTF-8",
    "-Dstderr.encoding=UTF-8",
    "-Dsun.stdout.encoding=UTF-8",
    "-Dsun.stderr.encoding=UTF-8"
)

# ── テスト対象小説一覧 ────────────────────────────────────────────────────────
$TestNovels = @(
    [PSCustomObject]@{
        Id       = "n8005ls"
        Url      = "https://ncode.syosetu.com/n8005ls/"
        Type     = "syosetu"
        Encoding = "UTF-8"
    },
    [PSCustomObject]@{
        Id       = "n0063lr"
        Url      = "https://ncode.syosetu.com/n0063lr/"
        Type     = "syosetu"
        Encoding = "UTF-8"
    },
    [PSCustomObject]@{
        Id       = "n9623lp"
        Url      = "https://ncode.syosetu.com/n9623lp/"
        Type     = "syosetu"
        Encoding = "UTF-8"
    },
    [PSCustomObject]@{
        Id       = "kakuyomu_822139840468926025"
        Url      = "https://kakuyomu.jp/works/822139840468926025"
        Type     = "kakuyomu"
        Encoding = "UTF-8"
    },
    [PSCustomObject]@{
        Id       = "aozora_1567_14913"
        Url      = "https://www.aozora.gr.jp/cards/000035/files/1567_14913.html"
        Type     = "aozora"
        Encoding = "MS932"
    }
)

# ── ヘルパー関数 ─────────────────────────────────────────────────────────────

function Write-Step([string]$Msg) {
    Write-Host "`n=== $Msg ===" -ForegroundColor Cyan
}

function Write-Ok([string]$Msg) {
    Write-Host "  [OK] $Msg" -ForegroundColor Green
}

function Write-Warn([string]$Msg) {
    Write-Host "  [WARN] $Msg" -ForegroundColor Yellow
}

# narou が生成した小説フォルダ (ncode または Kakuyomu ID で前方一致検索)
function Find-NarouNovelDir([string]$DataDir, [string]$Id) {
    Get-ChildItem -LiteralPath $DataDir -Directory |
        Where-Object { $_.Name -like "$Id *" -or $_.Name -eq $Id } |
        Select-Object -First 1
}

# narou データフォルダから .txt ファイルを探す（replace.txt, 調査ログ.txt を除く）
function Find-NarouTxtFile([string]$NovelDir) {
    Get-ChildItem -LiteralPath $NovelDir -Filter "*.txt" |
        Where-Object { $_.Name -notlike "replace*" -and $_.Name -notlike "*調査*" -and $_.Name -notlike "converter*" } |
        Select-Object -First 1
}

# narou データフォルダから .epub ファイルを探す
function Find-NarouEpubFile([string]$NovelDir) {
    Get-ChildItem -LiteralPath $NovelDir -Filter "*.epub" |
        Select-Object -First 1
}

# Java AozoraEpub3 で変換してリファレンス EPUB を生成
function Invoke-JavaAozoraEpub3([string]$TxtPath, [string]$DstDir, [string]$InputEncoding) {
    $coverFile = Get-ChildItem -LiteralPath (Split-Path $TxtPath) -File |
        Where-Object { $_.Name -match "^cover\.(jpg|png|jpeg)$" } |
        Select-Object -First 1
    $coverOpt = if ($coverFile) { @("-c", "0") } else { @() }

    $encOpt = @("-enc", $InputEncoding)
    $dstOpt = if ($DstDir) { @("-dst", $DstDir) } else { @() }

    $allArgs = $JavaVmArgs + @("-cp", "AozoraEpub3.jar", "AozoraEpub3", "-enc", $InputEncoding) +
               $coverOpt + $dstOpt + @("-of", "`"$TxtPath`"")

    Push-Location $NarouJarDir
    try {
        Write-Host "  java $($allArgs -join ' ')" -ForegroundColor DarkGray
        & java @allArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Java AozoraEpub3 が非ゼロ終了: $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

# .NET CLI で変換
function Invoke-DotNetAozoraEpub3([string]$TxtPath, [string]$DstDir, [string]$InputEncoding) {
    $coverFile = Get-ChildItem -LiteralPath (Split-Path $TxtPath) -File |
        Where-Object { $_.Name -match "^cover\.(jpg|png|jpeg)$" } |
        Select-Object -First 1
    $coverOpt = if ($coverFile) { @("-c", "0") } else { @() }

    $encArg = if ($InputEncoding -eq "UTF-8") { "UTF-8" } else { "MS932" }

    $cliArgs = @("run", "--project", $DotNetProject, "--") +
               @("-enc", $encArg, "-of", "-d", $DstDir) +
               $coverOpt +
               @($TxtPath)

    Write-Host "  dotnet $($cliArgs -join ' ')" -ForegroundColor DarkGray
    & dotnet @cliArgs
    if ($LASTEXITCODE -ne 0) {
        throw ".NET CLI が非ゼロ終了: $LASTEXITCODE"
    }
}

# 青空文庫 HTML ページからテキスト zip URL を抽出してダウンロード
function Get-AozoraTextFile([string]$HtmlUrl, [string]$DestDir) {
    Write-Host "  HTML 取得中: $HtmlUrl" -ForegroundColor DarkGray
    $html = (Invoke-WebRequest -Uri $HtmlUrl -UseBasicParsing).Content

    # <a href="...NN_NNNNN.zip"> 形式のリンクを検索
    $m = [regex]::Match($html, 'href="([^"]*\d+_\d+\.zip)"')
    if (-not $m.Success) {
        # zip がなければ .txt を試す
        $m = [regex]::Match($html, 'href="([^"]*\d+_\d+\.txt)"')
    }
    if (-not $m.Success) {
        throw "青空文庫ページからテキストファイルリンクが見つかりません: $HtmlUrl"
    }

    $relLink = $m.Groups[1].Value
    $base = $HtmlUrl -replace '/[^/]+$', ''
    $fileUrl = if ($relLink -match '^https?://') { $relLink } else { "$base/$relLink" }

    $fileName = Split-Path $fileUrl -Leaf
    $destPath = Join-Path $DestDir $fileName

    Write-Host "  ダウンロード: $fileUrl → $destPath" -ForegroundColor DarkGray
    Invoke-WebRequest -Uri $fileUrl -OutFile $destPath -UseBasicParsing
    return $destPath
}

# ── メイン処理 ───────────────────────────────────────────────────────────────

Write-Host "リファレンス EPUB 生成スクリプト" -ForegroundColor White
Write-Host "リファレンス保存先: $ReferenceDir"
New-Item -ItemType Directory -Force -Path $ReferenceDir | Out-Null

foreach ($novel in $TestNovels) {
    Write-Step "$($novel.Id) [$($novel.Type)]"

    $refDir = Join-Path $ReferenceDir $novel.Id

    # リファレンスが既に存在する場合はスキップ（-Force で強制再生成）
    $refEpub = Join-Path $refDir "reference.epub"
    $refTxt  = Join-Path $refDir "input.txt"
    if (-not $Force -and (Test-Path $refEpub) -and (Test-Path $refTxt)) {
        Write-Warn "既存のリファレンスをスキップ (再生成するには -Force オプション)"
        continue
    }

    New-Item -ItemType Directory -Force -Path $refDir | Out-Null

    switch ($novel.Type) {

        { $_ -in "syosetu", "kakuyomu" } {
            # ── narou で download（.txt + .epub を生成）────────────────────────
            $dataDir = if ($novel.Type -eq "syosetu") { $NarouDataSyosetu } else { $NarouDataKakuyomu }

            # narou ID（kakuyomu は URL の末尾 ID）
            $narouId = if ($novel.Id -like "kakuyomu_*") {
                $novel.Id -replace '^kakuyomu_', ''
            } else {
                $novel.Id
            }

            $existingDir = Find-NarouNovelDir -DataDir $dataDir -Id $narouId
            $needDownload = $Force -or (-not $existingDir)

            if ($needDownload) {
                Write-Host "  narou download $($novel.Url) ..." -ForegroundColor DarkGray
                Push-Location $NarouWorkDir
                try {
                    if ($Force -and $existingDir) {
                        & narou download $novel.Url --force 2>&1
                    } else {
                        & narou download $novel.Url 2>&1
                    }
                    if ($LASTEXITCODE -ne 0) {
                        throw "narou download が失敗: $LASTEXITCODE"
                    }
                } finally {
                    Pop-Location
                }
                $existingDir = Find-NarouNovelDir -DataDir $dataDir -Id $narouId
            } else {
                Write-Ok "既ダウンロード済み: $($existingDir.FullName)"

                # .epub が古い場合は narou convert で再変換
                $existingEpub = Find-NarouEpubFile $existingDir.FullName
                if (-not $existingEpub) {
                    Write-Host "  narou convert $narouId (epub なし) ..." -ForegroundColor DarkGray
                    Push-Location $NarouWorkDir
                    try {
                        & narou convert $narouId 2>&1
                    } finally {
                        Pop-Location
                    }
                }
            }

            if (-not $existingDir) {
                throw "ダウンロード後も小説フォルダが見つかりません: $narouId"
            }

            $txtFile  = Find-NarouTxtFile $existingDir.FullName
            $epubFile = Find-NarouEpubFile $existingDir.FullName

            if (-not $txtFile)  { throw "txt ファイルが見つかりません: $($existingDir.FullName)" }
            if (-not $epubFile) { throw "epub ファイルが見つかりません: $($existingDir.FullName)" }

            Write-Ok "txt : $($txtFile.Name)"
            Write-Ok "epub: $($epubFile.Name)"

            Copy-Item -LiteralPath $txtFile.FullName  -Destination $refTxt  -Force
            Copy-Item -LiteralPath $epubFile.FullName -Destination $refEpub -Force
        }

        "aozora" {
            # ── 青空文庫: HTML → zip DL → Java AozoraEpub3 ───────────────────
            $tmpDir = Join-Path $refDir "tmp_aozora"
            New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

            $downloadedFile = Get-AozoraTextFile -HtmlUrl $novel.Url -DestDir $tmpDir

            # zip の場合は内部 txt を探してコピー（zip のまま渡せるが、input として txt も保存）
            if ($downloadedFile -like "*.zip") {
                Add-Type -AssemblyName System.IO.Compression.FileSystem
                $zip = [System.IO.Compression.ZipFile]::OpenRead($downloadedFile)
                try {
                    $txtEntry = $zip.Entries | Where-Object { $_.Name -like "*.txt" } | Select-Object -First 1
                    if ($txtEntry) {
                        $tmpTxtPath = Join-Path $tmpDir $txtEntry.Name
                        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($txtEntry, $tmpTxtPath, $true)
                        Copy-Item -LiteralPath $tmpTxtPath -Destination $refTxt -Force
                    }
                } finally {
                    $zip.Dispose()
                }
                $javaInputFile = $downloadedFile  # zip のまま Java に渡す
            } else {
                Copy-Item -LiteralPath $downloadedFile -Destination $refTxt -Force
                $javaInputFile = $downloadedFile
            }

            # Java AozoraEpub3 で変換 → reference.epub 生成
            Invoke-JavaAozoraEpub3 -TxtPath $javaInputFile -DstDir $refDir -InputEncoding $novel.Encoding

            # 生成された epub を reference.epub にリネーム
            $generated = Get-ChildItem -LiteralPath $refDir -Filter "*.epub" |
                         Where-Object { $_.Name -ne "reference.epub" } |
                         Select-Object -First 1
            if ($generated) {
                Move-Item -LiteralPath $generated.FullName -Destination $refEpub -Force
            }

            # 一時フォルダ削除
            Remove-Item -LiteralPath $tmpDir -Recurse -Force
        }
    }

    Write-Ok "リファレンス保存完了: $refDir"
}

Write-Host "`n完了！ dotnet test で JavaComparisonTests を実行できます。" -ForegroundColor Green
