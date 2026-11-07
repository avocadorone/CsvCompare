using System.Text;
using CsvCompare.Models;

namespace CsvCompare.Services;

/// <summary>
/// CSVファイルの読み込みと、引用符・改行を考慮したCSV解析を担当します。
/// </summary>
public sealed class CsvReader
{
    public CsvDocument Read(string path)
    {
        string content = File.ReadAllText(path, DetectEncoding(path));
        return new CsvDocument(path, Parse(content));
    }

    private static Encoding DetectEncoding(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(true);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        // BOMなしは、まずUTF-8として厳密に判定。失敗した場合はWindows-31Jとして読む。
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(932);
        }
    }

    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    if (field.Length == 0)
                        inQuotes = true;
                    else
                        field.Append(c);
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        // 最終行。末尾改行だけの場合に不要な空行を追加しない。
        if (field.Length > 0 || row.Count > 0 || (text.Length > 0 && text[^1] != '\n' && text[^1] != '\r'))
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}
