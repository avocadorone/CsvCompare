using System.Drawing;
using CsvCompare.Models;
using CsvCompare.Services;

namespace CsvCompare.Forms;

public sealed class MainForm : Form
{
    private readonly TextBox _leftPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _rightPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly DataGridView _leftGrid = CreateGrid();
    private readonly DataGridView _rightGrid = CreateGrid();
    private readonly CheckBox _diffOnly = new() { Text = "差分のみ表示", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, Text = "左右のCSVを選択してください。" };

    private CsvDocument? _left;
    private CsvDocument? _right;
    private IReadOnlyList<RowComparison> _comparisons = Array.Empty<RowComparison>();

    public MainForm()
    {
        Text = "CSV Compare Tool";
        Width = 1400;
        Height = 850;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var leftBrowse = new Button { Text = "参照...", AutoSize = true };
        var rightBrowse = new Button { Text = "参照...", AutoSize = true };
        var compare = new Button { Text = "比較", AutoSize = true, Padding = new Padding(12, 4, 12, 4) };

        leftBrowse.Click += (_, _) => SelectFile(true);
        rightBrowse.Click += (_, _) => SelectFile(false);
        compare.Click += (_, _) => CompareFiles();
        _diffOnly.CheckedChanged += (_, _) => RenderResults();

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8),
            ColumnCount = 4, RowCount = 2
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        top.Controls.Add(new Label { Text = "比較元：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        top.Controls.Add(_leftPath, 1, 0);
        top.Controls.Add(leftBrowse, 2, 0);
        top.Controls.Add(new Label { Text = "比較先：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        top.Controls.Add(_rightPath, 1, 1);
        top.Controls.Add(rightBrowse, 2, 1);
        top.Controls.Add(compare, 3, 0);
        top.Controls.Add(_diffOnly, 3, 1);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            //Panel1MinSize = 250,
            //SplitterDistance = 680,
            //Panel2MinSize = 250
        };
        split.Panel1.Controls.Add(_leftGrid);
        split.Panel2.Controls.Add(_rightGrid);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(8, 5, 8, 0) };
        bottom.Controls.Add(_status);

        Controls.Add(split);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Color.White,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        MultiSelect = false,
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
    };

    private void SelectFile(bool isLeft)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
            Title = isLeft ? "比較元CSVを選択" : "比較先CSVを選択"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (isLeft) _leftPath.Text = dialog.FileName;
        else _rightPath.Text = dialog.FileName;
    }

    private void CompareFiles()
    {
        if (String.IsNullOrWhiteSpace(_leftPath.Text) || String.IsNullOrWhiteSpace(_rightPath.Text))
        {
            MessageBox.Show(this, "比較元と比較先のCSVを選択してください。", "確認",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var reader = new CsvReader();
            _left = reader.Read(_leftPath.Text);
            _right = reader.Read(_rightPath.Text);
            _comparisons = new CsvComparator().Compare(_left.Rows, _right.Rows);
            RenderResults();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CSV読み込みエラー",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RenderResults()
    {
        if (_left is null || _right is null) return;

        _leftGrid.Columns.Clear();
        _rightGrid.Columns.Clear();
        _leftGrid.Rows.Clear();
        _rightGrid.Rows.Clear();

        int maxColumns = Math.Max(
            _left.Rows.Concat(_right.Rows).Select(r => r.Length).DefaultIfEmpty(0).Max(), 1);

        for (int c = 0; c < maxColumns; c++)
        {
            string name = $"列 {c + 1}";
            _leftGrid.Columns.Add($"L{c}", name);
            _rightGrid.Columns.Add($"R{c}", name);
        }

        int same = 0, changed = 0, added = 0, deleted = 0;
        foreach (var item in _comparisons)
        {
            switch (item.Difference)
            {
                case DifferenceType.Same: same++; break;
                case DifferenceType.Changed: changed++; break;
                case DifferenceType.Added: added++; break;
                case DifferenceType.Deleted: deleted++; break;
            }

            if (_diffOnly.Checked && item.Difference == DifferenceType.Same) continue;

            int li = _leftGrid.Rows.Add(MakeCells(item.Left, maxColumns));
            int ri = _rightGrid.Rows.Add(MakeCells(item.Right, maxColumns));

            _leftGrid.Rows[li].HeaderCell.Value = item.RowNumber.ToString();
            _rightGrid.Rows[ri].HeaderCell.Value = item.RowNumber.ToString();

            if (item.Difference == DifferenceType.Added)
                _rightGrid.Rows[ri].DefaultCellStyle.BackColor = Color.FromArgb(218, 242, 218);
            else if (item.Difference == DifferenceType.Deleted)
                _leftGrid.Rows[li].DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
            else if (item.Difference == DifferenceType.Changed)
            {
                foreach (int c in item.ChangedColumns)
                {
                    if (c < maxColumns)
                    {
                        _leftGrid.Rows[li].Cells[c].Style.BackColor = Color.FromArgb(255, 242, 180);
                        _rightGrid.Rows[ri].Cells[c].Style.BackColor = Color.FromArgb(255, 242, 180);
                    }
                }
            }
        }

        _status.Text = $"行数: {_comparisons.Count}   一致: {same}   変更: {changed}   追加: {added}   削除: {deleted}";
    }

    private static object[] MakeCells(string[]? row, int count)
    {
        var values = new object[count];
        for (int i = 0; i < count; i++)
            values[i] = row is not null && i < row.Length ? row[i] : String.Empty;
        return values;
    }
}
