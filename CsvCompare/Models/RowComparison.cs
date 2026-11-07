namespace CsvCompare.Models;

/// <summary>
/// CSVの比較結果を表す列挙型。
/// </summary>
public enum DifferenceType
{
    /// <summary>左右の行が一致している。</summary>
    Same,

    /// <summary>左右の行は存在するが、一部の値が異なる。</summary>
    Changed,

    /// <summary>比較先にのみ存在する行。</summary>
    Added,

    /// <summary>比較元にのみ存在する行。</summary>
    Deleted
}

/// <summary>
/// CSVの1行分の比較結果を保持するクラス。
/// </summary>
public sealed class RowComparison
{
    /// <summary>
    /// 比較結果一覧における表示順の行番号（1始まり）。
    /// </summary>
    public int RowNumber { get; init; }

    /// <summary>
    /// 比較元CSVの行インデックス（0始まり）。
    /// 行が存在しない場合はnull。
    /// </summary>
    public int? LeftRowIndex { get; init; }

    /// <summary>
    /// 比較先CSVの行インデックス（0始まり）。
    /// 行が存在しない場合はnull。
    /// </summary>
    public int? RightRowIndex { get; init; }

    /// <summary>
    /// 比較元CSVの行データ。追加行の場合はnull。
    /// </summary>
    public string[]? Left { get; init; }

    /// <summary>
    /// 比較先CSVの行データ。削除行の場合はnull。
    /// </summary>
    public string[]? Right { get; init; }

    /// <summary>
    /// 行単位の比較結果。
    /// </summary>
    public DifferenceType Difference { get; init; }

    /// <summary>
    /// 値が変更された列番号の集合（0始まり）。
    /// </summary>
    public HashSet<int> ChangedColumns { get; init; } = new();
}