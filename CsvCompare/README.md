# CSV Compare Tool

WinMergeのCSV比較用途を参考に、C# / Windows Formsで独立実装した最小構成のCSV比較ツールです。

## 動作環境
- Windows 10 / 11
- Visual Studio 2022
- .NET 8 SDK

## 起動方法
1. `CsvCompare.sln` をVisual Studio 2022で開く
2. NuGetパッケージの復元を行う
3. スタートアッププロジェクトを `CsvCompare` にして実行する
4. 比較元・比較先のCSVを選択し、「比較」を押す

## 比較仕様
このツールは **行位置による比較** です。
- 1行目と1行目、2行目と2行目、というように同じ行番号同士を比較します。
- 行の並べ替えは考慮しません。並べ替えがあると、その位置以降が変更として検出される場合があります。
- 左右の行数が異なる場合、余った側の行を追加・削除として扱います。
- セルの値を文字列として完全一致比較します。空白除去や大文字小文字の無視はしません。
- CSVの先頭行も通常のデータ行として比較します（ヘッダー専用扱いにはしません）。

## 色の意味
- 黄色: 同じ行番号で値が異なるセル
- 緑: 比較先にのみ存在する行（追加）
- 赤: 比較元にのみ存在する行（削除）

## 対応している読み込み
- UTF-8（BOMあり / なし）
- UTF-16 LE / BE（BOMあり）
- Shift-JIS / Windows-31J（BOMなしでUTF-8として解釈できない場合）

CSVのダブルクォート、カンマを含む項目、項目内改行、`""` による引用符エスケープを解析します。

## フォルダー構成

```text
CsvCompare/
├─ CsvCompare.sln
└─ CsvCompare/
   ├─ Program.cs
   ├─ Models/
   │  ├─ CsvDocument.cs
   │  └─ RowComparison.cs
   ├─ Services/
   │  ├─ CsvReader.cs
   │  └─ CsvComparator.cs
   └─ Forms/
      └─ MainForm.cs
```

## クラスの責務
| クラス | 責務 |
|---|---|
| `CsvReader` | ファイルの文字コード判定とCSV解析 |
| `CsvDocument` | 読み込んだCSVのファイル名と行データを保持 |
| `CsvComparator` | 行番号を基準に左右の行・セルを比較 |
| `RowComparison` | 1行分の比較結果と差分列を保持 |
| `MainForm` | ファイル選択、比較実行、結果表示 |

## 処理の流れ

```text
MainForm
  ├─ CsvReader.Read(比較元)
  ├─ CsvReader.Read(比較先)
  ├─ CsvComparator.Compare(左の行, 右の行)
  └─ 比較結果をDataGridViewへ表示
```

## 現時点の制限
- 区切り文字はカンマ固定です。
- 文字コードはBOMおよびUTF-8妥当性による簡易判定です。文字化けするファイルは今後、文字コードを選択できる機能を追加してください。
- 大容量CSV向けの仮想モード表示や非同期読み込みは未実装です。
- 追加・削除は行位置を基準に判定するため、途中に行が挿入された場合は後続行も変更として見えることがあります。
- 比較結果のエクスポート、行単位の挿入・削除を追跡する高度な差分アルゴリズムは未実装です。
