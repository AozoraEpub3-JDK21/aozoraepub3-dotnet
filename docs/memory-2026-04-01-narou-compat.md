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
- 2026-04-01 追記（キャッシュ比較運用 + kakuyomu再調整後）:
  - 66: `total(norm) 2899`（live本文更新影響が支配的）
  - 68: `total(norm) 83`（`283 -> 83` に改善）

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
    - `enable_auto_join_in_brackets = true`
      - カクヨム由来の「かぎ括弧内が段落分断されるケース」を吸収

## 試して悪化した変更（採用しない）
- `!?` 後の区切り空白を半角化（`InsertSeparateSpace`）:
  - 68 が `1220 -> 1302` に悪化。
  - 既に切り戻し済み。

## 残課題（次に詰める順）
1. 66の巨大差分は live本文更新の影響切り分け（同時点master再生成 or 比較対象固定）
2. 68残差分（約83）:
   - `fullsp` 相当の空白表現差分
   - 区切り線（`──────────`）前後の単一空行保持
3. 中表紙・行間・行頭段落処理の最終検証

## 実運用メモ
- 比較作業は `_compare/` 配下を使用（生成物はコミット対象外）。
- CLI 実行は `--no-build` 利用時に設定差し替えが反映されないことがあるため、
  設定変更直後は `dotnet build` 後に再生成する。
- URL再取得の非効率対策として、CLI に `--web-cache-mode` / `--web-cache-dir` を追加済み。
  - 事前ウォーム: `tests/integration/warm-web-cache.ps1`
  - 比較実行: `tests/integration/compare-with-master-cache.ps1 -Warmup`

## 2026-04-01 追記（narou互換の再調整）
- 比較結果（cache compare）
  - 66: `total(norm) 2809`（live本文更新影響が支配的）
  - 68: `total(norm) 36`（前回 `48` から改善）
- 今回採用した修正
  - `AozoraTextFinalizer` に narou.rb互換の数字再変換処理を追加
    - 数字間の `.`/`．` を `・` に正規化
    - 漢数字化後、英字/単位に隣接する数字を全角アラビア数字へ再変換
  - 代表改善: `OPS/xhtml/0048.xhtml` の `3m/1.2m/1cm` 系差分を解消
- 試して戻した修正
  - note-like 行前の空行2行強制（68が `48 -> 132` に悪化）
  - WebAozoraConverter の連続空行保持（66を悪化させるため取り消し）
- 現在の主要残差（68）
  - `OPS/xhtml/0098.xhtml` (5)
  - `OPS/xhtml/0013.xhtml` (4)
  - 傾向: 末尾付近の空行1行不足（区切り線/末尾コメント直前）

## 2026-04-02 追記（続き）
- 比較結果（cache compare, same cache dir）
  - 66: `total(norm) 2809`（据え置き）
  - 68: `total(norm) 28`（前回 `36` から改善）
- 今回採用した修正（`AozoraTextFinalizer`）
  - 章末コメント周辺の空行補完（限定ルール）を維持
  - 二重引用符の正規化を追加
    - `「”` → `「“`
    - `「〟` → `「〝`
    - 「`＝` を含む引用（呪文表記）」を `〝...〟` へ正規化
- 効果
  - カクヨム側で多発していた `〟...〟` / `"..."` 系の差分を大幅削減
  - 代表値: `68 total(norm) 50 -> 35 -> 28`
- 現在の主要残差（68）
  - `OPS/xhtml/0013.xhtml` (4): 評価文直前の空行1行差
  - `OPS/xhtml/0055.xhtml` (2): `ラード` vs `獣脂` など本文語彙差
  - `OPS/xhtml/0052.xhtml` (2): `で会う/出会う`, `ウオーター/ウォーター` など本文語彙差
  - その他 1行差が複数（本文表記揺れ・記号差）

## 2026-04-02 追記（最終）
- 比較結果（cache compare, 最終確認）
  - 66: `total(norm) 2809`（据え置き）
  - 68: `total(norm) 28`（今回の到達ベスト）
- 今回の有効修正（維持）
  - `AozoraTextFinalizer`:
    - 章末コメント周辺の空行補完（限定ルール）
    - 呪文引用の互換正規化（`「”/「〟` と `"...＝..."` 系を `〝...〟` に寄せる）
- 試して戻した修正（悪化）
  - 比較記号 `＜/＞` を `〈/〉` に全体寄せ
  - 文中全角空白の広域除去
  - 評価文（`☆とか♥...`）直前の空行2行強制
  - いずれも 68 が悪化（例: `28 -> 32` や `28 -> 395`）したため不採用
- 残差28の内訳傾向
  - 本文語彙差（サイト本文側の差分）:
    - 例: `ラード/獣脂`, `で会う/出会う`, `ウオーター/ウォーター`
  - 軽微な空白・記号差が単発で散在
  - 互換ロジックを広げると副作用が先に出るため、現時点では `28` を安定ベースラインとして扱う
