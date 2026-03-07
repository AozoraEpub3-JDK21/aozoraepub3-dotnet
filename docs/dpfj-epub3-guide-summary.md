# DPFJ EPUB 3 File Creation Guide v1.1.4 -- Requirements Summary

Source: "DPFJ EPUB 3 File Creation Guide ver.1.1.4" (2025-10-24), published by the Digital Publishers Federation of Japan (DPFJ, formerly EBPAJ). Based on EPUB 3.3 (W3C Recommendation 2023).

This document summarizes the concrete technical requirements from the guide, its reference materials, and the two sample EPUBs (`dpfj-sample` and `book-template`).

---

## 1. Validation

- All output must pass the latest **epubcheck** without errors.
  - GitHub: https://github.com/w3c/epubcheck

---

## 2. OCF Container Structure

### 2.1 Required Folder Layout

```
root/
  mimetype
  META-INF/
    container.xml
  item/
    standard.opf
    navigation-documents.xhtml
    image/
    style/
    xhtml/
```

- Root folder name: per publisher instruction.
- File/folder names: **lowercase** (except `META-INF`).
- Content storage folder: `item` (to match `<item>` in OPF).
- All resources in designated subfolders inside `item/`. No additional subfolders.
- Image files in `image/`, CSS in `style/`, XHTML in `xhtml/`.

### 2.2 mimetype

```
application/epub+zip
```

- Must be the **first entry** in the ZIP archive.
- Must be stored **uncompressed** (no compression).
- No trailing newline.

### 2.3 container.xml

```xml
<?xml version="1.0"?>
<container
 version="1.0"
 xmlns="urn:oasis:names:tc:opendocument:xmlns:container"
>
<rootfiles>
<rootfile
 full-path="item/standard.opf"
 media-type="application/oebps-package+xml"
/>
</rootfiles>
</container>
```

- Namespace: `urn:oasis:names:tc:opendocument:xmlns:container`
- OPF path: `item/standard.opf`

---

## 3. Package Document (OPF)

### 3.1 Root Element

```xml
<package
 xmlns="http://www.idpf.org/2007/opf"
 version="3.0"
 xml:lang="ja"
 unique-identifier="unique-id"
 prefix="ebpaj: http://www.ebpaj.jp/
         dpfj: https://www.dpfj.or.jp/"
>
```

- Namespace: `http://www.idpf.org/2007/opf`
- `version="3.0"`
- `xml:lang="ja"` for Japanese publications
- `unique-identifier` references the `<dc:identifier>` id
- `prefix` attribute declares custom metadata prefixes:
  - `ebpaj: http://www.ebpaj.jp/` (legacy)
  - `dpfj: https://www.dpfj.or.jp/` (current)
  - `rendition: http://www.idpf.org/vocab/rendition/#` (when using rendition metadata)

### 3.2 Required Metadata

```xml
<metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
  <dc:title id="title">Work Title</dc:title>
  <dc:creator id="creator01">Author Name</dc:creator>
  <meta refines="#creator01" property="role" scheme="marc:relators">aut</meta>
  <dc:publisher id="publisher">Publisher Name</dc:publisher>
  <dc:language>ja</dc:language>
  <dc:identifier id="unique-id">urn:uuid:{UUID}</dc:identifier>
  <meta property="dcterms:modified">2025-07-01T00:00:00Z</meta>
  <meta property="ebpaj:guide-version">1.1.4</meta>
  <meta property="dpfj:guide-version">1.1.4</meta>
</metadata>
```

| Field | Element | Notes |
|-------|---------|-------|
| Title | `<dc:title id="title">` | All listed content should be displayable |
| Creator | `<dc:creator id="creatorNN">` | Multiple allowed; each with `<meta refines>` for role |
| Creator role | `<meta refines="#creatorNN" property="role" scheme="marc:relators">aut</meta>` | MARC relator codes |
| Publisher | `<dc:publisher id="publisher">` | |
| Language | `<dc:language>ja</dc:language>` | |
| Identifier | `<dc:identifier id="unique-id">` | Format: `urn:uuid:{UUID}` (default; follow publisher instructions) |
| Modified date | `<meta property="dcterms:modified">` | ISO 8601 format; use delivery date if no revision date given |
| Guide version | `<meta property="ebpaj:guide-version">1.1.4</meta>` | Legacy identifier |
| Guide version | `<meta property="dpfj:guide-version">1.1.4</meta>` | Current identifier |

