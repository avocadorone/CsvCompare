using System.Drawing;
using CsvCompare.Models;
using CsvCompare.Services;

namespace CsvCompare.Forms;

/// <summary>
/// CSV比較画面を管理するフォームクラス。
/// CSVファイルの選択、比較処理、比較結果の表示を担当する。
/// </summary>
public sealed class MainForm : Form
{
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
            RowCount = 2
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
        SelectionMode = DataGridViewSelectionMode.CellSelect,
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

            // 2つのCSVの行データを比較し、比較結果を取得
            _comparisons = new CsvComparator().Compare(_left.Rows, _right.Rows);

            // 比較結果を画面に表示
            RenderResults();
        }
        catch (Exception ex)
        {
            // 読み込みや比較処理でエラーが発生した場合はメッセージを表示
            MessageBox.Show(this, ex.Message, "CSV読み込みエラー",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            // 行ヘッダーに比較対象の行番号を設定
            _leftGrid.Rows[li].HeaderCell.Value = item.RowNumber.ToString();
            _rightGrid.Rows[ri].HeaderCell.Value = item.RowNumber.ToString();

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
}