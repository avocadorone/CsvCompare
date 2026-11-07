using CsvCompare.Models;

namespace CsvCompare.Services;

/// <summary>
/// 行番号（位置）を基準に左右のCSVを比較するサービス。
/// キー列による並べ替え・照合は行わず、同じ位置にある行同士を比較する。
/// </summary>
public sealed class CsvComparator
{
    #region 比較元と比較先のCSVデータを比較する。
    /// <summary>
    /// 比較元と比較先のCSVデータを比較する。
    /// 行の追加・削除・変更・一致を判定し、比較結果を返す。
    /// </summary>
    /// <param name="left">比較元CSVの行データ</param>
    /// <param name="right">比較先CSVの行データ</param>
    /// <returns>行ごとの比較結果一覧</returns>
    public IReadOnlyList<RowComparison> Compare(
        IReadOnlyList<string[]> left,
        IReadOnlyList<string[]> right)
    {
        // 左右のCSVのうち、行数が多い方を比較対象の行数とする
        int count = Math.Max(left.Count, right.Count);

        // 比較結果を格納するリストを作成
        // あらかじめ最大行数分の容量を確保しておく
        var results = new List<RowComparison>(count);

        // 先頭行から末尾行まで、同じ行番号同士を比較する
        for (int i = 0; i < count; i++)
        {
            // 比較元の対象行を取得
            // 指定した行番号が存在しない場合はnull
            string[]? l = i < left.Count ? left[i] : null;

            // 比較先の対象行を取得
            // 指定した行番号が存在しない場合はnull
            string[]? r = i < right.Count ? right[i] : null;

            // 比較元に行が存在しない場合は、比較先に追加された行と判定
            if (l is null)
            {
                results.Add(new RowComparison
                {
                    RowNumber = i + 1,
                    Left = null,
                    Right = r,
                    Difference = DifferenceType.Added
                });

                // 追加行の判定が完了したため、次の行へ進む
                continue;
            }

            // 比較先に行が存在しない場合は、比較元から削除された行と判定
            if (r is null)
            {
                results.Add(new RowComparison
                {
                    RowNumber = i + 1,
                    Left = l,
                    Right = null,
                    Difference = DifferenceType.Deleted
                });

                // 削除行の判定が完了したため、次の行へ進む
                continue;
            }

            // 左右の行のうち、列数が多い方を比較対象の列数とする
            int columns = Math.Max(l.Length, r.Length);

            // 変更された列番号を格納する集合を作成
            var changed = new HashSet<int>();

            // 左右の列を1列ずつ比較する
            for (int c = 0; c < columns; c++)
            {
                // 比較元の列データを取得
                // 列が存在しない場合は空文字として扱う
                string lv = c < l.Length ? l[c] : string.Empty;

                // 比較先の列データを取得
                // 列が存在しない場合は空文字として扱う
                string rv = c < r.Length ? r[c] : string.Empty;

                // 左右の値を完全一致で比較する
                // 大文字・小文字や空白の違いも差分として扱う
                if (!String.Equals(lv, rv, StringComparison.Ordinal))
                {
                    // 値が異なる場合は、変更された列番号を記録
                    changed.Add(c);
                }
            }

            // 1行分の比較結果を作成してリストに追加
            results.Add(new RowComparison
            {
                // 行番号は0始まりのインデックスに1を加えて設定
                RowNumber = i + 1,

                // 比較元・比較先の行データを保持
                Left = l,
                Right = r,

                // 変更された列がなければ一致、1つ以上あれば変更と判定
                Difference = changed.Count == 0
                    ? DifferenceType.Same
                    : DifferenceType.Changed,

                // 変更された列番号の集合を設定
                ChangedColumns = changed
            });
        }

        // 全行の比較結果を返す
        return results;
    }
    #endregion
}