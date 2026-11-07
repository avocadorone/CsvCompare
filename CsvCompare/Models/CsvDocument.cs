namespace CsvCompare.Models;

/// <summary>
/// 読み込んだCSVファイルの情報と内容を保持するクラス。
/// ファイルパスとCSVの行データを管理する。
/// </summary>
public sealed class CsvDocument
{
    /// <summary>
    /// 読み込んだCSVファイルのパス。
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// CSVの行データ。
    /// 1つの要素が1行を表し、各行は列ごとの文字列配列で構成される。
    /// </summary>
    public IReadOnlyList<string[]> Rows { get; }

    /// <summary>
    /// CSVファイルの情報と行データを設定する。
    /// </summary>
    /// <param name="filePath">CSVファイルのパス</param>
    /// <param name="rows">CSVの行データ</param>
    public CsvDocument(string filePath, IReadOnlyList<string[]> rows)
    {
        FilePath = filePath;
        Rows = rows;
    }
}