using CsvCompare.Models;
using CsvCompare.Services;
using CsvCompare.Utilities;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Drawing.Drawing2D;
using CsvCompare.Services;

namespace CsvCompare.Forms;

/// <summary>
/// CSV比較画面を管理するフォームクラス。
/// CSVファイルの選択、比較処理、比較結果の表示を担当する。
/// </summary>
public sealed class MainForm : Form
{
    #region クラス変数
    // 比較元CSVのファイルパスを表示するテキストボックス
    private readonly TextBox _leftPath = new() { ReadOnly = true, Dock = DockStyle.Fill };

    // 比較先CSVのファイルパスを表示するテキストボックス
    private readonly TextBox _rightPath = new() { ReadOnly = true, Dock = DockStyle.Fill };

    // 比較元CSVの内容を表示するグリッド
    private readonly DataGridView _leftGrid = CreateGrid();

    // 比較先CSVの内容を表示するグリッド
    private readonly DataGridView _rightGrid = CreateGrid();

    // チェックすると、一致している行を非表示にする
    private readonly CheckBox _diffOnly = new() { Text = "差分のみ表示", AutoSize = true };

    // 比較結果の件数や現在の状態を表示するラベル
    private readonly Label _status = new() { AutoSize = true, Text = "左右のCSVを選択してください。" };

    // 読み込んだ比較元CSVのデータ
    private CsvDocument? _left;

    // 読み込んだ比較先CSVのデータ
    private CsvDocument? _right;

    // CSVの行単位で比較した結果を保持する
    private IReadOnlyList<RowComparison> _comparisons = Array.Empty<RowComparison>();

    // 手動で対応付けた行を保持する。
    // Key：比較元の行インデックス（0始まり）
    // Value：比較先の行インデックス（0始まり）
    private readonly Dictionary<int, int> _manualPairs = new();
    #endregion

    #region public

    #region フォームの初期化処理。
    /// <summary>
    /// フォームの初期化処理。
    /// 画面の基本設定、各種コントロールの配置、イベント登録を行う。
    /// </summary>
    public MainForm()
    {
        // フォームの基本設定
        Text = "CSV Compare Tool";
        Width = 1400;
        Height = 850;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // CSVファイル選択ボタンと比較実行ボタンを作成
        var leftBrowse = new Button { Text = "参照...", AutoSize = true };
        var rightBrowse = new Button { Text = "参照...", AutoSize = true };
        var compare = new Button { Text = "比較", AutoSize = true, Padding = new Padding(12, 4, 12, 4) };

        // ボタンクリック時のイベントを登録
        leftBrowse.Click += (_, _) => SelectFile(true);
        rightBrowse.Click += (_, _) => SelectFile(false);
        compare.Click += (_, _) => CompareFiles();

        // 差分のみ表示のチェック状態が変更されたら、表示内容を再描画
        _diffOnly.CheckedChanged += (_, _) => RenderResults();

        // 上部のファイル選択エリアを作成
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            ColumnCount = 4,
            RowCount = 3
        };

