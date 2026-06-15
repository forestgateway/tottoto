using System.Windows;

namespace todochart.Services;

/// <summary>
/// アプリのカラーテーマを管理するサービス。
/// MergedDictionaries の先頭エントリをテーマファイルに差し替えることで再起動なしに切り替える。
/// </summary>
public static class ThemeService
{
    /// <summary>テーマが切り替わったときに発火するイベント。Controls の再描画に使用する。</summary>
    public static event Action? ThemeChanged;

    private static readonly IReadOnlyDictionary<string, string> s_themeUris =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DarkCyan",   "Themes/Colors/ThemeDarkCyan.xaml"   },
            { "DarkPurple", "Themes/Colors/ThemeDarkPurple.xaml" },
            { "DarkGreen",  "Themes/Colors/ThemeDarkGreen.xaml"  },
            { "DarkOrange", "Themes/Colors/ThemeDarkOrange.xaml" },
            { "Light",      "Themes/Colors/ThemeLight.xaml"      },
        };

    /// <summary>有効なテーマ名の一覧。</summary>
    public static IEnumerable<string> ThemeNames => s_themeUris.Keys;

    /// <summary>
    /// テーマを適用する。
    /// Application.Resources の MergedDictionaries[0] をテーマファイルに差し替える。
    /// </summary>
    /// <param name="themeName">テーマ名（DarkCyan / DarkPurple / DarkGreen / DarkOrange / Light）</param>
    public static void ApplyTheme(string themeName)
    {
        if (!s_themeUris.TryGetValue(themeName, out var relativeUri))
            relativeUri = s_themeUris["DarkCyan"];

        var uri = new Uri(relativeUri, UriKind.Relative);
        var merged = Application.Current.Resources.MergedDictionaries;

        // 先頭がカラーテーマ辞書なので差し替える
        var newDict = new ResourceDictionary { Source = uri };
        if (merged.Count > 0)
            merged[0] = newDict;
        else
            merged.Insert(0, newDict);

        ThemeChanged?.Invoke();
    }

    /// <summary>テーマ名が有効かどうかを返す。</summary>
    public static bool IsValidTheme(string? themeName)
        => themeName is not null && s_themeUris.ContainsKey(themeName);
}
