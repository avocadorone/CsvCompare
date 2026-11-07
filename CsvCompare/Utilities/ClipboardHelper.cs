using System.Text;
using System.Net;
using CsvCompare.Models;
using CsvCompare.Services;

namespace CsvCompare.Utilities;

/// <summary>
/// CSV比較結果をクリップボードにコピーするユーティリティ。
/// HTML形式とテキスト形式の両方を提供する。
/// </summary>
public static class ClipboardHelper
{
    /// <summary>
    /// Excel向けのHTML形式を生成する。
    /// セルの背景色、文字色、太字、罫線を保持する。
    /// </summary>
    public static string CreateComparisonHtml(
        IReadOnlyList<RowComparison> comparisons,
        int columnCount,
        bool diffOnly)
    {
        var html = new StringBuilder();

        html.Append("""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            </head>
            <body>
            <!--StartFragment-->
            <table style="border-collapse:collapse;font-family:Meiryo,sans-serif;font-size:10pt;">
            <tr>
            <th style="border:1px solid #999;padding:5px;background-color:#D9EAF7;">左行番号</th>
            <th style="border:1px solid #999;padding:5px;background-color:#D9EAF7;">右行番号</th>
            <th style="border:1px solid #999;padding:5px;background-color:#D9EAF7;">差分</th>
            """);

        // 左右の列見出しを作成
        for (int i = 0; i < columnCount; i++)
        {
            html.Append(
                $"<th style=\"border:1px solid #999;padding:5px;background-color:#D9EAF7;\">左_列{i + 1}</th>");
        }

        for (int i = 0; i < columnCount; i++)
        {
            html.Append(
                $"<th style=\"border:1px solid #999;padding:5px;background-color:#D9EAF7;\">右_列{i + 1}</th>");
        }

        html.Append("</tr>");

        // 比較結果を1行ずつHTMLに変換
        foreach (var item in comparisons)
        {
            if (diffOnly && item.Difference == DifferenceType.Same)
                continue;

            html.Append("<tr>");

            // 左右の行番号
            AppendHtmlCell(
                html,
                item.LeftRowIndex.HasValue
                    ? (item.LeftRowIndex.Value + 1).ToString()
                    : string.Empty);

            AppendHtmlCell(
                html,
                item.RightRowIndex.HasValue
                    ? (item.RightRowIndex.Value + 1).ToString()
                    : string.Empty);

            // 差分の種類
            AppendHtmlCell(
                html,
                GetDifferenceName(item.Difference));

            // 比較元のセル
            for (int c = 0; c < columnCount; c++)
            {
                string value = GetCellValue(item.Left, c);

                bool changed =
                    item.Difference == DifferenceType.Changed &&
                    item.ChangedColumns.Contains(c);

                var segments = changed
                    ? CellDiff.Compare(
                        value,
                        GetCellValue(item.Right, c)).Left
                    : null;

                string background = item.Difference switch
                {
                    DifferenceType.Deleted => "#FFDDDD",
                    DifferenceType.Changed when changed => "#FFF2B4",
                    _ => "#FFFFFF"
                };

                AppendHtmlCell(
                    html,
                    value,
                    segments,
                    background,
                    isLeft: true);
            }

            // 比較先のセル
            for (int c = 0; c < columnCount; c++)
            {
                string value = GetCellValue(item.Right, c);

                bool changed =
                    item.Difference == DifferenceType.Changed &&
                    item.ChangedColumns.Contains(c);

                var segments = changed
                    ? CellDiff.Compare(
                        GetCellValue(item.Left, c),
                        value).Right
                    : null;

                string background = item.Difference switch
                {
                    DifferenceType.Added => "#DAF2DA",
                    DifferenceType.Changed when changed => "#FFF2B4",
                    _ => "#FFFFFF"
                };

                AppendHtmlCell(
                    html,
                    value,
                    segments,
                    background,
                    isLeft: false);
            }

            html.Append("</tr>");
        }

        html.Append("""
            </table>
            <!--EndFragment-->
            </body>
            </html>
            """);

        return CreateCfHtml(html.ToString());
    }

