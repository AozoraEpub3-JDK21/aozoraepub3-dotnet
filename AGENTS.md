# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## VISIONの一言
「読む人を作る人に変えるアプリ」（docs/VISION.md 参照）
 物事を判断するときの最重要基準としてください

## 設計の3層構造
- 層1: 読む/変換（URL→EPUB）
- 層2: 書く（初心者カード）
- 層3: 本にする（編集カード）

## 重要ドキュメント
- docs/VISION.md — ビジョン全文
- docs/feature-app-redesign-v2.md — 全体設計
- docs/feature-editor-phase7.md — E7詳細設計（最新）


## Project Overview

This is a .NET 10 C# port of the Java application [AozoraEpub3](https://github.com/hmdev/AozoraEpub3), which converts Aozora Bunko-format text files (and web novel pages) into EPUB3. The Java source reference is at `D:\git\AozoraEpub3\AozoraEpub3\`.

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Build release
dotnet build -c Release

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~AozoraEpub3ConverterTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~AozoraEpub3ConverterTests.Constructor_Succeeds_WithoutResourcePath"

# Run CLI
dotnet run --project src/AozoraEpub3.Cli -- [options] <files>

# Convert a URL (e.g., syosetu.com novel)
dotnet run --project src/AozoraEpub3.Cli -- --url https://ncode.syosetu.com/nXXXX/ -d ./output
```

## Architecture

### Processing Pipeline

**File conversion (2-pass):**
1. `ArchiveTextExtractor.GetTextInputStream()` — opens txt/zip/rar/cbz, returns `Stream`
2. `AozoraEpub3Converter.GetBookInfo()` — 1st pass: scans text to extract title, author, chapter structure → `BookInfo`
3. `Epub3Writer.Write()` — 2nd pass: calls back into `AozoraEpub3Converter.ConvertTextToEpub3()`, writes EPUB zip

**URL conversion:**
1. `WebAozoraConverter.ConvertToAozoraLinesAsync()` — fetches HTML pages with AngleSharp, produces `List<string>` in Aozora format
2. `AozoraTextFinalizer.Finalize()` — post-processes lines (detects foreword/afterword, promotes headings)
3. Same 2-pass flow as above, using `StringReader` over the joined text

### Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `AozoraEpub3Converter` | `Core/Converter/` | Core text→EPUB conversion; reads chuki (注記) annotations |
| `Epub3Writer` | `Core/Io/` | Writes EPUB3 zip using Scriban templates; implements `IEpub3Writer` |
| `WebAozoraConverter` | `Core/Web/` | Fetches web novel pages → Aozora-format text lines |
| `AozoraTextFinalizer` | `Core/Web/` | Post-processes web-fetched lines |
| `ArchiveTextExtractor` | `Core/Io/` | Extracts text from zip/rar archives (SharpCompress) |
| `ImageInfoReader` | `Core/Io/` | Reads image dimensions from archives |
| `BookInfo` | `Core/Info/` | Aggregates metadata: title, creator, chapters, cover settings |

### Templates (Scriban `.sbn`)

EPUB3 structure is generated from embedded Scriban templates in `Core/Resources/template/`:
- `OPS/package.sbn` → `content.opf`
- `OPS/toc.ncx.sbn` → `toc.ncx`
- `OPS/xhtml/xhtml_header.sbn` / `xhtml_footer.sbn` → chapter XHTML wrapper
- `OPS/xhtml/xhtml_nav.sbn` → `nav.xhtml` (EPUB3 navigation)
- `OPS/css/vertical_text.sbn` / `horizontal_text.sbn` → dynamic CSS

**Embedded resource naming:** `META-INF` folder becomes `META_INF` in assembly resource names (hyphens → underscores). Template constants use `.sbn` extension.

### Web Extraction Config (`web/` directory)

Per-site extraction rules live in `web/{FQDN}/extract.txt` (tab-separated). Supported keys: `TITLE`, `AUTHOR`, `DESCRIPTION`, `HREF`, `CONTENT_ARTICLE`, `CONTENT_SUBTITLE`, `CONTENT_CHAPTER`, `CONTENT_PREAMBLE`, `CONTENT_APPENDIX`, `PAGE_URL`, etc. Selector syntax: `.css-class:index` or `#id:index`. The `web/` directory is copied to the CLI output directory automatically.

### System.CommandLine 3.0 Preview API

The CLI uses a preview API with non-standard patterns:
- `new Option<T>(name, aliases[])` + property initializer (`DefaultValueFactory`, `Description`, `Arity`)
- `rootCommand.Add(option)` — not `AddOption`
- `rootCommand.SetAction((Action<ParseResult>)(pr => { ... }))` — explicit cast required
- `parseResult.GetValue(option)` for values
- Execute: `rootCommand.Parse(args).Invoke(null!)`

## Key Technical Notes

- **Encoding:** `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` is called in `Program.cs`; required for Shift-JIS (MS932) input files.
- **Scriban:** Accesses C# properties by their exact name. Use `MemberRenamer` to preserve casing. Methods cannot be called from templates — use computed properties instead.
- **EPUB mimetype:** Must be stored uncompressed (`CompressionLevel.NoCompression`) as the first entry in the zip.
- **SharpCompress:** Use `OpenArchive(FileInfo, ReaderOptions?)` — `Open()` does not exist.
- **`string.StartsWith` with offset:** Does not exist in C#; use `IndexOf(...) == offset` instead.
- **Regex possessive quantifiers:** Java `*+` → C# atomic group `(?>..*)`.
- **`goto` and `using var`:** `goto` cannot jump over `using var` declarations; extract into a separate method instead.
- **Async in CLI:** `Task.Run(...).GetAwaiter().GetResult()` for synchronous execution of async methods.
- **`ZipArchive` disposal:** Use `leaveOpen: true` when creating over a `FileStream`, and dispose `FileStream` separately in `finally`.

## Java Source Reference

When porting or debugging, the original Java source is at:
- `D:\git\AozoraEpub3\AozoraEpub3\src\com\github\hmdev\converter\AozoraEpub3Converter.java`
- `D:\git\AozoraEpub3\AozoraEpub3\src\com\github\hmdev\writer\Epub3Writer.java`
- `D:\git\AozoraEpub3\AozoraEpub3\src\com\github\hmdev\io\ArchiveTextExtractor.java`
- Original templates (Velocity `.vm`): `D:\git\AozoraEpub3\AozoraEpub3\template\` — ported to Scriban `.sbn`