        // 各列の幅を設定
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // 比較元CSVの選択欄を配置
        top.Controls.Add(new Label { Text = "比較元：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        top.Controls.Add(_leftPath, 1, 0);
        top.Controls.Add(leftBrowse, 2, 0);

        // 比較先CSVの選択欄を配置
        top.Controls.Add(new Label { Text = "比較先：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        top.Controls.Add(_rightPath, 1, 1);
        top.Controls.Add(rightBrowse, 2, 1);

        // 比較ボタンと差分表示チェックボックスを配置
        top.Controls.Add(compare, 3, 0);
        top.Controls.Add(_diffOnly, 3, 1);

        // 行操作・コピー用のボタンを配置するパネル
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        // 3行目にボタン群を配置
        top.Controls.Add(actions, 0, 2);

        // 4列分の幅を使用する
        top.SetColumnSpan(actions, 4);

        var pairRows = new Button
        {
            Text = "選択行を対応付け",
            AutoSize = true
        };

        var unpairRows = new Button
        {
            Text = "対応付け解除",
            AutoSize = true
        };

        var copyResults = new Button
        {
            Text = "Excel用にコピー",
            AutoSize = true
        };

        pairRows.Click += (_, _) => PairSelectedRows();
        unpairRows.Click += (_, _) => UnpairSelectedRows();
        copyResults.Click += (_, _) => CopyResultsToClipboard();
        // セル内の文字単位の差分を描画する
        _leftGrid.CellPainting += Grid_CellPainting;
        _rightGrid.CellPainting += Grid_CellPainting;

        actions.Controls.Add(pairRows);
        actions.Controls.Add(unpairRows);
        actions.Controls.Add(copyResults);


        // 左右のCSV表示領域を分割するコンテナを作成
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,

            // Panel1MinSize = 250,
            // SplitterDistance = 680,
            // Panel2MinSize = 250
        };

        // 左右それぞれのパネルにCSV表示用グリッドを配置
        split.Panel1.Controls.Add(_leftGrid);
        split.Panel2.Controls.Add(_rightGrid);

        // 画面下部のステータス表示エリアを作成
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            Padding = new Padding(8, 5, 8, 0)
        };

        bottom.Controls.Add(_status);

        // フォームに各エリアを追加
        Controls.Add(split);
        Controls.Add(bottom);
        Controls.Add(top);
    }
    #endregion

    #endregion

    #region private

    #region CSV表示用のDataGridViewを生成する。
    /// <summary>
    /// CSV表示用のDataGridViewを生成する。
    /// 左右のグリッドで共通する表示設定を定義する。
    /// </summary>
    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,

        // 表示専用とし、セルの編集や行の追加・削除を禁止
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,

        // 行番号を表示するためのヘッダーを有効化
        RowHeadersVisible = true,

        // 表示されているセルの内容に合わせて列幅を自動調整
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,

        // グリッドの背景色を白に設定
        BackgroundColor = Color.White,

        // セル単位で選択できるように設定
        //SelectionMode = DataGridViewSelectionMode.CellSelect,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,

        // コピー時にヘッダーも含める
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
    };
    #endregion

    #region CSVファイル選択ダイアログを表示する。
    /// <summary>
    /// CSVファイル選択ダイアログを表示する。
    /// 選択されたファイルパスを比較元または比較先に設定する。
    /// </summary>
    /// <param name="isLeft">trueの場合は比較元、falseの場合は比較先</param>
    private void SelectFile(bool isLeft)
    {
        // ファイル選択ダイアログを作成
        using var dialog = new OpenFileDialog
        {
            Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
            Title = isLeft ? "比較元CSVを選択" : "比較先CSVを選択"
        };

        // ファイルが選択されなかった場合は処理を終了
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // 選択されたファイルパスを対応するテキストボックスに設定
        if (isLeft)
            _leftPath.Text = dialog.FileName;
        else
            _rightPath.Text = dialog.FileName;
    }
    #endregion

