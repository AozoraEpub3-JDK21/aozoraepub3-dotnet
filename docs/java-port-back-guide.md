# AozoraEpub3-JDK21 への反映ガイド

本ドキュメントは、aozoraepub3-dotnet で実装した narou.rb 互換機能を
AozoraEpub3-JDK21 (Java版) に反映する際の手順と技術詳細をまとめたものです。

## 背景

narou.rb + AozoraEpub3-JDK21 のパイプラインで生成される EPUB と、
aozoraepub3-dotnet の `--url` 直接変換で生成される EPUB の差異を解消するため、
.NET 側で以下の対応を行いました。Java 版にも同様の対応を推奨します。

## 対応一覧

### A カテゴリ: AozoraEpub3 本体の修正

| # | 内容 | Java側の対応 |
|---|------|-------------|
| A1-A3 | `vertical_font.css` に line-height, .introduction/.postscript 等を追加 | `template/OPS/css/vertical_font.css` を更新 |
| A4 | URL変換時に title.xhtml を生成 | `--url` 時に `TitlePageType` を設定 |
| A5 | OPF `<guide>` 要素の出力 | `InsertTocPage=true` で自動出力 |
| A6 | nav.xhtml landmarks の出力 | 同上 |
| A7 | テンプレート出力の CRLF 統一 | Java は既に CRLF (対応不要) |
| A8 | tcy span ネスト | Java/C# 共に単重ネスト (対応不要) |
| B5 | `chuki_tag.txt` に二分アキ・前書き・後書きの注記変換を追加 | 同ファイルに追加 |

### B カテゴリ: narou.rb 互換テキスト前処理

Java 版では `--url` 変換時の後処理として実装することを推奨します。

| # | 内容 | 実装場所 (推奨) |
|---|------|----------------|
| B1 | 自動行頭字下げ | `AozoraTextFinalizer` 相当のクラスを新規作成 |
| B2 | 空行圧縮 | 同上 |
| B3 | 漢数字変換 | 同上 |
| B4 | 英字全角化 | 同上 |
| B5 | 二分アキ挿入 | 同上 |
| B6 | 読了表示 | 同上 |
| B7 | TOC ページネーション | `WebAozoraConverter` (既存コードの有効化) |

---

## 詳細手順

### A1-A3: vertical_font.css の更新

**ファイル:** `template/OPS/css/vertical_font.css`

現在のフォント定義のみの CSS を、以下の完全版に置き換えます:

```css
@charset "utf-8";
@namespace "http://www.w3.org/1999/xhtml";

body {
	font-family: "@ＭＳ 明朝", "@MS Mincho", "ヒラギノ明朝 ProN W3", "HiraMinProN-W3", serif, sans-serif;
	line-height: 1.8em !important;
}

.gtc, .b {
	font-family: '@ＭＳ ゴシック','@MS Gothic',sans-serif !important;
}

.b { font-weight: bold; }
.i { font-style: italic; }

rt { font-size: 0.6em; }

/* 柱（もどき） */
.running_head {
	position: absolute !important;
	top: 15px;
	left: 10px;
	font-size: 0.8em;
}

/* 二分アキ */
.half_em_space { padding-top: 0.5em; }

/* パラメーター（折り返しあり） */
.custom_parameter_block {
	font-size: 100%;
	line-height: 1.2em !important;
	border: 2px solid #000;
	border-radius: 4px;
	margin: 1em 0.5em 1em 0.5em;
	padding: 1em 0.2em 1em 0.2em;
	display: inline-block;
	font-family: sans-serif !important;
	font-weight: bold;
	box-shadow: 3px 3px 3px #bbb;
	-webkit-box-shadow: 3px 3px 3px #bbb;
}
.jzm .custom_parameter_block { display: block; }
.jzm .p .custom_parameter_block { display: inline-block; }

/* 前書き */
.introduction {
	float: right;
	font-size: 83%;
	line-height: 1.5em !important;
	border-top: 3px solid #aaa;
	color: #555;
	margin: 0.25em;
	margin-right: 1em;
	padding: 1em 0.5em 1em 0.5em;
	display: inline-block;
	font-family: sans-serif !important;
	text-align: left !important;
	height: 70%;
}
.jzm .introduction { display: block; }
.jzm .p .introduction { display: inline-block; }

/* 後書き */
.postscript {
	float: right;
	font-size: 83%;
	line-height: 1.5em !important;
	border-top: 3px solid #888;
	color: #222;
	margin: 0.25em;
	margin-right: 2em;
	padding: 1em 0.5em 1em 0.5em;
	display: inline-block;
	font-family: sans-serif !important;
	text-align: left !important;
	height: 70%;
}
.jzm .postscript { display: block; }
.jzm .p .postscript { display: inline-block; }

div.clear { clear: both; }
```

