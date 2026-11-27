using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fold3DSpike.Runtime.UI
{
    /// <summary>
    /// Korean-capable OS font for IMGUI, created once without any font asset.
    /// Only fonts actually installed on the OS are requested; if none is installed or creation fails in any way
    /// (e.g. batchmode CI without fonts), <see cref="Get"/> returns null and the caller keeps the default skin font.
    /// Never logs: an error log would fail PlayMode tests.
    /// </summary>
    internal static class KoreanFont
    {
        /// <summary>Candidate OS fonts in preference order.</summary>
        private static readonly string[] Candidates =
        {
            "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "NanumGothic", "Arial",
        };

        private static bool s_tried;
        private static Font s_font;

        /// <summary>The created font, or null to use the default font. Tries creation at most once per domain.</summary>
        public static Font Get(int size)
        {
            if (s_font != null) return s_font;
            if (s_tried) return null;
            s_tried = true;
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                if (installed == null || installed.Length == 0) return null;

                var available = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
                var names = new List<string>();
                foreach (var candidate in Candidates)
                {
                    if (available.Contains(candidate)) names.Add(candidate);
                }
                if (names.Count == 0) return null;

                s_font = Font.CreateDynamicFontFromOSFont(names.ToArray(), size);
                if (s_font != null) s_font.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception)
            {
                // Deliberately silent (see class remarks): fall back to the default font.
                s_font = null;
            }
            return s_font != null ? s_font : null;
        }
    }
}
