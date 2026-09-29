using System;

namespace WireDrop.Ranking
{
    /// <summary>
    /// Ranks a component against typed text. Deliberately ordered so that what you are
    /// most likely to have meant sorts first: an exact prefix beats a word start, which
    /// beats a substring, which beats a scattered subsequence.
    /// </summary>
    internal static class Fuzzy
    {
        public static int Score(string name, string nickName, string category, string subCategory, string query)
        {
            if (string.IsNullOrEmpty(query)) return 0;
            if (string.IsNullOrEmpty(name)) return 0;

            var q = query.ToLowerInvariant();
            var n = name.ToLowerInvariant();
            if (n.StartsWith(q, StringComparison.Ordinal)) return 100;

            var nick = (nickName ?? string.Empty).ToLowerInvariant();
            if (nick.Length > 0 && nick.StartsWith(q, StringComparison.Ordinal)) return 92;

            var words = n.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < words.Length; i++)
                if (words[i].StartsWith(q, StringComparison.Ordinal)) return 80;

            if (n.Contains(q)) return 62;

            var path = ((category ?? string.Empty) + " " + (subCategory ?? string.Empty)).ToLowerInvariant();
            if (path.Length > 1 && path.StartsWith(q, StringComparison.Ordinal)) return 46;

            var j = 0;
            for (int i = 0; i < n.Length && j < q.Length; i++)
                if (n[i] == q[j]) j++;
            return j == q.Length ? 30 : 0;
        }
    }
}
