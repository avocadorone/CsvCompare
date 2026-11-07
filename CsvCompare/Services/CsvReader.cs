using System.Text;
using CsvCompare.Models;

namespace CsvCompare.Services;

/// <summary>
/// CSVファイルの読み込みと解析を担当するサービス。
/// 文字コードの判定、CSVの解析、行データへの変換を行う。
/// 引用符で囲まれたカンマや改行にも対応する。
/// </summary>
public sealed class CsvReader
{
    #region CSVファイルを読み込み、CsvDocumentとして返す。
    /// <summary>
    /// CSVファイルを読み込み、CsvDocumentとして返す。
    /// ファイルの文字コードを判定したうえで内容を解析する。
    /// </summary>
    /// <param name="path">読み込むCSVファイルのパス</param>
    /// <returns>ファイルパスと解析済み行データを保持するCSVドキュメント</returns>
    public CsvDocument Read(string path)
    {
        // ファイルの文字コードを判定して、テキストとして読み込む
        string content = File.ReadAllText(path, DetectEncoding(path));

        // 読み込んだテキストをCSV形式として解析し、CSVドキュメントを生成
        return new CsvDocument(path, Parse(content));
    }
    #endregion

    #region CSVファイルの文字コードを判定する。
    /// <summary>
    /// CSVファイルの文字コードを判定する。
    /// BOMが存在する場合はBOMを優先し、
    /// BOMがない場合はUTF-8として判定して、失敗した場合はWindows-31Jを使用する。
    /// </summary>
    /// <param name="path">判定対象のCSVファイルのパス</param>
    /// <returns>判定された文字コードのEncoding</returns>
    private static Encoding DetectEncoding(string path)
    {
        // ファイルをバイト配列として読み込む
        byte[] bytes = File.ReadAllBytes(path);

        // UTF-8 BOM（EF BB BF）が付いているか確認
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(true);

        // UTF-16 LE BOM（FF FE）が付いているか確認
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;

        // UTF-16 BE BOM（FE FF）が付いているか確認
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        // BOMなしの場合は、まずUTF-8として厳密に判定する
        try
        {
            // 不正なUTF-8シーケンスが存在する場合は例外を発生させる
            _ = new UTF8Encoding(false, true).GetString(bytes);

            // UTF-8として正常に読み込める場合はUTF-8を返す
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            // UTF-8として読み込めない場合はWindows-31J（コードページ932）を使用
            return Encoding.GetEncoding(932);
        }
    }
    #endregion

    #region CSV形式のテキストを解析し、行・列単位のデータに変換する。
    /// <summary>
    /// CSV形式のテキストを解析し、行・列単位のデータに変換する。
    /// 引用符で囲まれたカンマや改行、二重引用符のエスケープに対応する。
    /// </summary>
    /// <param name="text">解析対象のCSVテキスト</param>
    /// <returns>行ごとの列データを格納したリスト</returns>
    public static List<string[]> Parse(string text)
    {
        // CSV全体の行データを格納するリスト
        var rows = new List<string[]>();

        // 現在解析中の1行分の列データを格納するリスト
        var row = new List<string>();

        // 現在解析中の1列分の文字列を構築するためのバッファ
        var field = new StringBuilder();

        // 現在、引用符の内側を解析しているかどうかを管理
        bool inQuotes = false;

        // CSVテキストを先頭から1文字ずつ解析する
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // 引用符の内側を解析している場合
            if (inQuotes)
            {
                if (c == '"')
                {
                    // 連続する二重引用符（""）は、データ上の1つの引用符として扱う
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');

                        // 2文字分処理したため、次の文字をスキップ
                        i++;
                    }
                    else
                    {
                        // 閉じ引用符に到達したため、引用符の内側から抜ける
                        inQuotes = false;
                    }
                }
                else
                {
                    // 引用符の内側ではカンマや改行も通常の文字として追加
                    field.Append(c);
                }

                // 引用符の内側の処理が完了したため、次の文字へ進む
                continue;
            }

            // 引用符の外側では文字の種類に応じて処理を分岐
            switch (c)
            {
                case '"':
                    // 列の先頭にある引用符は、引用符の開始として扱う
                    if (field.Length == 0)
                        inQuotes = true;
                    else
                        // 列の途中にある引用符は通常の文字として追加
                        field.Append(c);

                    break;

                case ',':
                    // カンマに到達したら、現在の列データを確定
                    row.Add(field.ToString());

                    // 次の列を解析するためにバッファをクリア
                    field.Clear();
                    break;

                case '\r':
                    // CRLF（\r\n）の場合は、2文字を1つの改行として扱う
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    // 現在の列を確定
                    row.Add(field.ToString());
                    field.Clear();

                    // 現在の行を確定して全体のリストに追加
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;

                case '\n':
                    // LF単独の改行の場合も行の区切りとして扱う
                    row.Add(field.ToString());
                    field.Clear();

                    // 現在の行を確定して全体のリストに追加
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;

                default:
                    // カンマ・改行・引用符以外の文字は列データに追加
                    field.Append(c);
                    break;
            }
        }

        // 最終行の処理
        // 末尾が改行の場合は、不要な空行を追加しない
        if (field.Length > 0 || row.Count > 0 || (text.Length > 0 && text[^1] != '\n' && text[^1] != '\r'))
        {
            // 最後に解析していた列を確定
            row.Add(field.ToString());

            // 最後の行を全体のリストに追加
            rows.Add(row.ToArray());
        }

        // 解析した全行のデータを返す
        return rows;
    }
    #endregion
}