### 3.3 Manifest

```xml
<manifest>
  <!-- Navigation document -->
  <item media-type="application/xhtml+xml" id="toc"
        href="navigation-documents.xhtml" properties="nav"/>

  <!-- CSS files -->
  <item media-type="text/css" id="book-style" href="style/book-style.css"/>
  <item media-type="text/css" id="style-reset" href="style/style-reset.css"/>
  <item media-type="text/css" id="style-standard" href="style/style-standard.css"/>
  <item media-type="text/css" id="style-advance" href="style/style-advance.css"/>
  <item media-type="text/css" id="style-check" href="style/style-check.css"/>

  <!-- Images -->
  <item media-type="image/jpeg" id="cover"
        href="image/cover.jpg" properties="cover-image"/>
  <item media-type="image/jpeg" id="img-001" href="image/img-001.jpg"/>

  <!-- XHTML content documents -->
  <item media-type="application/xhtml+xml" id="p-cover" href="xhtml/p-cover.xhtml"/>
  <item media-type="application/xhtml+xml" id="p-001" href="xhtml/p-001.xhtml"/>
</manifest>
```

- Navigation document: `properties="nav"`
- Cover image: `properties="cover-image"`
- Fixed-layout SVG pages: `properties="svg"`

### 3.4 Spine

```xml
<spine page-progression-direction="rtl">
  <itemref linear="yes" idref="p-cover" properties="page-spread-left"/>
  <itemref linear="yes" idref="p-001" properties="page-spread-left"/>
  <itemref linear="yes" idref="p-002"/>
</spine>
```

- `page-progression-direction`: `"rtl"` for vertical Japanese, `"ltr"` for horizontal
- `linear="yes"` or `"no"` on each `<itemref>`
- `properties`: `page-spread-left`, `page-spread-right`
- Items not in spine should not be shown as book pages

### 3.5 Rendition Metadata (Reference Information supplement)

These are **optional** (omission uses EPUB 3 defaults shown in blue):

```xml
<meta property="rendition:flow">auto</meta>
<meta property="rendition:layout">reflowable</meta>
<meta property="rendition:spread">auto</meta>
<meta property="rendition:orientation">auto</meta>
```

For fixed layouts:

```xml
<meta property="rendition:layout">pre-paginated</meta>
<meta property="rendition:spread">landscape</meta>
<meta property="rendition:viewport">width=848, height=1200</meta>
```

When using rendition properties, add to prefix: `rendition: http://www.idpf.org/vocab/rendition/#`

Per-page rendition overrides in spine `<itemref>`:
- `rendition:layout-pre-paginated`
- `rendition:flow-scrolled-continuous`
- `rendition:flow-scrolled-doc`
- `rendition:page-spread-center`
- `rendition:spread-none`

---

## 4. Navigation Document

### 4.1 Required Structure

Filename: `navigation-documents.xhtml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE html>
<html
 xmlns="http://www.w3.org/1999/xhtml"
 xmlns:epub="http://www.idpf.org/2007/ops"
 xml:lang="ja"
>
<head>
<meta charset="UTF-8"/>
<title>Navigation</title>
</head>
<body>
<nav epub:type="toc" id="toc">
<h1>Navigation</h1>
<ol>
<li><a href="xhtml/p-cover.xhtml">Cover</a></li>
<li><a href="xhtml/p-toc.xhtml">Contents</a></li>
<li><a href="xhtml/p-colophon.xhtml">Imprint</a></li>
</ol>
</nav>
</body>
</html>
```

- **Must** contain `<nav epub:type="toc">` (epubcheck requirement).
- Minimum links: cover, contents, imprint (colophon).
- No style sheet link needed unless displaying as body content.
- NCX is obsolete; navigation document takes priority.

### 4.2 Optional landmarks nav (Reference Information)

```xml
<nav epub:type="landmarks" id="guide">
<h1>Guide</h1>
<ol>
<li><a epub:type="cover" href="xhtml/p-cover.xhtml">Cover</a></li>
<li><a epub:type="toc" href="xhtml/p-toc.xhtml">Contents</a></li>
<li><a epub:type="bodymatter" href="xhtml/p-titlepage.xhtml">Main</a></li>
</ol>
</nav>
```

---

## 5. Content Documents (XHTML)

