using Microsoft.CodeAnalysis.Diagnostics;

namespace SaveDataGenerator;

internal static class AnalyzerConfigOptionsExtensions
{
    public static bool GetBool(this AnalyzerConfigOptions options, string key, bool defaultValue = false)
        => options.TryGetValue(key, out var val) && bool.TryParse(val, out var result) ? result : defaultValue;

    public static string? GetString(this AnalyzerConfigOptions options, string key)
        => options.TryGetValue(key, out var val) ? val : null;
}