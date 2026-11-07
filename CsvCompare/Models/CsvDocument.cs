namespace CsvCompare.Models;

public sealed class CsvDocument
{
    public string FilePath { get; }
    public IReadOnlyList<string[]> Rows { get; }

    public CsvDocument(string filePath, IReadOnlyList<string[]> rows)
    {
        FilePath = filePath;
        Rows = rows;
    }
}