### 5.1 Common XHTML Template

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE html>
<html
 xmlns="http://www.w3.org/1999/xhtml"
 xmlns:epub="http://www.idpf.org/2007/ops"
 xml:lang="ja"
 class="vrtl"
>
<head>
<meta charset="UTF-8"/>
<title>Work Title</title>
<link rel="stylesheet" type="text/css" href="../style/book-style.css"/>
</head>
<body class="p-tobira">
<div class="main">
  <!-- content -->
</div>
</body>
</html>
```

### 5.2 Required Namespaces

- XHTML: `xmlns="http://www.w3.org/1999/xhtml"`
- EPUB ops: `xmlns:epub="http://www.idpf.org/2007/ops"`

### 5.3 Text Orientation Classes on `<html>`

| Class | Meaning |
|-------|---------|
| `class="hltr"` | Horizontal writing, left-to-right progression |
| `class="vrtl"` | Vertical writing, right-to-left progression |

- Image-only pages use `hltr` to center images horizontally.
- Vertically-oriented LTR is not assumed.

### 5.4 epub:type Usage

Only two required:
- Cover page: `<body epub:type="cover" class="p-cover">`
- Navigation TOC: `<nav epub:type="toc" id="toc">`

### 5.5 Standard Page Types and Body Classes

| Page Type | Filename Pattern | Body Class | html Class |
|-----------|-----------------|------------|------------|
| Cover | `p-cover.xhtml` | `p-cover` | `hltr` |
| Front matter | `p-fmatter-NNN.xhtml` | `p-image` | `hltr` |
| Title page | `p-titlepage.xhtml` | `p-titlepage` | `hltr` |
| Caution | `p-caution.xhtml` | `p-caution` | `vrtl` |
| TOC | `p-toc.xhtml` | `p-toc` | `vrtl` |
| Section title (tobira) | `p-NNN.xhtml` | `p-tobira` | `vrtl` |
| Body | `p-NNN.xhtml` | `p-text` | `vrtl` |
| Colophon (imprint) | `p-colophon.xhtml` | `p-colophon` | `hltr` |
| Advertisement | `p-ad-NNN.xhtml` | `p-image` | `hltr` |

### 5.6 File Splitting Rules

- Split at page breaks from the original text.
- If no page breaks, split at approximately **240KB** (under 256KB).
- Prefer splitting before headings; otherwise at blank lines.

### 5.7 Title Element

- `<title>` contains the work title.
- Connect main title, subtitle, and series name with full-width spaces.

### 5.8 Source Formatting Rules

- Attribute order within body elements: `epub:type` -> `class` -> `id` -> `src/href` -> `alt`
- Line breaks after HTML elements in XHTML.
- Line breaks before/after start and end tags of block-level elements (`<div>`).
- No line breaks immediately after start tags or before end tags for `<p>` and `<h1>`-`<h6>`.
- Avoid `class` on `<p>` where possible.

### 5.9 Encoding

- **UTF-8** without BOM (UTF-8N) recommended.
- Do not mix line break codes (CR+LF, CR, LF) within the same file.

---

## 6. CSS Requirements

### 6.1 Style Sheet Architecture (Reflowable)

```
book-style.css              <-- Only CSS linked from XHTML
  @import "style-reset.css"
  @import "style-standard.css"
  @import "style-advance.css"
  /* @import "style-check.css" -- for Windows checking only, remove for delivery */
  /* Per-work customization area */
```

- Only `book-style.css` is linked from XHTML via `<link>`.
- Other CSS loaded via `@import` inside `book-style.css`.
- Do not nest `@import` (no `@import` inside imported files).
- All CSS files start with `@charset "UTF-8";`

### 6.2 CSS Reset (style-reset.css)

Key body defaults:

```css
body {
  margin: 0;
  padding: 0;
  font-size: 100%;
  vertical-align: baseline;
  line-height: 1.75;
  background: transparent;
  word-spacing: normal;
  letter-spacing: normal;
  white-space: normal;
  word-wrap: break-word;
  text-align: justify;
  -webkit-line-break: normal;
  -epub-line-break: normal;
  -webkit-word-break: normal;
  -epub-word-break: normal;
  -webkit-hyphens: auto;
  -epub-hyphens: auto;
  -webkit-text-underline-position: under left;
  -epub-text-underline-position: under left;
}
```

- `margin: 0; padding: 0;` on body (RS adds its own margins)
- Default line height: **1.75**
- `text-align: justify`
- `word-wrap: break-word` to prevent long words from overflowing

### 6.3 Writing Mode (html element)

Only writing mode and font are set on `<html>`:

```css
/* Vertical writing */
.vrtl {
  -webkit-writing-mode: vertical-rl;
  -epub-writing-mode: vertical-rl;
  writing-mode: vertical-rl;
}