**影響:** 全ての EPUB 出力に反映される。`line-height: 1.8em !important` が
`vertical_text.css` の値を上書きし、narou.rb 出力と同じ行間になる。

---

### A4-A6: URL 変換時の BookInfo 設定

**ファイル:** `--url` 変換処理のメインメソッド

URL 変換で BookInfo を構築した後、以下の設定を追加:

```java
bookInfo.setVertical(true);
bookInfo.setInsertTocPage(true);         // A5: <guide> + nav landmarks
bookInfo.setTocVertical(true);           // 目次も縦書き
bookInfo.setTitlePageType(BookInfo.TITLE_HORIZONTAL); // A4: title.xhtml 生成
bookInfo.setInsertTitleToc(true);        // 目次にタイトルを含める
```

**注意:** 現在の `--url` 実装では `InsertTocPage=false` がハードコードされている可能性あり。

---

### B5 (Converter側): chuki_tag.txt への注記追加

**ファイル:** `chuki_tag.txt`

「その他」セクションの前に以下を追加:

```
####二分アキ (narou.rb互換)
二分アキ	<span class="half_em_space"> </span>

####前書き・後書き (narou.rb互換)
ここから前書き	<div class="introduction">
ここで前書き終わり	</div><div class="clear"></div>
ここから後書き	<div class="postscript">
ここで後書き終わり	</div><div class="clear"></div>
```

**タブ区切り:** 注記名と HTML タグの間はタブ文字。

---

### B1: 自動行頭字下げ

narou.rb の `auto_indent` 相当。以下のアルゴリズム:

1. **判定フェーズ:** 字下げ判定除外文字 `(（「『〈《≪【〔―・※［〝\n` で始まる行を除外した中で、
   50% 以上が未字下げ（先頭が空白でない）なら字下げを適用
2. **ダッシュ行:** `――` で始まる行は `　――` にする（全角スペース追加）
3. **字下げ:** 除外文字（上記から `・` を除く）で始まらない行の先頭に `　` を挿入。
   ただし `・` 1文字だけの場合はスキップ（三点リーダー代替の対策）

```java
// 判定除外文字
static final String IGNORE_INDENT_CHARS = "(（「『〈《≪【〔―・※［〝\n";
// 字下げ対象外文字（・を除外）
static final String AUTO_INDENT_IGNORE = "(（「『〈《≪【〔―※［〝\n";

// 判定: targetCount 中の noIndentCount の割合が 0.5 超なら字下げ実行
// 字下げ: 行頭が AUTO_INDENT_IGNORE に含まれなければ「　」を挿入
```

---

### B2: 空行圧縮

narou.rb の `enable_pack_blank_line` 相当。連続する空行を最大 1 つに削減。

```
アルゴリズム:
- 連続する空行が 2 行以上あれば 1 行に削減
- つまり段落間の 1 空行は維持、2 空行以上は 1 空行に圧縮
```

---

### B3: 漢数字変換

narou.rb の `enable_convert_num_to_kanji` 相当。

```
変換ルール:
- 半角数字 (0-9) → 全角数字 (０-９) → 漢数字 (〇一二三四五六七八九)
- カンマを含む数字列 (1,234) はそのまま全角化 (１，２３４)
- 注記行 (［＃ で始まる行) はスキップ

デフォルト: 有効 (narou.rb と同じ)
```

---

### B4: 英字全角化

narou.rb の `alphabet_to_zenkaku` 相当。

```
変換ルール:
- 正規表現 [\w.,!?'" &:;-]+ にマッチする英字含有部分を処理
- 2語以上（スペース含む）の英文 → 半角のまま保護
- 8文字以上の英単語 → 半角のまま保護
- それ以外の短い英単語 → 全角化 (a-z → ａ-ｚ, A-Z → Ａ-Ｚ)
- enable_alphabet_force_zenkaku=true なら全て全角化

デフォルト: 非強制モード (短い英単語のみ全角化)
```

---

### B5 (Finalizer側): 二分アキ挿入

