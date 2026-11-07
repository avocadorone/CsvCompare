namespace CsvCompare.Models;

public enum DifferenceType
{
    Same,
    Changed,
    Added,
    Deleted
}

public sealed class RowComparison
{
    public int RowNumber { get; init; }
    public string[]? Left { get; init; }
    public string[]? Right { get; init; }
    public DifferenceType Difference { get; init; }
    public HashSet<int> ChangedColumns { get; init; } = new();
}
