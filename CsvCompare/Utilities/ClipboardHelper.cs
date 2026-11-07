using System.Text;
using CsvCompare.Models;

namespace CsvCompare.Utilities;

/// <summary>
/// CSV比較結果をクリップボードにコピーするためのユーティリティ。
/// Excelに貼り付けやすいタブ区切り形式を生成する。
/// </summary>
public static class ClipboardHelper
{
    /// <summary>
    /// 比較結果をExcel向けのタブ区切り文字列に変換する。
    /// </summary>
    public static string CreateComparisonTsv(
        IReadOnlyList<RowComparison> comparisons,
        int columnCount,
        bool diffOnly)
    {
        var builder = new StringBuilder();

        // ヘッダー行を作成
        var headers = new List<string>
        {
            "左行番号",
            "右行番号",
            "差分"
        };

        for (int i = 0; i < columnCount; i++)
            headers.Add($"左_列{i + 1}");

        for (int i = 0; i < columnCount; i++)
            headers.Add($"右_列{i + 1}");

        AppendLine(builder, headers);

        // 比較結果を1行ずつ出力
        foreach (var item in comparisons)
        {
            // 差分のみ表示が有効なら一致行を除外
            if (diffOnly && item.Difference == DifferenceType.Same)
                continue;

            var values = new List<string>
            {
                item.LeftRowIndex.HasValue
                    ? (item.LeftRowIndex.Value + 1).ToString()
                    : string.Empty,

                item.RightRowIndex.HasValue
                    ? (item.RightRowIndex.Value + 1).ToString()
                    : string.Empty,

                GetDifferenceName(item.Difference)
            };

            // 比較元の各列を追加
            for (int i = 0; i < columnCount; i++)
            {
                values.Add(
                    item.Left is not null && i < item.Left.Length
                        ? item.Left[i]
                        : string.Empty);
            }

            // 比較先の各列を追加
            for (int i = 0; i < columnCount; i++)
            {
                values.Add(
                    item.Right is not null && i < item.Right.Length
                        ? item.Right[i]
                        : string.Empty);
            }

            AppendLine(builder, values);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 1行分の値をタブ区切りで追加する。
    /// </summary>
    private static void AppendLine(
        StringBuilder builder,
        IReadOnlyList<string> values)
    {
        builder.AppendLine(String.Join(
            "\t",
            values.Select(EscapeCell)));
    }

    /// <summary>
    /// セル内のタブ・改行を可視文字に変換する。
    /// Excel貼り付け時に列や行が崩れるのを防ぐ。
    /// </summary>
    private static string EscapeCell(string? value)
    {
        if (String.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\t", "\\t")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    /// <summary>
    /// 差分の種類を日本語に変換する。
    /// </summary>
    private static string GetDifferenceName(DifferenceType difference)
    {
        return difference switch
        {
            DifferenceType.Same => "一致",
            DifferenceType.Changed => "変更",
            DifferenceType.Added => "追加",
            DifferenceType.Deleted => "削除",
            _ => "不明"
        };
    }


}