namespace CsvCompare.Services;

/// <summary>
/// 文字列内の差分を表す1つの区間。
/// </summary>
/// <param name="Text">表示する文字列</param>
/// <param name="IsDifferent">差分文字列かどうか</param>
public sealed record TextSegment(string Text, bool IsDifferent);

/// <summary>
/// 2つの文字列を文字単位で比較するサービス。
/// LCS（最長共通部分列）を使用して差分を抽出する。
/// </summary>
public static class CellDiff
{
    /// <summary>
    /// 左右の文字列を比較し、それぞれの表示用区間を返す。
    /// </summary>
    public static (
        IReadOnlyList<TextSegment> Left,
        IReadOnlyList<TextSegment> Right)
        Compare(string left, string right)
    {
        int n = left.Length;
        int m = right.Length;

        // LCSの長さを保持するテーブル
        var dp = new int[n + 1, m + 1];

        // 最長共通部分列の長さを計算
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                if (left[i - 1] == right[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(
                        dp[i - 1, j],
                        dp[i, j - 1]);
                }
            }
        }

        // 差分操作を逆順に格納する
        var operations = new List<(char Character, DiffKind Kind)>();

        int x = n;
        int y = m;

        // LCSテーブルを末尾からたどって差分を抽出
        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 &&
                left[x - 1] == right[y - 1])
            {
                operations.Add((left[x - 1], DiffKind.Common));
                x--;
                y--;
            }
            else if (x > 0 &&
                     (y == 0 || dp[x - 1, y] >= dp[x, y - 1]))
            {
                operations.Add((left[x - 1], DiffKind.Removed));
                x--;
            }
            else
            {
                operations.Add((right[y - 1], DiffKind.Added));
                y--;
            }
        }

        // 元の文字列の順番に戻す
        operations.Reverse();

        // 左右それぞれの描画区間を作成
        var leftSegments = new List<TextSegment>();
        var rightSegments = new List<TextSegment>();

        foreach (var operation in operations)
        {
            // 比較元には共通文字と削除文字を表示
            if (operation.Kind != DiffKind.Added)
            {
                AddSegment(
                    leftSegments,
                    operation.Character,
                    operation.Kind == DiffKind.Removed);
            }

            // 比較先には共通文字と追加文字を表示
            if (operation.Kind != DiffKind.Removed)
            {
                AddSegment(
                    rightSegments,
                    operation.Character,
                    operation.Kind == DiffKind.Added);
            }
        }

        return (leftSegments, rightSegments);
    }

    /// <summary>
    /// 文字を描画区間に追加する。
    /// 隣接する同じ種類の文字は1つの区間にまとめる。
    /// </summary>
    private static void AddSegment(
        List<TextSegment> segments,
        char character,
        bool isDifferent)
    {
        if (segments.Count > 0 &&
            segments[^1].IsDifferent == isDifferent)
        {
            var previous = segments[^1];

            segments[^1] = previous with
            {
                Text = previous.Text + character
            };
        }
        else
        {
            segments.Add(new TextSegment(
                character.ToString(),
                isDifferent));
        }
    }

    /// <summary>
    /// 差分の種類。
    /// </summary>
    private enum DiffKind
    {
        Common,
        Removed,
        Added
    }
}