/* Horizontal writing */
.hltr {
  -webkit-writing-mode: horizontal-tb;
  -epub-writing-mode: horizontal-tb;
  writing-mode: horizontal-tb;
}
```

### 6.4 CSS Prefix Requirements

Properties must include both `-epub-` and `-webkit-` prefixes for compatibility:

| Property | Prefixed Forms |
|----------|---------------|
| `writing-mode` | `-epub-writing-mode`, `-webkit-writing-mode`, `writing-mode` |
| `text-orientation` | `-epub-text-orientation` |
| `text-combine` | `-epub-text-combine: horizontal` |
| `text-combine-upright` | `text-combine-upright: all` (additionally recommended) |
| `text-underline-position` | `-epub-text-underline-position: under left` |
| `line-break` | `-epub-line-break`, `-webkit-line-break` |
| `word-break` | `-epub-word-break`, `-webkit-word-break` |
| `hyphens` | `-epub-hyphens`, `-webkit-hyphens` |

- `-epub-` prefix takes priority per EPUB 3.0.1 spec.
- Only properties in EPUB 3.0.1 spec may use `-epub-` prefix.

### 6.5 Vertical Writing CSS Properties

**Text direction in vertical mode:**
- Upright: `-epub-text-orientation: upright;`
- Sideways (90deg right): `-epub-text-orientation: sideways;`
- `rotate-right` value should behave like `sideways`

**Horizontal-in-vertical (tate-chu-yoko):**
```css
-epub-text-combine: horizontal;
/* Additionally recommended: */
text-combine-upright: all;
```
- Assumed for up to **3 half-width digits**.
- Combined text treated as single character for decorations, rubies, etc.

### 6.6 Font Requirements

Minimum fonts:
- **Serif** (Ming-cho / Mincho): monospaced
- **Sans-serif** (Gothic): monospaced

Font families in CSS:
```css
/* Horizontal headings */
.hltr h1 { font-family: serif-ja, serif; }
/* Vertical headings */
.vrtl h1 { font-family: serif-ja-v, serif-ja, serif; }
```

- `serif` maps to Ming-cho type
- `sans-serif` maps to Gothic type
- Em size must be equal between serif and sans-serif at same font size
- Monospaced preferred for vertical typesetting

### 6.7 Key CSS Classes

#### Text Size
- `font-Nem` / `font-NemNN` (e.g., `font-1em50` = 1.50em)
- `font-NNNper` (e.g., `font-085per` = 85%)
- Range: 0.50em to 3.00em in the default set

#### Text Alignment
- `align-left`, `align-center`, `align-right`, `align-justify`
- `align-start`, `align-end` (logical direction)

#### Text Indentation
- `start-Nem` (line-start indent, e.g., `start-1em`)
- `end-Nem` (line-end indent)
- `h-indent-Nem` (hanging indent)

#### Decorations
- Emphatic dots: `em-sesame` (sesame dot), `em-dot` (filled dot)
- Emphasis on right side (vertical): via `text-emphasis`
- Text underline/overline for side lines

#### Ruby
```html
<ruby>漢<rt>かん</rt>字<rt>じ</rt></ruby>
```
- Mono-ruby (per-character) designation required
- Ruby strings may contain: text, number/text references, gaiji images, text-orientation, tate-chu-yoko

#### Display
- `display-none`, `display-inline`, `display-inline-block`, `display-block`

#### Page Breaks
- `pagebreak` (break after), `pagebreak-before`, `pagebreak-both`

---

## 7. Images

### 7.1 Supported Formats

| Format | Notes |
|--------|-------|
| JPEG | Always supported |
| PNG | Transparent backgrounds preferred |
| GIF | Transparent backgrounds preferred |
| WebP | New in EPUB 3.3; confirm RS support before use |

### 7.2 Cover Image

- Filename: `cover.jpg` (standardized for thumbnail speed)
- Declared in manifest with `properties="cover-image"`
- Cover page uses `<img class="fit" src="../image/cover.jpg" alt=""/>`
- `class="fit"` applies page-fitting CSS

### 7.3 Non-standard Kanji (Gaiji) Images

- Recommended size: **128px x 128px**
- Format: 8-bit **transparent-background PNG**
- Anti-aliasing: none
- CSS classes: `gaiji`, `gaiji-line`, `gaiji-wide`
- Displayed at single-character size using image reduction

### 7.4 Image Page Fitting

The `fit` class uses `max-width` / `max-height` for page fitting:

```css
.fit {
  max-width: 100%;
  max-height: 100%;
}
```

- Images cannot be enlarged beyond original size in reflowable.
- Borders must not be applied to page-fit images (they overflow).
- Users should be able to pinch-zoom reduced images back to original size.

### 7.5 Image Placement in Body

```html
<!-- Inline image -->
<p><img src="../image/img-001.jpg" alt=""/></p>