    /// <summary>
    /// HTML形式のクリップボードデータに必要なヘッダーを付与する。
    /// </summary>
    private static string CreateCfHtml(string html)
    {
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";

        int fragmentStart = html.IndexOf(
            startMarker,
            StringComparison.Ordinal) + startMarker.Length;

        int fragmentEnd = html.IndexOf(
            endMarker,
            StringComparison.Ordinal);

        string htmlPrefix = html[..fragmentStart];
        string fragment = html[fragmentStart..fragmentEnd];
        string htmlSuffix = html[fragmentEnd..];

        const string headerTemplate =
            "Version:1.0\r\n" +
            "StartHTML:{0:D10}\r\n" +
            "EndHTML:{1:D10}\r\n" +
            "StartFragment:{2:D10}\r\n" +
            "EndFragment:{3:D10}\r\n";

        // CF_HTMLのオフセットはUTF-8のバイト数で指定する
        int headerLength = Encoding.UTF8.GetByteCount(
            string.Format(
                headerTemplate,
                0, 0, 0, 0));

        int startHtml = headerLength;
        int startFragment = startHtml +
            Encoding.UTF8.GetByteCount(htmlPrefix);

        int endFragment = startFragment +
            Encoding.UTF8.GetByteCount(fragment);

        int endHtml = endFragment +
            Encoding.UTF8.GetByteCount(htmlSuffix);

        string header = string.Format(
            headerTemplate,
            startHtml,
            endHtml,
            startFragment,
            endFragment);

        return header + html;
    }

    /// <summary>
    /// HTMLのセルを生成する。
    /// </summary>
    private static void AppendHtmlCell(
        StringBuilder html,
        string value,
        IReadOnlyList<TextSegment>? segments = null,
        string background = "#FFFFFF",
        bool isLeft = false)
    {
        html.Append(
            $"<td style=\"border:1px solid #999;padding:5px;" +
            $"background-color:{background};white-space:pre-wrap;\">");

        if (segments is null)
        {
            html.Append(WebUtility.HtmlEncode(value));
        }
        else
        {
            foreach (var segment in segments)
            {
                string encoded = WebUtility.HtmlEncode(segment.Text);

                if (segment.IsDifferent)
                {
                    string color = isLeft
                        ? "#D9534F"
                        : "#239B56";

                    html.Append(
                        $"<span style=\"color:{color};font-weight:bold;\">");
                    html.Append(encoded);
                    html.Append("</span>");
                }
                else
                {
                    html.Append(encoded);
                }
            }
        }

        html.Append("</td>");
    }

    /// <summary>
    /// 指定列の値を取得する。
    /// </summary>
    private static string GetCellValue(string[]? row, int column)
    {
        return row is not null && column < row.Length
            ? row[column]
            : string.Empty;
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

    /// <summary>
    /// 既存のテキスト形式出力。
    /// HTML形式に対応していないアプリ向けの代替データ。
    /// </summary>
    public static string CreateComparisonTsv(
        IReadOnlyList<RowComparison> comparisons,
        int columnCount,
        bool diffOnly)
    {
        var builder = new StringBuilder();

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

        AppendTsvLine(builder, headers);

        foreach (var item in comparisons)
        {
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

            for (int i = 0; i < columnCount; i++)
                values.Add(GetCellValue(item.Left, i));

            for (int i = 0; i < columnCount; i++)
                values.Add(GetCellValue(item.Right, i));

            AppendTsvLine(builder, values);
        }

        return builder.ToString();
    }

    private static void AppendTsvLine(
        StringBuilder builder,
        IReadOnlyList<string> values)
    {
        builder.AppendLine(String.Join(
            "\t",
            values.Select(value =>
                (value ?? string.Empty)
                    .Replace("\\", "\\\\")
                    .Replace("\t", "\\t")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n"))));
    }
}