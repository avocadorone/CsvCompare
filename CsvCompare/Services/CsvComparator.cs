using CsvCompare.Models;

namespace CsvCompare.Services;

/// <summary>
/// 行番号（位置）を基準に左右のCSVを比較します。キー列による並べ替え・照合は行いません。
/// </summary>
public sealed class CsvComparator
{
    public IReadOnlyList<RowComparison> Compare(
        IReadOnlyList<string[]> left,
        IReadOnlyList<string[]> right)
    {
        int count = Math.Max(left.Count, right.Count);
        var results = new List<RowComparison>(count);

        for (int i = 0; i < count; i++)
        {
            string[]? l = i < left.Count ? left[i] : null;
            string[]? r = i < right.Count ? right[i] : null;

            if (l is null)
            {
                results.Add(new RowComparison
                {
                    RowNumber = i + 1, Left = null, Right = r,
                    Difference = DifferenceType.Added
                });
                continue;
            }

            if (r is null)
            {
                results.Add(new RowComparison
                {
                    RowNumber = i + 1, Left = l, Right = null,
                    Difference = DifferenceType.Deleted
                });
                continue;
            }

            int columns = Math.Max(l.Length, r.Length);
            var changed = new HashSet<int>();

            for (int c = 0; c < columns; c++)
            {
                string lv = c < l.Length ? l[c] : string.Empty;
                string rv = c < r.Length ? r[c] : string.Empty;
                if (!String.Equals(lv, rv, StringComparison.Ordinal))
                    changed.Add(c);
            }

            results.Add(new RowComparison
            {
                RowNumber = i + 1,
                Left = l,
                Right = r,
                Difference = changed.Count == 0 ? DifferenceType.Same : DifferenceType.Changed,
                ChangedColumns = changed
            });
        }

        return results;
    }
}
