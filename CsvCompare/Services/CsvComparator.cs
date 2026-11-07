using CsvCompare.Models;

namespace CsvCompare.Services;

/// <summary>
/// 左右のCSVを比較するサービス。
/// 手動で対応付けされた行を優先し、残りは同じ行番号同士で比較する。
/// </summary>
public sealed class CsvComparator
{
    #region CSV比較処理

    /// <summary>
    /// 左右のCSVを比較する。
    /// 手動対応付けが指定されている場合は、その対応を優先する。
    /// </summary>
    /// <param name="left">比較元CSVの行データ</param>
    /// <param name="right">比較先CSVの行データ</param>
    /// <param name="manualPairs">
    /// 手動対応付け情報。
    /// Keyは左行インデックス、Valueは右行インデックス（どちらも0始まり）。
    /// </param>
    /// <returns>行ごとの比較結果</returns>
    public IReadOnlyList<RowComparison> Compare(
        IReadOnlyList<string[]> left,
        IReadOnlyList<string[]> right,
        IReadOnlyDictionary<int, int>? manualPairs = null)
    {
        // 対応付け済みの行インデックスを管理する
        var usedLeft = new HashSet<int>();
        var usedRight = new HashSet<int>();

        // 左右の行の対応関係を保持する
        var alignments = new List<(int? Left, int? Right)>();

        // 手動対応付けを優先して登録する
        if (manualPairs is not null)
        {
            foreach (var pair in manualPairs)
            {
                int leftIndex = pair.Key;
                int rightIndex = pair.Value;

                // 行インデックスが範囲内か確認
                if (leftIndex < 0 || leftIndex >= left.Count)
                    throw new ArgumentOutOfRangeException(nameof(manualPairs),
                        $"左側の行インデックスが不正です: {leftIndex}");

                if (rightIndex < 0 || rightIndex >= right.Count)
                    throw new ArgumentOutOfRangeException(nameof(manualPairs),
                        $"右側の行インデックスが不正です: {rightIndex}");

                // 同じ行を複数回対応付けない
                if (!usedLeft.Add(leftIndex))
                    throw new ArgumentException("左側の行が重複して対応付けられています。");

                if (!usedRight.Add(rightIndex))
                    throw new ArgumentException("右側の行が重複して対応付けられています。");

                alignments.Add((leftIndex, rightIndex));
            }
        }

        // 手動対応付けされていない行は、同じ行番号同士で比較する
        int maxCount = Math.Max(left.Count, right.Count);

        for (int i = 0; i < maxCount; i++)
        {
            bool hasLeft = i < left.Count && !usedLeft.Contains(i);
            bool hasRight = i < right.Count && !usedRight.Contains(i);

            if (hasLeft && hasRight)
            {
                // 左右とも未対応なら、同じ行番号同士を対応付ける
                alignments.Add((i, i));
                usedLeft.Add(i);
                usedRight.Add(i);
            }
            else if (hasLeft)
            {
                // 左側だけ残っている場合は削除行
                alignments.Add((i, null));
                usedLeft.Add(i);
            }
            else if (hasRight)
            {
                // 右側だけ残っている場合は追加行
                alignments.Add((null, i));
                usedRight.Add(i);
            }
        }

        // 元の行位置を基準に表示順を整える
        var orderedAlignments = alignments
            .OrderBy(x => Math.Min(
                x.Left ?? int.MaxValue,
                x.Right ?? int.MaxValue))
            .ThenBy(x => x.Left ?? int.MaxValue)
            .ThenBy(x => x.Right ?? int.MaxValue)
            .ToList();

        // 比較結果を作成
        var results = new List<RowComparison>(orderedAlignments.Count);

        foreach (var alignment in orderedAlignments)
        {
            int? leftIndex = alignment.Left;
            int? rightIndex = alignment.Right;

            string[]? leftRow =
                leftIndex.HasValue ? left[leftIndex.Value] : null;

            string[]? rightRow =
                rightIndex.HasValue ? right[rightIndex.Value] : null;

            // 追加行
            if (leftRow is null)
            {
                results.Add(new RowComparison
                {
                    RowNumber = results.Count + 1,
                    LeftRowIndex = null,
                    RightRowIndex = rightIndex,
                    Left = null,
                    Right = rightRow,
                    Difference = DifferenceType.Added
                });

                continue;
            }

            // 削除行
            if (rightRow is null)
            {
                results.Add(new RowComparison
                {
                    RowNumber = results.Count + 1,
                    LeftRowIndex = leftIndex,
                    RightRowIndex = null,
                    Left = leftRow,
                    Right = null,
                    Difference = DifferenceType.Deleted
                });

                continue;
            }

            // 左右の列数の最大値を取得
            int columns = Math.Max(leftRow.Length, rightRow.Length);

            // 変更された列番号を保持
            var changedColumns = new HashSet<int>();

            for (int c = 0; c < columns; c++)
            {
                string leftValue =
                    c < leftRow.Length ? leftRow[c] : string.Empty;

                string rightValue =
                    c < rightRow.Length ? rightRow[c] : string.Empty;

                if (!String.Equals(
                    leftValue,
                    rightValue,
                    StringComparison.Ordinal))
                {
                    changedColumns.Add(c);
                }
            }

            // 比較結果を追加
            results.Add(new RowComparison
            {
                RowNumber = results.Count + 1,
                LeftRowIndex = leftIndex,
                RightRowIndex = rightIndex,
                Left = leftRow,
                Right = rightRow,
                Difference = changedColumns.Count == 0
                    ? DifferenceType.Same
                    : DifferenceType.Changed,
                ChangedColumns = changedColumns
            });
        }

        return results;
    }

    #endregion
}