    #region CSVファイルの読み込みと比較を実行する。
    /// <summary>
    /// CSVファイルの読み込みと比較を実行する。
    /// 比較結果を取得した後、画面に反映する。
    /// </summary>
    private void CompareFiles()
    {
        // 比較元・比較先の両方が選択されているか確認
        if (String.IsNullOrWhiteSpace(_leftPath.Text) || String.IsNullOrWhiteSpace(_rightPath.Text))
        {
            MessageBox.Show(this, "比較元と比較先のCSVを選択してください。", "確認",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            // CSV読み込み用のサービスを生成
            var reader = new CsvReader();

            // 比較元と比較先のCSVファイルを読み込む
            _left = reader.Read(_leftPath.Text);
            _right = reader.Read(_rightPath.Text);

            // *** BFR-START 2026/11/07
            //// 2つのCSVの行データを比較し、比較結果を取得
            //_comparisons = new CsvComparator().Compare(_left.Rows, _right.Rows);
            //
            //// 比較結果を画面に表示
            //RenderResults();
            // *** BFR-END   2026/11/07
            // *** AFT-START 2026/11/07
            // 新しく比較するときは、以前の手動対応付けを解除する
            _manualPairs.Clear();

            // 比較処理を実行
            UpdateComparison();
            // *** AFT-END   2026/11/07fCompareFiles
        }
        catch (Exception ex)
        {
            // 読み込みや比較処理でエラーが発生した場合はメッセージを表示
            MessageBox.Show(this, ex.Message, "CSV読み込みエラー",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    #endregion

    #region 手動対応付けを反映して再比較する。
    /// <summary>
    /// 現在の手動対応付けを反映して比較処理を実行する。
    /// </summary>
    private void UpdateComparison()
    {
        if (_left is null || _right is null)
            return;

        // 手動対応付け情報を渡して比較
        _comparisons = new CsvComparator().Compare(
            _left.Rows,
            _right.Rows,
            _manualPairs);

        // 比較結果を画面に反映
        RenderResults();
    }
    #endregion

    #region 選択した左右の行を対応付ける。
    /// <summary>
    /// 左右のグリッドで選択されている行を対応付ける。
    /// </summary>
    private void PairSelectedRows()
    {
        if (_leftGrid.CurrentRow?.Tag is not RowComparison leftItem ||
            !leftItem.LeftRowIndex.HasValue)
        {
            MessageBox.Show(this, "比較元の行を選択してください。");
            return;
        }

        if (_rightGrid.CurrentRow?.Tag is not RowComparison rightItem ||
            !rightItem.RightRowIndex.HasValue)
        {
            MessageBox.Show(this, "比較先の行を選択してください。");
            return;
        }

        int leftIndex = leftItem.LeftRowIndex.Value;
        int rightIndex = rightItem.RightRowIndex.Value;

        // 既存の対応付けを解除
        _manualPairs.Remove(leftIndex);

        var existingKeys = _manualPairs
            .Where(x => x.Value == rightIndex)
            .Select(x => x.Key)
            .ToList();

        foreach (int key in existingKeys)
            _manualPairs.Remove(key);

        // 新しい対応付けを登録
        _manualPairs[leftIndex] = rightIndex;

        UpdateComparison();
    }
    #endregion

    #region 選択した行の対応付けを解除する。
    /// <summary>
    /// 選択した左右の行に設定されている手動対応付けを解除する。
    /// </summary>
    private void UnpairSelectedRows()
    {
        RowComparison? leftItem =
            _leftGrid.CurrentRow?.Tag as RowComparison;

        RowComparison? rightItem =
            _rightGrid.CurrentRow?.Tag as RowComparison;

        int? leftIndex = leftItem?.LeftRowIndex;
        int? rightIndex = rightItem?.RightRowIndex;

        var removeKeys = _manualPairs
            .Where(x =>
                (leftIndex.HasValue && x.Key == leftIndex.Value) ||
                (rightIndex.HasValue && x.Value == rightIndex.Value))
            .Select(x => x.Key)
            .ToList();

        if (removeKeys.Count == 0)
        {
            MessageBox.Show(
                this,
                "選択した行には手動対応付けがありません。");
            return;
        }

        foreach (int key in removeKeys)
            _manualPairs.Remove(key);

        UpdateComparison();
    }
    #endregion

    #region 比較結果をExcel用にコピーする。
    /// <summary>
    /// 現在の比較結果をExcelに貼り付けられる形式でコピーする。
    /// </summary>
    private void CopyResultsToClipboard()
    {
        if (_left is null || _right is null || _comparisons.Count == 0)
        {
            MessageBox.Show(
                this,
                "先にCSVを比較してください。",
                "確認",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // 左右のCSVに含まれる最大列数を取得
        int maxColumns = Math.Max(
            _left.Rows.Concat(_right.Rows)
                .Select(row => row.Length)
                .DefaultIfEmpty(0)
                .Max(),
            1);

        // Excel用のタブ区切り文字列を生成
        string tsv = ClipboardHelper.CreateComparisonTsv(
            _comparisons,
            maxColumns,
            _diffOnly.Checked);

        try
        {
            // Unicode形式でクリップボードに設定
            Clipboard.SetText(tsv, TextDataFormat.UnicodeText);

            _status.Text = "比較結果をクリップボードにコピーしました。Excelで貼り付けできます。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"クリップボードへのコピーに失敗しました。\n{ex.Message}",
                "コピーエラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
    #endregion

    #region CSVの比較結果を画面に描画する。
    /// <summary>
    /// CSVの比較結果を画面に描画する。
    /// 差分の種類に応じて背景色を変更し、件数を集計する。
    /// </summary>
    private void RenderResults()
    {
        // 比較元または比較先のデータが存在しない場合は描画しない
        if (_left is null || _right is null) return;

        // グリッドの既存の列・行をクリア
        _leftGrid.Columns.Clear();
        _rightGrid.Columns.Clear();
        _leftGrid.Rows.Clear();
        _rightGrid.Rows.Clear();

        // 左右のCSVに含まれる列数の最大値を取得
        // 最低1列は表示する
        int maxColumns = Math.Max(
            _left.Rows.Concat(_right.Rows).Select(r => r.Length).DefaultIfEmpty(0).Max(), 1);

        // 左右のグリッドに列を追加
        for (int c = 0; c < maxColumns; c++)
        {
            string name = $"列 {c + 1}";

            _leftGrid.Columns.Add($"L{c}", name);
            _rightGrid.Columns.Add($"R{c}", name);
        }

        // 差分の種類ごとの件数を初期化
        int same = 0, changed = 0, added = 0, deleted = 0;

        // 比較結果を1行ずつ処理
        foreach (var item in _comparisons)
        {
            // 差分の種類に応じて件数を集計
            switch (item.Difference)
            {
                case DifferenceType.Same:
                    same++;
                    break;

                case DifferenceType.Changed:
                    changed++;
                    break;

                case DifferenceType.Added:
                    added++;
                    break;

                case DifferenceType.Deleted:
                    deleted++;
                    break;
            }

            // 差分のみ表示が有効で、一致している行の場合は表示をスキップ
            if (_diffOnly.Checked && item.Difference == DifferenceType.Same)
                continue;

            // 比較元・比較先それぞれのグリッドに行を追加
            int li = _leftGrid.Rows.Add(MakeCells(item.Left, maxColumns));
            int ri = _rightGrid.Rows.Add(MakeCells(item.Right, maxColumns));

            //// 行ヘッダーに比較対象の行番号を設定
            //_leftGrid.Rows[li].HeaderCell.Value = item.RowNumber.ToString();
            //_rightGrid.Rows[ri].HeaderCell.Value = item.RowNumber.ToString();

            // *** BFR-START 2026/11/07
            //// 左右のグリッド行に、元CSVの行インデックスを保持する
            //// この情報を使って、選択行の対応付けを行う
            //_leftGrid.Rows[li].Tag = item.LeftRowIndex;
            //_rightGrid.Rows[ri].Tag = item.RightRowIndex;
            // *** BFR-END   2026/11/07
            // *** AFT-START 2026/11/07
            // 描画時に比較結果を参照できるように保持
            _leftGrid.Rows[li].Tag = item;
            _rightGrid.Rows[ri].Tag = item;
            // *** AFT-END   2026/11/07

            // 左右それぞれの元CSVの行番号をヘッダーに表示
            _leftGrid.Rows[li].HeaderCell.Value =
                item.LeftRowIndex.HasValue
                    ? (item.LeftRowIndex.Value + 1).ToString()
                    : string.Empty;

            _rightGrid.Rows[ri].HeaderCell.Value =
                item.RightRowIndex.HasValue
                    ? (item.RightRowIndex.Value + 1).ToString()
                    : string.Empty;

            // 追加された行は比較先側を緑色で表示
            if (item.Difference == DifferenceType.Added)
            {
                _rightGrid.Rows[ri].DefaultCellStyle.BackColor = Color.FromArgb(218, 242, 218);
            }

            // 削除された行は比較元側を赤色で表示
            else if (item.Difference == DifferenceType.Deleted)
            {
                _leftGrid.Rows[li].DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
            }

            // 変更された行は、変更があったセルだけを黄色で表示
            else if (item.Difference == DifferenceType.Changed)
            {
                foreach (int c in item.ChangedColumns)
                {
                    // 有効な列番号であることを確認
                    if (c < maxColumns)
                    {
                        _leftGrid.Rows[li].Cells[c].Style.BackColor = Color.FromArgb(255, 242, 180);
                        _rightGrid.Rows[ri].Cells[c].Style.BackColor = Color.FromArgb(255, 242, 180);
                    }
                }
            }
        }

        // 比較結果の件数を画面下部に表示
        _status.Text = $"行数: {_comparisons.Count}   一致: {same}   変更: {changed}   追加: {added}   削除: {deleted}";
    }
    #endregion

    #region CSVの1行分のデータをDataGridViewに設定できる配列に変換する。
    /// <summary>
    /// CSVの1行分のデータをDataGridViewに設定できる配列に変換する。
    /// CSVの列数が不足している場合は空文字で補完する。
    /// </summary>
    /// <param name="row">変換対象のCSV行データ</param>
    /// <param name="count">変換後の列数</param>
    /// <returns>DataGridViewに設定するセル値の配列</returns>
    private static object[] MakeCells(string[]? row, int count)
    {
        // 指定された列数分の配列を作成
        var values = new object[count];

        // 各列の値を設定
        for (int i = 0; i < count; i++)
        {
            // 元データが存在する列はその値を設定し、
            // 存在しない列は空文字で補完する
            values[i] = row is not null && i < row.Length ? row[i] : String.Empty;
        }

        return values;
    }
    #endregion

    #region セル内の文字単位の差分を描画する。
    /// <summary>
    /// DataGridViewのセルを描画する。
    /// 変更されたセルについて、文字単位で差分を着色する。
    /// </summary>
    private void Grid_CellPainting(
        object? sender,
        DataGridViewCellPaintingEventArgs e)
    {
        // ヘッダー行・列は通常描画に任せる
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (sender is not DataGridView grid)
            return;

        // 行に保持している比較結果を取得
        if (grid.Rows[e.RowIndex].Tag is not RowComparison item)
            return;

        // 変更されたセル以外は通常描画に任せる
        if (item.Difference != DifferenceType.Changed ||
            !item.ChangedColumns.Contains(e.ColumnIndex))
        {
            return;
        }

        // 比較元・比較先の値を取得
        string leftText = e.ColumnIndex < (item.Left?.Length ?? 0)
            ? item.Left![e.ColumnIndex]
            : string.Empty;

        string rightText = e.ColumnIndex < (item.Right?.Length ?? 0)
            ? item.Right![e.ColumnIndex]
            : string.Empty;

        // 文字単位の差分を抽出
        var diff = CellDiff.Compare(leftText, rightText);

        // 左右どちらのグリッドかによって描画内容を切り替える
        IReadOnlyList<TextSegment> segments =
            ReferenceEquals(grid, _leftGrid)
                ? diff.Left
                : diff.Right;

        // セルの背景を描画
        e.PaintBackground(e.ClipBounds, true);

        // セルの余白を考慮した描画領域
        Rectangle bounds = e.CellBounds;

        int leftPadding = e.CellStyle.Padding.Left + 3;
        int rightPadding = e.CellStyle.Padding.Right + 3;

        int x = bounds.Left + leftPadding;
        int y = bounds.Top;

        int availableWidth = Math.Max(
            0,
            bounds.Width - leftPadding - rightPadding);

        // セル内で描画する範囲を制限
        Rectangle textBounds = new(
            x,
            bounds.Top,
            availableWidth,
            bounds.Height);

        GraphicsState state = e.Graphics.Save();

        try
        {
            e.Graphics.SetClip(textBounds);

            // 縦方向の文字位置を中央に合わせる
            Size sampleSize = TextRenderer.MeasureText(
                "あ",
                e.CellStyle.Font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            int textY = y + (bounds.Height - sampleSize.Height) / 2;

            // 水平方向の配置を考慮
            int totalWidth = segments.Sum(segment =>
                TextRenderer.MeasureText(
                    segment.Text,
                    segment.IsDifferent
                        ? new Font(e.CellStyle.Font, FontStyle.Bold)
                        : e.CellStyle.Font,
                    Size.Empty,
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine).Width);

            switch (e.CellStyle.Alignment)
            {
                case DataGridViewContentAlignment.MiddleRight:
                case DataGridViewContentAlignment.TopRight:
                case DataGridViewContentAlignment.BottomRight:
                    x = bounds.Right - rightPadding - totalWidth;
                    break;

                case DataGridViewContentAlignment.MiddleCenter:
                case DataGridViewContentAlignment.TopCenter:
                case DataGridViewContentAlignment.BottomCenter:
                    x = bounds.Left + (bounds.Width - totalWidth) / 2;
                    break;
            }

            // 区間ごとに文字を描画
            foreach (var segment in segments)
            {
                bool isDifferent = segment.IsDifferent;

                // 差分文字列は太字にする
                Font font = isDifferent
                    ? new Font(e.CellStyle.Font, FontStyle.Bold)
                    : e.CellStyle.Font;

                try
                {
                    // 削除文字は赤、追加文字は緑
                    Color color = isDifferent
                        ? (ReferenceEquals(grid, _leftGrid)
                            ? Color.Firebrick
                            : Color.ForestGreen)
                        : e.CellStyle.ForeColor;

                    TextRenderer.DrawText(
                        e.Graphics,
                        segment.Text,
                        font,
                        new Point(x, textY),
                        color,
                        TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPrefix);

                    // 次の文字列の描画位置を進める
                    x += TextRenderer.MeasureText(
                        segment.Text,
                        font,
                        Size.Empty,
                        TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine).Width;
                }
                finally
                {
                    // 差分用に生成したフォントだけ破棄
                    if (isDifferent)
                        font.Dispose();
                }
            }
        }
        finally
        {
            e.Graphics.Restore(state);
        }

        // セルの枠線を描画
        e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

        // 標準描画を終了し、独自描画を採用
        e.Handled = true;
    }
    #endregion

    #endregion
}