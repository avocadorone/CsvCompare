namespace CsvCompare.Models;

/// <summary>
/// CSVの比較結果を表す列挙型。
/// 行単位でどのような差分が発生したかを管理する。
/// </summary>
public enum DifferenceType
{
    /// <summary>
    /// 左右の行の内容が完全に一致している。
    /// </summary>
    Same,

    /// <summary>
    /// 左右の行は存在するが、一部の列の値が異なる。
    /// </summary>
    Changed,

    /// <summary>
    /// 比較先にのみ存在する行。
    /// </summary>
    Added,

    /// <summary>
    /// 比較元にのみ存在する行。
    /// </summary>
    Deleted
}

/// <summary>
/// CSVの1行分の比較結果を保持するクラス。
/// 比較元・比較先の行データと、差分の種類、変更された列を管理する。
/// </summary>
public sealed class RowComparison
{
    /// <summary>
    /// 比較対象の行番号（1始まり）。
    /// </summary>
    public int RowNumber { get; init; }

    /// <summary>
    /// 比較元CSVの行データ。
    /// 追加された行の場合はnull。
    /// </summary>
    public string[]? Left { get; init; }

    /// <summary>
    /// 比較先CSVの行データ。
    /// 削除された行の場合はnull。
    /// </summary>
    public string[]? Right { get; init; }

    /// <summary>
    /// 行単位の比較結果。
    /// 一致・変更・追加・削除のいずれかを保持する。
    /// </summary>
    public DifferenceType Difference { get; init; }

    /// <summary>
    /// 値が変更された列番号の集合。
    /// 列番号は0始まり。
    /// 例：0列目と2列目が変更された場合は { 0, 2 }。
    /// </summary>
    public HashSet<int> ChangedColumns { get; init; } = new();
}