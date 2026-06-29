using System;
using System.Collections.Generic;
using System.Windows;

namespace todochart.Services;

/// <summary>
/// テーマ切り替えサービス。MergedDictionaries の先頭辞書をテーマ辞書に差し替える。
/// </summary>
public static class ThemeService
{
    public static event Action? ThemeChanged;

    private static readonly IReadOnlyDictionary<string, string> s_themeUris =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Light", "Themes/Colors/ThemeLight.xaml" },
            { "DarkCyan", "Themes/Colors/ThemeDarkCyan.xaml" },
        };

    public static IEnumerable<string> ThemeNames => s_themeUris.Keys;

    public static void ApplyTheme(string themeName)
    {
        if (!s_themeUris.TryGetValue(themeName, out var uriStr))
            uriStr = s_themeUris["Light"];

        var uri = new Uri(uriStr, UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        // Remove any existing theme dictionaries (Themes/Colors/*.xaml) to avoid duplicates or stale resources
        var removeIndices = new List<int>();
        for (int i = 0; i < merged.Count; i++)
        {
            try
            {
                var src = merged[i].Source;
                if (src != null && src.OriginalString.IndexOf("Themes/Colors/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    removeIndices.Add(i);
                }
            }
            catch { }
        }

        // Remove from end to keep indices valid
        for (int i = removeIndices.Count - 1; i >= 0; i--)
            merged.RemoveAt(removeIndices[i]);

        // Insert new theme dictionary after BaseTheme if present, otherwise append
        int baseIndex = -1;
        for (int i = 0; i < merged.Count; i++)
        {
            try
            {
                if (merged[i].Source != null && merged[i].Source.OriginalString.IndexOf("Themes/BaseTheme.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseIndex = i;
                    break;
                }
            }
            catch { }
        }

        if (baseIndex >= 0)
            merged.Insert(baseIndex + 1, dict);
        else
            merged.Add(dict);

        ThemeChanged?.Invoke();
    }

    public static bool IsValidTheme(string? name) => name is not null && s_themeUris.ContainsKey(name);
}
