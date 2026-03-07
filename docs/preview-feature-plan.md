# EPUB Preview / CSS Editor / epubcheck Integration - Implementation Plan

## Design Decisions

| # | Item | Decision |
|---|------|----------|
| Q1 | Platform | Windows only (WebView2) |
| Q2 | Preview placement | New "Preview" tab in sidebar |
| Q3 | DPFJ Guide | English PDF read + documented (docs/dpfj-epub3-guide-summary.md) |
| Q4 | Template preview target | CSS editing + preview |
| Q5 | CSS editor style | Beginner mode (form UI) / Advanced mode (text editor) toggle |
| Q6 | epubcheck integration | Button in preview screen + independent "Validate" tab |
| Q7 | Source mapping | Phase E1 (chapter-file map) -> E2 (line numbers) -> E3 (all elements) |
| Q8 | Page navigation | TOC panel + prev/next buttons |
| Q9 | Meta info storage | `.epub.meta.json` alongside the EPUB file |

## Implementation Phases

### Phase A: EPUB Specification Documentation (DONE)
- `docs/dpfj-epub3-guide-summary.md`

### Phase B: Preview Foundation
- **B1**: WebView2 integration (Avalonia + WebView2 setup, NuGet packages)
- **B2**: EPUB unpack -> XHTML display engine
- **B3**: TOC panel + prev/next button navigation
- **B4**: Add "Preview" tab to sidebar navigation
- **B5**: Auto-navigate to preview after conversion completes

### Phase C: CSS Editor + Template Preview
- **C1**: Beginner mode (form UI with descriptions, default values display)
- **C2**: Advanced mode (text editor for direct CSS editing)
- **C3**: Real-time preview integration (debounced)
- **C4**: Template save / reset functionality

### Phase D: epubcheck Integration
- **D1**: Add JAR path setting to Settings page
- **D2**: epubcheck execution service (JSON output parsing)
- **D3**: Validation button in preview + error list panel
- **D4**: Independent "Validate" tab in sidebar (supports existing EPUBs)
- **D5**: Error -> preview page jump

### Phase E: Source Mapping (Incremental)
- **E1**: Meta info generation (chapter <-> XHTML file mapping)
- **E2**: Line number mapping
- **E3**: Full element mapping

## Technical Stack
- **WebView2**: Microsoft.Web.WebView2 (Windows 10/11 pre-installed runtime)
- **Avalonia WebView2**: Avalonia.WebView2 or custom hosting
- **epubcheck**: External JAR, path stored in GuiSettings
- **Meta info**: JSON file (`{name}.epub.meta.json`) alongside EPUB output
