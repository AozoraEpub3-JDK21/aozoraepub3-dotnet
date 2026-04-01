# Memory (2026-04-01): narou.rb 完全互換トラック

## 前提
- 本アプリの最優先は `narou.rb` 完全互換。
- 比較基準は `narou.rb + aozoraepub3` のマスター出力。

## 現在の到達点
- 比較セット（代表）:
  - 66: なろう `https://ncode.syosetu.com/n8005ls/`
  - 68: カクヨム `https://kakuyomu.jp/works/822139840468926025`
- 最新ベスト（siteH）差分:
  - 66: `total 275`（A比 `497 -> 275`）
  - 68: `total 1220`（A比 `1663 -> 1220`）
  - 合計: `2160 -> 1495`（`-665`）

## 今回有効だった変更
- `AozoraTextFinalizer`:
  - 見出し数字は「2桁のみ縦中横」、3桁以上は全角数字化。
  - 英字判定を ASCII 連続文字列ベースに変更（日本語混在語の過変換抑制）。
- `chuki_tag.txt`:
  - `縦中横` の出力タグをマスター側に合わせて二重ネスト化。
- サイト別設定:
  - `web/setting_narourb.narou.ini`
    - `enable_alphabet_force_zenkaku = true`
    - `enable_erase_introduction = true`
    - `enable_erase_postscript = true`
  - `web/setting_narourb.kakuyomu.ini`
    - `enable_convert_num_to_kanji = true`

## 試して悪化した変更（採用しない）
- `!?` 後の区切り空白を半角化（`InsertSeparateSpace`）:
  - 68 が `1220 -> 1302` に悪化。
  - 既に切り戻し済み。

## 残課題（次に詰める順）
1. `fullsp` 相当の空白表現差分（`<span class="fullsp"> </span>` になる箇所）
2. 場面転換行（`◇`/`◆`）前後の空行保持ルール
3. 中表紙・行間・行頭段落処理の最終検証

## 実運用メモ
- 比較作業は `_compare/` 配下を使用（生成物はコミット対象外）。
- CLI 実行は `--no-build` 利用時に設定差し替えが反映されないことがあるため、
  設定変更直後は `dotnet build` 後に再生成する。
