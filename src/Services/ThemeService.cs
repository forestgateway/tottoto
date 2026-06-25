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
        // Expect BaseTheme to be at index 0 and theme dictionary at index 1.
        // Replace theme dictionary if present, otherwise insert after BaseTheme when possible.
        if (merged.Count > 1)
            merged[1] = dict;
        else if (merged.Count == 1)
            merged.Insert(1, dict);
        else
            merged.Insert(0, dict);

        ThemeChanged?.Invoke();
    }

    public static bool IsValidTheme(string? name) => name is not null && s_themeUris.ContainsKey(name);
}