<!-- Page-fit image -->
<p><img class="fit" src="../image/img-001.jpg" alt=""/></p>
```

- Two-page spread images: create as single connected image, then page-fit.

---

## 8. Fixed Layout

### 8.1 Overview

- Limited to **image-only works** in this guide.
- Uses **SVG wrapping** method to fit images to page.
- SVG written directly in XHTML.

### 8.2 OPF Differences from Reflowable

```xml
<meta property="rendition:layout">pre-paginated</meta>
<meta property="rendition:spread">landscape</meta>
<meta property="rendition:viewport">width=848, height=1200</meta>
```

- Cover page: `properties="rendition:page-spread-center"` in spine itemref (always displayed alone)
- Other pages: `page-spread-right` / `page-spread-left` for opposing spreads
- SVG-containing pages: `properties="svg"` in manifest item

### 8.3 Fixed Layout XHTML Template

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE html>
<html
 xmlns="http://www.w3.org/1999/xhtml"
 xmlns:epub="http://www.idpf.org/2007/ops"
 xml:lang="ja"
>
<head>
<meta charset="UTF-8"/>
<title>Work Title</title>
<link rel="stylesheet" type="text/css" href="../style/fixed-layout-jp.css"/>
<meta name="viewport" content="width=848, height=1200"/>
</head>
<body>
<div class="main">
<svg xmlns="http://www.w3.org/2000/svg" version="1.1"
 xmlns:xlink="http://www.w3.org/1999/xlink"
 width="100%" height="100%" viewBox="0 0 848 1200">
<image width="848" height="1200" xlink:href="../image/img-001.jpg"/>
</svg>
</div>
</body>
</html>
```

- CSS file: `fixed-layout-jp.css` (separate from reflowable styles)
- `<meta name="viewport">` recommended in each XHTML for safety
- Image formats same as reflowable

### 8.4 Partial Fixed Layout (Reference)

Mixing reflowable and fixed-layout pages is possible via per-page spine properties:
```xml
<itemref idref="p-001" properties="rendition:layout-pre-paginated page-spread-right"/>
```
- Only use for image-only pages
- Spread display not guaranteed across all RS

---

## 9. Specific Content Patterns

### 9.1 Rubies

```html
<ruby>漢<rt>かん</rt>字<rt>じ</rt></ruby>
```

### 9.2 Horizontal Text in Vertical Lines (Tate-chu-yoko)

```html
<span class="tcy">12</span>
```
```css
.tcy {
  -epub-text-combine: horizontal;
  text-combine-upright: all;
}
```

### 9.3 Emphasis Marks

```html
<span class="em-sesame">emphasized text</span>
```

### 9.4 Superscript / Subscript

```html
<span class="super">*</span>  <!-- superscript -->
<span class="sub">1</span>    <!-- subscript -->
```

### 9.5 Annotations (Endnotes)

```html
<!-- Reference side -->
<p><a class="noteref" id="noteref-001" href="p-notes.xhtml#note-001">
  Text<span class="super">*</span></a></p>

<!-- Note side -->
<p><a class="note" id="note-001" href="p-001.xhtml#noteref-001">*Text</a> Note content</p>
```

### 9.6 Separator

```html
<hr/>  <!-- horizontal line in horizontal; vertical in vertical -->
```

---

## 10. Character Set

- Minimum: **JIS X 0213:2004** (includes Unicode surrogate pair area)
- Future goal: Adobe-Japan-1-6 character set
- IVS (Ideographic Variation Selector) support desired but not yet assumed

---

## 11. Items Not Assumed by This Guide

### 11.1 HTML Elements Not Used

