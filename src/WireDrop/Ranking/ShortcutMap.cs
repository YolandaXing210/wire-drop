using System;

namespace WireDrop.Ranking
{
    /// <summary>
    /// Grasshopper's canvas search reads a few leading characters as "make me this object"
    /// rather than "find me this name" — the grammar in
    /// <c>GH_PopupSearchDialog.CreateImpliedObject</c>. This restates it, free of
    /// Grasshopper types so it can be tested without a Rhino install. The numeric
    /// shortcuts are not here: only Grasshopper's own parser can settle those.
    /// </summary>
    internal static class ShortcutMap
    {
        /// <summary>
        /// The components Grasshopper puts behind a single symbol, by the guids it names
        /// them with. They are resolved through the installed catalog at runtime, so a guid
        /// that no longer exists costs a missing row rather than a wrong one.
        /// </summary>
        static readonly string[][] Symbols =
        {
            new[] { "+",  "A0D62394-A118-422D-ABB3-6AF115C75B25" },
            new[] { "-",  "9C007A04-D0D9-48E4-9DA3-9BA142BC4D46" },
            new[] { "*",  "CE46B74E-00C9-43C4-805A-193B69EA4A11" },
            new[] { "/",  "9C85271F-89FA-4e9f-9F4A-D75802120CCC" },
            new[] { "\\", "54DB2568-3441-4ae2-BCEF-92C4CC608E11" },
            new[] { "%",  "431BC610-8AE1-4090-B217-1A9D9C519FE2" },
            new[] { "&",  "2013E425-8713-42e2-A661-B57E78840337" },
            new[] { "<",  "AE840986-CADE-4e5a-96B0-570F007D4FC0" },
            new[] { ">",  "30D58600-1AAB-42db-80A3-F1EA6C4269A0" },
            new[] { "=",  "5DB0FB89-4F22-4f09-A777-FA5E55AED7EC" },
        };

        /// <summary>Grasshopper's expression component, reached by starting to write one.</summary>
        const string ExpressionId = "9DF5E896-552D-4c8c-B9CA-4FC147FFA022";

        /// <summary>
        /// A quoted string or a comment opener makes a panel holding that text:
        /// <c>"hello</c> and <c>//hello</c> both give a panel reading "hello".
        /// </summary>
        public static bool TryPanel(string text, out string content)
        {
            content = null;
            if (string.IsNullOrEmpty(text)) return false;

            if (text[0] == '"')
            {
                content = text.Replace("\"", string.Empty);
                return true;
            }
            if (text.Length > 2 && text[0] == '/' && text[1] == '/')
            {
                content = text.Substring(2);
                return true;
            }
            return false;
        }

        /// <summary><c>~note</c> makes a scribble on the canvas reading "note".</summary>
        public static bool TryScribble(string text, out string content)
        {
            content = null;
            if (string.IsNullOrEmpty(text) || text.Length < 2 || text[0] != '~') return false;
            content = text.Substring(1);
            return true;
        }

        /// <summary>The guid of the component a lone symbol stands for, or null.</summary>
        public static string Component(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            foreach (var pair in Symbols)
                if (string.Equals(text, pair[0], StringComparison.Ordinal)) return pair[1];

            // "f(" is how you start writing an expression, so it opens the component for it.
            if (text.StartsWith("f(", StringComparison.Ordinal)) return ExpressionId;
            return null;
        }

        /// <summary>
        /// Two numbers with a comma between them read as a point. Grasshopper wants them
        /// adjacent; a space after the comma is allowed here because the coordinates still
        /// parse and typing one is natural. Only a first pass — Grasshopper's own converter
        /// decides whether the text really is a point.
        /// </summary>
        public static bool LooksLikePoint(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (!char.IsDigit(text[i])) continue;
                var j = i + 1;
                while (j < text.Length && text[j] == ' ') j++;
                if (j >= text.Length || text[j] != ',') continue;
                j++;
                while (j < text.Length && text[j] == ' ') j++;
                if (j < text.Length && char.IsDigit(text[j])) return true;
            }
            return false;
        }
    }
}