narou.rb の `half_indent_bracket` 相当。

```
正規表現: ^[ 　\t]*((?:[〔「『(（【〈《≪〝]))
置換: ［＃二分アキ］$1

行頭の空白を除去し、開きかぎ括弧の前に ［＃二分アキ］ を挿入。
chuki_tag.txt の定義により <span class="half_em_space"> </span> に変換される。
```

---

### B6: 読了表示

narou.rb の `enable_display_end_of_book` 相当。

テキスト末尾に以下を追加:

```
（空行）
［＃ここから地付き］［＃小書き］（本を読み終わりました）［＃小書き終わり］［＃ここで地付き終わり］
```

---

### B7: TOC ページネーション

**ファイル:** `WebAozoraConverter.java`

Java 版には既に `PAGE_URL` による TOC ページネーションコード（737-773行付近）が
存在します。`--url` 変換パスでこのコードが実行されていることを確認してください。

なろうの 101 話以上の小説は目次が複数ページ (`?p=1`, `?p=2`, ...) に分割されます。
`extract.txt` の `PAGE_URL` 設定で最後のページリンクを取得し、
`?p=N` の N を総ページ数として 2 ページ目以降を順次ダウンロードします。

```
extract.txt 設定:
PAGE_URL	a[href*="?p="]:-1	(\?p=)\d+\t(\d+)	$1$2
```

---

## デフォルト設定値 (narou.rb 準拠)

以下は narou.rb の `ORIGINAL_SETTINGS` に基づくデフォルト値です:

| 設定 | デフォルト | 説明 |
|------|-----------|------|
| enable_auto_indent | true | 自動字下げ |
| enable_pack_blank_line | true | 空行圧縮 |
| enable_convert_num_to_kanji | true | 漢数字変換 |
| enable_kanji_num_with_units | true | 漢数字の単位化 (千・万等) |
| kanji_num_with_units_lower_digit_zero | 3 | 単位化する下位ゼロ桁数 |
| enable_alphabet_force_zenkaku | false | 全英字強制全角 |
| enable_half_indent_bracket | true | 二分アキ |
| enable_display_end_of_book | true | 読了表示 |
| enable_author_comments | true | 前書き・後書き検出 |

---

## サイト対応

narou.rb のテキスト前処理は **サイト非依存 (ユニバーサル)** です。
以下 6 サイトすべてに同一の処理が適用されます:

| サイト | ドメイン |
|--------|----------|
| 小説家になろう | ncode.syosetu.com |
| なろう R18 | novel18.syosetu.com |
| カクヨム | kakuyomu.jp |
| ハーメルン | syosetu.org |
| 暁 | www.akatsuki-novels.com |
| Arcadia | www.mai-net.net |

---

## テスト方法

### 比較テスト手順

1. narou.rb + AozoraEpub3-JDK21 で EPUB を生成
2. AozoraEpub3-JDK21 単体 (`--url`) で同じ小説の EPUB を生成
3. 両 EPUB を展開して以下を比較:
   - `OPS/css/vertical_font.css` — 完全一致すべき
   - `OPS/xhtml/*.xhtml` — 字下げ・漢数字・英字・二分アキが反映されているか
   - `OPS/package.opf` — `<guide>` 要素が存在するか
   - `OPS/xhtml/title.xhtml` — 中表紙が生成されているか
   - `OPS/xhtml/nav.xhtml` — landmarks に toc/titlepage/bodymatter があるか

### 推奨テスト小説

- **短編:** https://ncode.syosetu.com/n7673ff/ (274話, 目次4ページ — ページネーションテスト)
- **単話:** カクヨムの短編 (目次なし — 単一ページ作品テスト)

---

## .NET 実装リファレンス

- `src/AozoraEpub3.Core/Web/AozoraTextFinalizer.cs` — B1-B6 の実装
- `src/AozoraEpub3.Core/Web/NarouFormatSettings.cs` — 設定定義
- `src/AozoraEpub3.Core/Web/WebAozoraConverter.cs` — B7 (TOCページング)
- `src/AozoraEpub3.Core/Resources/chuki_tag.txt` — 二分アキ・前書き・後書き注記
- `src/AozoraEpub3.Core/Resources/template/OPS/css/vertical_font.css` — A1-A3

narou.rb ソース (参考):
- `C:\Ruby34-x64\lib\ruby\gems\3.4.0\gems\narou-3.9.1\lib\converterbase.rb`