- Sectioning: `<section>`, `<article>`, `<aside>`, `<header>`, `<footer>`, `<address>`
- Lists: `<ul>`, `<ol>` (outside nav), `<dl>`, `<dt>`, `<dd>`
- Tables: `<table>` and all related elements
- Forms, scripts, embedded content (`<video>`, `<audio>`, `<iframe>`, `<canvas>`, `<object>`)
- Semantic: `<em>`, `<strong>`, `<blockquote>`, `<figure>`, `<figcaption>`, `<sub>`, `<sup>`, `<pre>`
- MathML, SVG in reflowable (except fixed layout)

### 11.2 CSS Properties Not Used

- Units: `ex`, `in`, `cm`, `mm`, `pt`, `pc`
- Pseudo-elements: `::before`, `::after`, `::first-line`, `::first-letter`
- Selectors: `:focus`, `:first-child`, `:nth-child()`, `:not()`, etc.
- `@page` rules
- Background images (`background-image`, `background-position`, etc.)
- `text-shadow`, `text-transform`, `white-space`, `word-spacing`
- `min-width`, `min-height`
- `position`, `float`, `clear` (wraparound is reference-only, not recommended)
- `visibility`, `z-index`, `overflow`
- Table layout properties
- Multi-column (`column-width`, `column-count`, etc.)
- CSS Speech properties
- `display: table-*` variants, `display: list-item`

### 11.3 Not Assumed

- **JavaScript** is not assumed.
- **Alternative style sheets** are not used.
- **NCX** is obsolete; not relied upon.

---

## 12. Future RS Expectations (Supplement)

Items RSs are expected to support in the future:

- Full Adobe-Japan-1-6 character set and IVS
- `text-orientation: upright` / `sideways` (without `-epub-` prefix)
- Background color on block elements and entire pages
- SVG wrapping in reflowable publications
- Media Queries (`@media (orientation: landscape/portrait)`)
- `float` / `clear` wraparound
- Sectioning elements (`<section>`, `<aside>`, `<article>`)
- `object-fit` / `object-position` for image fitting
- `border-radius`
- Flexbox layout in page media
- `text-indent: hanging`

---

## 13. Key Differences: Reflowable vs Fixed Layout

| Aspect | Reflowable | Fixed Layout |
|--------|-----------|--------------|
| `rendition:layout` | `reflowable` (default, omittable) | `pre-paginated` |
| Page size | Dynamic (RS-controlled) | Fixed via `rendition:viewport` |
| CSS file | `book-style.css` (with @import chain) | `fixed-layout-jp.css` (standalone) |
| Content | HTML text + inline images | SVG-wrapped images |
| SVG | Not used (in this guide) | Required for image fitting |
| `properties` on manifest item | -- | `svg` for SVG-containing pages |
| Cover in spine | `page-spread-left` | `rendition:page-spread-center` |
| Spread display | Pages flow naturally | Opposing pairs (right/left) required |

---

## 14. Concrete Values Reference

### Default CSS Values

| Property | Default Value |
|----------|--------------|
| `line-height` | `1.75` |
| `font-size` | `100%` |
| `text-align` | `justify` |
| `margin` (body) | `0` |
| `padding` (body) | `0` |
| `word-wrap` | `break-word` |

### Common Class Name Patterns

| Pattern | Example | CSS |
|---------|---------|-----|
| Font size | `font-1em50` | `font-size: 1.50em` |
| Font size % | `font-085per` | `font-size: 85%` |
| Line height | `line-height-3em50` | `line-height: 3.50` |
| Start indent | `start-1em` | (vertical: `padding-top`; horizontal: `padding-left`) |
| Hanging indent | `h-indent-1em` | `text-indent: -1em; padding-top/left: 1em` |
| Margin | `m-start-2em` | Logical-direction margin |
| Padding | `p-2em` | All padding |
| Border | `k-solid` | `border: 1px solid` |
| Color | `color-red` | `color: #ff0000` |
| Background | `bg-silver` | `background-color: #c0c0c0` |
| Gothic font | `gfont` | `font-family: sans-serif` |

### Logical Direction Naming Convention

| Name | Vertical Meaning | Horizontal Meaning |
|------|-----------------|-------------------|
| `start` | top | left |
| `end` | bottom | right |
| `before` | right | top |
| `after` | left | bottom |
| `center` | center (line direction) | center (line direction) |
| `middle` | middle (page progression) | middle (page progression) |
